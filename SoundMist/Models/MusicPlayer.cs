using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TagLib;

namespace SoundMist.Models
{
    public enum PlayState
    {
        Playing,
        Paused,
        Ended,
        Loaded,
        Error
    }

    //TODO decouple the logic of music player class from the downloaders
    public class MusicPlayer : IMusicPlayer
    {
        public Track? CurrentTrack { get => _settings.LastTrack; private set => _settings.LastTrack = value; }
        public TracksPlaylist TracksPlaylist { get; } = new();

        public float DesiredVolume
        {
            get => _settings.Volume;
            set
            {
                _settings.Volume = value;
                if (_waveOut != null)
                    _waveOut.Volume = value;
            }
        }

        public bool PlayerReady => !_switchingTracks;
        public bool Playing => _playing;

        private volatile WaveStream? _waveStream;
        private volatile WaveOutEvent? _waveOut;
        private volatile MemoryStream? _audioStream;

        private readonly System.Timers.Timer _trackTimer;
        private readonly HttpClient _httpClient;
        private readonly ProgramSettings _settings;
        private readonly ILogger _logger;
        private CancellationTokenSource? _playPauseTokenSource;
        private CancellationTokenSource? _trackTokenSource;

        public event Action<Track>? TrackChanging;

        public event Action<Track>? TrackChanged;

        public event Action<double>? TrackTimeUpdated;

        public event Action<PlayState>? PlayStateChanged;

        public event Action<string>? LoadingStatusChanged;

        public volatile bool _switchingTracks;

        private volatile bool _playing;

        private HttpClient? _proxyClient;
        private Func<Task<IEnumerable<Track>>>? _continueDownloader;
        private CancellationTokenSource? _proxyCheckTokenSource;

        public MusicPlayer(HttpClient httpClient, ProgramSettings settings, ILogger logger)
        {
            _httpClient = httpClient;
            _settings = settings;
            _logger = logger;
            _trackTimer = new(TimeSpan.FromMilliseconds(250));
            _trackTimer.Elapsed += (s, e) => UpdateTrackTime();
            InterceptKeys.PlayPausedTriggered += TryPlayPause;
            InterceptKeys.PrevTrackTriggered += () => Task.Run(async () => await PlayPrev());
            InterceptKeys.NextTrackTriggered += () => Task.Run(async () => await PlayNext());

            //if (settings.LastTrack != null)
            //    Task.Run(() => ChangeTrack(settings.LastTrack, false));
        }

        ~MusicPlayer()
        {
            _waveOut?.Dispose();
            _waveStream?.Dispose();
            _audioStream?.Dispose();
            _trackTimer?.Dispose();
            _proxyClient?.Dispose();
            _playPauseTokenSource?.Dispose();
            _proxyCheckTokenSource?.Dispose();
            _trackTokenSource?.Dispose();
        }

        public void SetPosition(double value)
        {
            Debug.Print($"setting position to {value}, waveStream is set: {_waveStream is not null}");
            if (!_switchingTracks && _waveStream is not null)
                _waveStream.CurrentTime = TimeSpan.FromMilliseconds(value);
        }

        public async Task PlayNewQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null)
        {
            tracks = FilterTracks(tracks);

            TracksPlaylist.Clear();
            TracksPlaylist.AddRange(tracks);

            if (downloadMore != null)
                _continueDownloader = downloadMore;
            else if (_settings.AutoplayStationOnLastTrack)
                _continueDownloader = GetAutoplay;

            if (TracksPlaylist.TryGetCurrent(out var track) && await LoadTrack(track))
                StartPlayback();
        }

        public async Task AddToQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool preloadTrack = false)
        {
            tracks = FilterTracks(tracks);
            if (!tracks.Any())
            {
                _logger.Warn("Tried adding tracks to queue, but it ended up being empty after filtering");
                return;
            }

            TracksPlaylist.AddRange(tracks);

            if (downloadMore != null)
                _continueDownloader = downloadMore;
            else if (_settings.AutoplayStationOnLastTrack)
                _continueDownloader = GetAutoplay;

            if (preloadTrack && _waveOut == null && TracksPlaylist.TryGetCurrent(out var track))
                await LoadTrack(track);
        }

        public async Task AddToQueue(Track track, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool preloadTrack = false) =>
            await AddToQueue([track], downloadMore, preloadTrack);

        private IEnumerable<Track> FilterTracks(IEnumerable<Track> tracks) =>
            tracks.Where(x => !_settings.IsBlockedUser(x) || !_settings.IsBlockedTrack(x));

        public void ClearQueue()
        {
            if (TracksPlaylist.Count == 0)
                return;

            TracksPlaylist.Clear();
            if (CurrentTrack != null)
                TracksPlaylist.Add(CurrentTrack);

            _continueDownloader = null;
            if (_settings.AutoplayStationOnLastTrack)
                _continueDownloader = GetAutoplay;
        }

        public async Task SkipTrack(int id)
        {
            TracksPlaylist.RemoveAll(x => x.Id == id);
            bool wasPlaying = _playing;
            if (TracksPlaylist.TryGetCurrent(out var track) && await LoadTrack(track) && wasPlaying)
                StartPlayback();
        }

        public async Task SkipUser(int id)
        {
            TracksPlaylist.RemoveAll(x => x.User!.Id == id);
            bool wasPlaying = _playing;
            if (TracksPlaylist.TryGetCurrent(out var track) && await LoadTrack(track) && wasPlaying)
                StartPlayback();
        }

        /// <summary>
        /// When the playlist reaches the end, it'll try to dowload the "related" tracks instead
        /// </summary>
        public void ContinueWithAutoplay()
        {
            _continueDownloader = GetAutoplay;
        }

        private async Task<IEnumerable<Track>> GetAutoplay()
        {
            var track = TracksPlaylist.GetLastTrack();
            if (track == null)
                return [];

            return await GetRelatedTracks(track);
        }

        private async Task<bool> LoadTrack(Track track)
        {
            _switchingTracks = true;
            EndPlayback();
            _logger.Info($"loading new track: {track.FullLabel}");
            TrackChanging?.Invoke(track);
            LoadingStatusChanged?.Invoke("Loading...");

            _trackTokenSource?.Cancel();
            _trackTokenSource = new();
            var token = _trackTokenSource.Token;

            try
            {
                while (_waveOut is not null || _waveStream is not null)
                    await Task.Delay(100, token);

                CurrentTrack = track;
                if (System.IO.File.Exists(track.LocalFilePath))
                {
                    LoadingStatusChanged?.Invoke("Loading from file...");
                    _waveStream = new Mp3FileReader(track.LocalFilePath);
                }
                else if (track.Policy == "BLOCK")
                {
                    _logger.Info($"track is straight up blocked, idk what to do about it, maybe get its info from proxy?");
                    LoadingStatusChanged?.Invoke("Access blocked");
                    PlayStateChanged?.Invoke(PlayState.Error);
                    return false;
                }
                else if (track.Policy == "SNIP")
                {
                    LoadingStatusChanged?.Invoke("Region-blocked music, searching proxy...");
                    _logger.Info($"Region-blocked music, searching for proxy...");
                    bool success = await DownloadTrackFromProxy(track, token);
                    if (!success)
                    {
                        LoadingStatusChanged?.Invoke("Region-blocked - no valid proxies available");
                        PlayStateChanged?.Invoke(PlayState.Error);
                        return false;
                    }

                    _waveStream = new Mp3FileReader(track.LocalFilePath);
                }
                else if (track.Policy == "ALLOW")
                {
                    LoadingStatusChanged?.Invoke("Downloading stream...");
                    _waveStream = await GetWaveStreamFromChunks(track, _httpClient, token);
                }

                if (_waveStream is null || token.IsCancellationRequested)
                    return false;

                //SaveTrackToFile(track, waveStream);

                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                _waveOut.Init(_waveStream);
                _waveOut.Volume = DesiredVolume;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"http request exception while changing track: {ex.Message}");
                PlayStateChanged?.Invoke(PlayState.Error);
                LoadingStatusChanged?.Invoke($"Error while requesting track");
                return false;
            }
            catch (TaskCanceledException)
            {
                _logger.Info($"changing track to {track.FullLabel} has been cancelled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"unhandled exception while changing track: {ex.Message}");
                PlayStateChanged?.Invoke(PlayState.Error);
                return false;
            }
            finally
            {
                _switchingTracks = false;
            }

            if (_waveStream == null)
                return false;

            PlayStateChanged?.Invoke(PlayState.Loaded);
            _logger.Info($"track loaded");
            Debug.Print("finished loading...");
            TrackChanged?.Invoke(track);
            return true;
        }

        private async Task<WaveStream?> GetWaveStreamFromChunks(Track track, HttpClient httpClient, CancellationToken token)
        {
            //grab the link to the audio stream from metadata
            var audioChunksLinks = await GetAudioChunksLinks(track, httpClient, token);
            if (audioChunksLinks is null)
            {
                _logger.Info("no links returned - skipping");
                return null;
            }

            //download streams
            LoadingStatusChanged?.Invoke($"Downloading stream: 0/{audioChunksLinks.Count}");
            _audioStream = new();
            int chunkCount = 0;
            foreach (var link in audioChunksLinks)
            {
                using var r = await httpClient.GetAsync(link, token);
                r.EnsureSuccessStatusCode();

                Stream s = await r.Content.ReadAsStreamAsync(token);
                await s.CopyToAsync(_audioStream, token);
                LoadingStatusChanged?.Invoke($"Downloading stream: {++chunkCount}/{audioChunksLinks.Count}");
            }
            LoadingStatusChanged?.Invoke("");

            _audioStream.Position = 0;

            return WaveFormatConversionStream.CreatePcmStream(new Mp3FileReader(_audioStream));
        }

        private async Task<List<string>?> GetAudioChunksLinks(Track track, HttpClient client, CancellationToken token)
        {
            var transcoding = track.Media.Transcodings.Find(x => x.Format.MimeType == "audio/mpeg" && x.Format.Protocol == "hls");
            if (transcoding == null)
                return null;

            transcoding.Url = transcoding.Url.Replace("/preview/", "/stream/");

            //get track stream url
            UrlHolder? playbackStream = null;
            try
            {
                using var playbackStreamRequest = await client.GetAsync($"{transcoding.Url}?client_id={_settings.ClientId}&track_authorization={track.TrackAuthorization}", token);
                playbackStreamRequest.EnsureSuccessStatusCode();

                playbackStream = await playbackStreamRequest.Content.ReadFromJsonAsync<UrlHolder>(token);
            }
            catch (HttpRequestException e)
            {
                _logger.Error($"Failed getting playback stream url {track.Title}, return code: {e.StatusCode}: {e.Message}");
                throw;
            }

            //get the list of streams
            string chunks;
            try
            {
                using var chunksRequest = await client.GetAsync(playbackStream.Url, token);
                chunks = await chunksRequest.Content.ReadAsStringAsync(token);
            }
            catch (HttpRequestException e)
            {
                _logger.Error($"exception while getting chunks list for {track.Title}: {e.Message}");
                throw;
            }

            List<string> chunksList = chunks.Split('\n').Where(x => !x.StartsWith('#')).ToList();
            return chunksList;
        }

        private async Task<List<Track>> GetRelatedTracks(Track track)
        {
            TrackCollection? tracks;
            try
            {
                using var response = await _httpClient.GetAsync($"tracks/{track.Id}/related?user_id={_settings.UserId}&client_id={_settings.ClientId}&limit=50&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en");
                response.EnsureSuccessStatusCode();
                tracks = await response.Content.ReadFromJsonAsync<TrackCollection>();
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Failed retrieving related tracks for {track.Title}: {ex.Message}");
                throw;
            }

            if (tracks == null)
                return [];

            return tracks.Collection;
        }

        private void TryPlayPause()
        {
            _playPauseTokenSource?.Cancel();
            _playPauseTokenSource = new();

            Task.Run(async () =>
            {
                await PlayPause(_playPauseTokenSource.Token);
            }, _playPauseTokenSource.Token);
        }

        public async Task PlayPause(CancellationToken token)
        {
            if (_waveOut is null)
                return;

            if (!_playing)
            {
                _playing = true;
                PlayStateChanged?.Invoke(PlayState.Playing);
                await FadeInOut(DesiredVolume, token);
                _trackTimer.Start();
            }
            else
            {
                _playing = false;
                PlayStateChanged?.Invoke(PlayState.Paused);
                await FadeInOut(0, token);
                _trackTimer.Stop();
            }
        }

        private async Task FadeInOut(float changeVolumeTo, CancellationToken token)
        {
            if (_waveOut is null)
                return;

            int stepDelay = 20; // in ms
            int fadeDuration = 150; // in ms
            float steps = fadeDuration / stepDelay;
            float volumeChange = DesiredVolume / steps;

            if (changeVolumeTo == 0)
            {
                while (true)
                {
                    float newVolume = _waveOut.Volume - volumeChange;
                    if (newVolume < 0)
                        newVolume = 0;

                    _waveOut.Volume = newVolume;

                    if (_waveOut.Volume <= 0)
                    {
                        _waveOut.Pause();
                        return;
                    }

                    await Task.Delay(stepDelay, token);
                }
            }

            _waveOut.Play();
            while (true)
            {
                float newVolume = _waveOut.Volume + volumeChange;
                if (newVolume > 1)
                    newVolume = 1;

                _waveOut.Volume = newVolume;

                if (_waveOut.Volume >= changeVolumeTo)
                {
                    _waveOut.Volume = changeVolumeTo;
                    return;
                }

                await Task.Delay(stepDelay, token);
            }
        }

        private void UpdateTrackTime()
        {
            if (_waveStream is null)
                return;

            TrackTimeUpdated?.Invoke(_waveStream.CurrentTime.TotalMilliseconds);
        }

        public async Task PlayNext()
        {
            if (TracksPlaylist.Count == 0)
                return;

            _switchingTracks = true;

            if (TracksPlaylist.TryMoveForward(out var nextTrack) && await LoadTrack(nextTrack))
            {
                StartPlayback();
            }
            else if (_continueDownloader is not null)
            {
                EndPlayback();
                IEnumerable<Track> newQueue;
                try
                {
                    newQueue = await _continueDownloader();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed retrieving next track: {ex.Message}");
                    LoadingStatusChanged?.Invoke("Failed getting next track");
                    PlayStateChanged?.Invoke(PlayState.Error);
                    _switchingTracks = false;
                    return;
                }

                await AddToQueue(newQueue, _continueDownloader, false);

                if (TracksPlaylist.TryMoveForward(out var track) && await LoadTrack(track))
                    StartPlayback();

                _switchingTracks = false;
            }
        }

        public async Task PlayPrev()
        {
            if (_waveStream == null)
                return;

            _switchingTracks = true;

            //if played more than 5 seconds - only rewind to the beginning instead
            if (_waveStream.CurrentTime > TimeSpan.FromSeconds(5))
                _waveStream.Position = 0;
            else if (TracksPlaylist.TryMoveBack(out var previousTrack) && await LoadTrack(previousTrack))
                StartPlayback();

            _switchingTracks = false;
        }

        private void StartPlayback()
        {
            if (_waveOut == null)
                return;

            _waveOut.Play();
            _trackTimer.Start();
            _playing = true;
            PlayStateChanged?.Invoke(PlayState.Playing);
        }

        private void EndPlayback()
        {
            if (_waveOut is not null)
            {
                if (_waveOut.PlaybackState != PlaybackState.Stopped)
                    _waveOut.Stop();
                else
                    DisposeWave();
            }

            _playing = false;
        }

        public async Task SaveTrackLocally(Track track)
        {
            using var tempToken = new CancellationTokenSource();
            var stream = await GetWaveStreamFromChunks(track, _httpClient, tempToken.Token);
            if (stream == null)
            {
                _logger.Error($"Failed retrieving the track stream for: {track.FullLabel}");
                return;
            }

            await SaveTrackToFile(track, stream);
        }

        private async Task<bool> DownloadTrackFromProxy(Track track, CancellationToken token)
        {
            if (_proxyClient == null)
            {
                //todo have settings that the user can change, primarily country and possibly timeout
                string addressesString;
                try
                {
                    using var proxyRequest = await _httpClient.GetAsync("https://api.proxyscrape.com/v4/free-proxy-list/get?request=display_proxies&country=us&protocol=http&proxy_format=protocolipport&format=text&timeout=5000", token);
                    proxyRequest.EnsureSuccessStatusCode();
                    addressesString = await proxyRequest.Content.ReadAsStringAsync(token);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error($"Failed request to get proxies: {ex.Message}");
                    return false;
                }

                string[] addresses = addressesString.Split("\r\n");
                if (addresses.Length == 0)
                {
                    _logger.Warn("No valid proxies available");
                    return false;
                }
                else
                {
                    _logger.Warn($"Got {addresses.Length} proxies available.");
                }

                string? address = await FindWorkingProxy(addresses);

                if (token.IsCancellationRequested)
                    return false;

                if (address == null)
                {
                    _logger.Warn("No valid proxy found.");
                    return false;
                }

                var proxy = new WebProxy() { Address = new Uri(address) };
                var handler = new HttpClientHandler() { Proxy = proxy, AutomaticDecompression = DecompressionMethods.All };
                _proxyClient = new HttpClient(handler) { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) };
            }

            using var stream = await GetWaveStreamFromChunks(track, _proxyClient, token);
            if (stream is null)
                return false;

            await SaveTrackToFile(track, stream);

            return true;
        }

        private async Task<string?> FindWorkingProxy(string[] addresses)
        {
            _proxyCheckTokenSource?.Cancel();
            _proxyCheckTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string? workingAddress = null;

            try
            {
                await Parallel.ForEachAsync(addresses, _proxyCheckTokenSource.Token, async (address, token) =>
                {
                    try
                    {
                        var proxy = new WebProxy(address);
                        var handler = new HttpClientHandler() { Proxy = proxy };
                        using var client = new HttpClient(handler);

                        using var request = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://soundcloud.com/"), token);
                        request.EnsureSuccessStatusCode();

                        _logger.Info($"found working proxy: {address}");
                        workingAddress = address;

                        _proxyCheckTokenSource.Cancel();
                    }
                    catch (HttpRequestException e)
                    {
                        Debug.Print($"pinging {address} threw exception: {e.Message}");
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Debug.Print($"proxy pinging cancelled");
            }

            _logger.Info($"found working proxy: {workingAddress}");
            return workingAddress;
        }

        private async Task SaveTrackToFile(Track track, WaveStream stream)
        {
            Directory.CreateDirectory(Globals.LocalDownloadsPath);

            MediaFoundationEncoder.EncodeToMp3(stream, track.LocalFilePath);
            stream.Position = 0;

            using var tfile = TagLib.File.Create(track.LocalFilePath);
            tfile.Tag.Title = track.Title;
            tfile.Tag.AlbumArtists = [track.ArtistName];
            if (!string.IsNullOrEmpty(track.Genre))
                tfile.Tag.Genres = [track.Genre];
            if (track.ReleaseDate.HasValue)
                tfile.Tag.Year = (uint)track.ReleaseDate.Value.Year;
            if (track.PublisherMetadata is not null)
            {
                tfile.Tag.Album = track.PublisherMetadata.AlbumTitle;
                tfile.Tag.Publisher = track.PublisherMetadata.Publisher;
                tfile.Tag.Composers = [track.PublisherMetadata.WriterComposer];
            }

            var pic = await _httpClient.GetAsync(track.ArtworkUrlOriginal);
            if (pic.IsSuccessStatusCode)
            {
                var vect = ByteVector.FromStream(await pic.Content.ReadAsStreamAsync());
                tfile.Tag.Pictures = [new Picture(vect)];
            }

            tfile.Save();

            using var writeStream = System.IO.File.OpenWrite($"{Globals.LocalDownloadsPath}/{track.FullLabel}.json");
            await JsonSerializer.SerializeAsync(writeStream, track);
        }

        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            DisposeWave();
            PlayStateChanged?.Invoke(PlayState.Ended);

            if (!_switchingTracks && TracksPlaylist.Count > 0)
                Task.Run(PlayNext);
        }

        private void DisposeWave()
        {
            _trackTimer.Stop();
            _waveOut?.Dispose();
            _waveStream?.Dispose();
            _audioStream?.Dispose();
            _waveOut = null;
            _waveStream = null;
        }

        public void ShufflePlaylist(bool value)
        {
            TracksPlaylist.ChangeShuffle(value);
        }

        private class UrlHolder
        {
            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
        }
    }
}