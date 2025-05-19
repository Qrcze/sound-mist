using Avalonia.Controls.Notifications;
using SoundMist.Helpers;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace SoundMist.Models.Audio
{
    public class MusicPlayer : IMusicPlayer
    {
        public TracksPlaylist TracksPlaylist { get; } = new();

        public Track? CurrentTrack
        {
            get
            {
                TracksPlaylist.TryGetCurrent(out var track);
                return track;
            }
        }

        public float DesiredVolume
        {
            get => _settings.Volume;
            set
            {
                _settings.Volume = value;
                _audioController.Volume = value;
            }
        }

        double Volume { get => _audioController.Volume; set => _audioController.Volume = value; }

        public bool PlayerReady => _audioController.ChannelInitialized;

        public bool IsPlaying => _audioController.IsPlaying;

        public event Action<PlayState, string>? PlayStateUpdated;

        public event Action<string>? ErrorCallback;

        public event Action<Track>? TrackChanging;

        public event Action<Track>? TrackChanged;

        public event Action<double>? TrackTimeUpdated;

        private readonly HttpManager _httpManager;
        private readonly ProgramSettings _settings;
        private readonly IAudioController _audioController;
        private Func<Task<IEnumerable<Track>>>? _continueDownloader;
        private CancellationTokenSource? _loadTrackTokenSource;
        private CancellationTokenSource? _playPauseTokenSource;
        private readonly System.Timers.Timer _timeUpdateTimer;

        public MusicPlayer(HttpManager httpManager, ProgramSettings settings, IAudioController audioController, ILogger logger)
        {
            _httpManager = httpManager;
            _settings = settings;
            _audioController = audioController;
            _audioController.OnTrackEnded += () => Task.Run(PlayNext);

            _timeUpdateTimer = new(250);
            _timeUpdateTimer.Elapsed += _timeUpdateTimer_Elapsed;

            ErrorCallback += m =>
            {
                NotificationManager.Show(new Notification("Player error", m, NotificationType.Error, TimeSpan.Zero));
                logger.Error(m);
            };
        }

        public void SetPosition(double value)
        {
            _audioController.TimeInSeconds = value / 1000;
        }

        private void _timeUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (TrackTimeUpdated is null || !IsPlaying)
                return;

            TrackTimeUpdated.Invoke(_audioController.TimeInSeconds * 1000);
        }

        public async Task AddToQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null)
        {
            var filteredTracks = FilterTracks(tracks);
            if (!filteredTracks.Any())
                return;

            TracksPlaylist.AddRange(filteredTracks);

            _continueDownloader = downloadMore ?? _continueDownloader;

            await StartLoadingIfNothingLoaded(downloadMore);
        }

        public async Task AddToQueue(Track track, Func<Task<IEnumerable<Track>>>? downloadMore = null)
        {
            TracksPlaylist.Add(track);

            await StartLoadingIfNothingLoaded(downloadMore);

            _continueDownloader = downloadMore ?? _continueDownloader;

            if (_settings.Shuffle)
                TracksPlaylist.ChangeShuffle(true);
        }

        private async Task StartLoadingIfNothingLoaded(Func<Task<IEnumerable<Track>>>? downloadMore = null)
        {
            if (PlayerReady)
                return;

            TracksPlaylist.TryGetCurrent(out var track);

            _loadTrackTokenSource?.Cancel();
            _loadTrackTokenSource = new();
            try
            {
                if (await LoadTrack(track, _loadTrackTokenSource.Token))
                    StartPlaying();
            }
            catch (TaskCanceledException)
            { }
        }

        public async Task LoadNewQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool startPlaying = true)
        {
            var filteredTracks = FilterTracks(tracks);
            if (!filteredTracks.Any())
                return;

            TracksPlaylist.Clear();
            TracksPlaylist.AddRange(filteredTracks);
            _continueDownloader = downloadMore;

            TracksPlaylist.TryGetCurrent(out var currentTrack);

            if (_settings.Shuffle)
                TracksPlaylist.ChangeShuffle(true);

            _loadTrackTokenSource?.Cancel();
            _loadTrackTokenSource = new();
            try
            {
                if (await LoadTrack(currentTrack, _loadTrackTokenSource.Token) && startPlaying)
                    StartPlaying();
            }
            catch (TaskCanceledException)
            { }
        }

        public async Task PlayNext()
        {
            _loadTrackTokenSource?.Cancel();
            _loadTrackTokenSource = new();

            if (TracksPlaylist.TryMoveForward(out var track))
            {
                try
                {
                    if (await LoadTrack(track, _loadTrackTokenSource.Token))
                        StartPlaying();
                }
                catch (TaskCanceledException)
                { }
            }
            else
            {
                _continueDownloader ??= GetAutoplay;

                var nextTracks = await _continueDownloader.Invoke();
                await AddToQueue(nextTracks);
                try
                {
                    if (TracksPlaylist.TryMoveForward(out var nextTrack) && await LoadTrack(nextTrack, _loadTrackTokenSource.Token))
                        StartPlaying();
                }
                catch (TaskCanceledException)
                { }
            }
        }

        public async Task PlayPrev()
        {
            if (!PlayerReady)
                return;

            if (_audioController.TimeInSeconds > 5)
            {
                _loadTrackTokenSource?.Cancel();
                _loadTrackTokenSource = new();

                _audioController.TimeInSeconds = 0;
            }
            else if (TracksPlaylist.TryMoveBack(out var track))
            {
                _loadTrackTokenSource?.Cancel();
                _loadTrackTokenSource = new();
                try
                {
                    if (await LoadTrack(track, _loadTrackTokenSource.Token))
                        StartPlaying();
                }
                catch (TaskCanceledException)
                { }
            }
        }

        /// <summary>
        /// Stops and clears current music, then downloads and loads next music channel with given track. Does not start playback.
        /// </summary>
        /// <param name="track"></param>
        /// <returns> true if loaded successfully, false if fail </returns>
        /// <exception cref="TaskCanceledException" />
        private async Task<bool> LoadTrack(Track track, CancellationToken token)
        {
            _audioController.Stop();

            TrackChanging?.Invoke(track);
            PlayStateUpdated?.Invoke(PlayState.Loading, "Loading track...");

            if (File.Exists(track.LocalFilePath))
            {
                PlayStateUpdated?.Invoke(PlayState.Loading, "Loading track...");
                _audioController.LoadFromFile(track.LocalFilePath);
            }
            else if (track.Policy == "BLOCK")
            {
                if (_settings.ProxyMode != ViewModels.ProxyMode.BypassOnly)
                {
                    PlayStateUpdated?.Invoke(PlayState.Error, "Track is region-blocked.");
                    return false;
                }

                var trackProx = (await SoundCloudQueries.GetTracksById(_httpManager.GetProxiedClient(), _settings.ClientId,
                    _settings.AppVersion, [track.Id])).Single();

                if (trackProx is null || trackProx.Policy == "BLOCK")
                {
                    PlayStateUpdated?.Invoke(PlayState.Error, "Track is region-blocked.");
                    return false;
                }

                await InitBuffered(_httpManager.GetProxiedClient(), trackProx, token);
            }
            else if (track.Policy == "SNIP")
            {
                if (_settings.ProxyMode != ViewModels.ProxyMode.BypassOnly)
                {
                    PlayStateUpdated?.Invoke(PlayState.Error, "Track is region-blocked.");
                    return false;
                }

                await InitBuffered(_httpManager.GetProxiedClient(), track, token);
            }
            else if (track.Policy == "ALLOW" || track.Policy == "MONETIZE")
            {
                await InitBuffered(_httpManager.DefaultClient, track, token);
            }
            else
            {
                PlayStateUpdated?.Invoke(PlayState.Error, $"Unhandled track policy: {track.Policy}, cancelling.");
                return false;
            }

            if (!PlayerReady)
            {
                PlayStateUpdated?.Invoke(PlayState.Error, $"Sound library failed loading track");
                return false;
            }

            Volume = DesiredVolume;
            PlayStateUpdated?.Invoke(PlayState.Loaded, string.Empty);
            TrackChanged?.Invoke(track);
            _settings.LastTrackId = track.Id;
            return true;
        }

        private async Task<bool> InitBuffered(HttpClient httpClient, Track track, CancellationToken token)
        {
            (var links, string error) = await SoundCloudDownloader.GetTrackLinks(httpClient, track, _settings.ClientId!, token);
            if (links is null)
            {
                ErrorCallback?.Invoke(error);
                PlayStateUpdated?.Invoke(PlayState.Error, error);
                return false;
            }

            int initialChunks = Math.Min(3, links.Count);

            var initialBytes = new List<byte[]>(initialChunks);
            for (int i = 0; i < initialChunks; i++)
            {
                byte[] bytes = await SoundCloudDownloader.DownloadTrackChunk(httpClient, links[i], token);
                initialBytes.Add(bytes);
                PlayStateUpdated?.Invoke(PlayState.Loading, $"{i + 1}/{links.Count}");
            }

            try
            {
                _audioController.InitBufferedChannel(initialBytes.SelectMany(x => x).ToArray(), track.Duration);
            }
            catch (AudioControllerException ex)
            {
                string err = $"Failed initializing buffered channel: {ex.Message}";
                ErrorCallback?.Invoke(err);
                PlayStateUpdated?.Invoke(PlayState.Error, err);
                return false;
            }

            RunDownloaderTask(httpClient, links, initialChunks, token);

            return true;
        }

        void RunDownloaderTask(HttpClient httpClient, List<string> links, int startingIndex, CancellationToken token)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    for (int i = startingIndex; i < links.Count; i++)
                    {
                        byte[] bytes = await SoundCloudDownloader.DownloadTrackChunk(httpClient, links[i], token);
                        _audioController.AppendBytes(bytes);
                        PlayStateUpdated?.Invoke(PlayState.Loading, $"{i + 1}/{links.Count}");
                    }
                    _audioController.StreamCompleted();
                    PlayStateUpdated?.Invoke(PlayState.Loaded, string.Empty);
                }
                catch (TaskCanceledException ex)
                {
                }
            }, token);
        }

        private void StartPlaying()
        {
            if (!PlayerReady)
                return;

            _audioController.Play();
            _timeUpdateTimer.Start();
            PlayStateUpdated?.Invoke(PlayState.Playing, string.Empty);
        }

        private IEnumerable<Track> FilterTracks(IEnumerable<Track> tracks) =>
            tracks.Where(x => !_settings.IsBlockedTrack(x) && !_settings.IsBlockedUser(x));

        public void ClearQueue()
        {
            TracksPlaylist.TryGetCurrent(out var currentTrack);
            TracksPlaylist.Clear();
            TracksPlaylist.Add(currentTrack);
            ContinueWithAutoplay();
        }

        public void ContinueWithAutoplay()
        {
            _continueDownloader = GetAutoplay;
        }

        private async Task<IEnumerable<Track>> GetAutoplay()
        {
            var track = TracksPlaylist.GetLastTrack();
            if (track == null)
                return [];

            (var tracks, string error) = await SoundCloudDownloader.GetRelatedTracks(_httpManager.DefaultClient, track, _settings.UserId.Value, _settings.ClientId, _settings.AppVersion);
            if (tracks is null)
            {
                ErrorCallback?.Invoke(error);
                return [];
            }

            tracks.Collection.ForEach(track => track.FromAutoplay = true);
            return tracks.Collection;
        }

        public void Stop()
        {
            _playPauseTokenSource?.Cancel();
            _playPauseTokenSource = null;

            _audioController.Pause();
            _timeUpdateTimer.Stop();
            SetPosition(0);

            PlayStateUpdated?.Invoke(PlayState.Paused, string.Empty);
            TrackTimeUpdated?.Invoke(0);
        }

        public void PlayPause()
        {
            _playPauseTokenSource?.Cancel();
            _playPauseTokenSource = new();
            Task.Run(() => HandlePlayPause(_playPauseTokenSource.Token), _playPauseTokenSource.Token);
        }

        public void Play()
        {
            if (IsPlaying)
                return;
            PlayPause();
        }

        public void Pause()
        {
            if (!IsPlaying)
                return;
            PlayPause();
        }

        public async Task HandlePlayPause(CancellationToken token)
        {
            if (!PlayerReady)
                return;

            if (IsPlaying)
            {
                PlayStateUpdated?.Invoke(PlayState.Paused, string.Empty);
                await FadeOut(token);
                _timeUpdateTimer.Stop();
            }
            else
            {
                PlayStateUpdated?.Invoke(PlayState.Playing, string.Empty);
                await FadeIn(token);
                _timeUpdateTimer.Start();
            }
        }

        private async Task FadeOut(CancellationToken token)
        {
            int stepDelay = 20; // in ms
            double fadeDuration = 150; // in ms
            double steps = fadeDuration / stepDelay;
            double volumeChange = DesiredVolume / steps;

            while (Volume > 0)
            {
                if (token.IsCancellationRequested)
                    return;
                Volume -= volumeChange;
                await Task.Delay(stepDelay);
            }

            _audioController.Pause();
        }

        private async Task FadeIn(CancellationToken token)
        {
            int stepDelay = 20; // in ms
            double fadeDuration = 150; // in ms
            double steps = fadeDuration / stepDelay;
            double volumeChange = DesiredVolume / steps;

            _audioController.Play();
            while (Volume < DesiredVolume)
            {
                if (token.IsCancellationRequested)
                    return;
                Volume += volumeChange;
                await Task.Delay(stepDelay);
            }
        }

        public async Task ReloadCurrentTrack()
        {
            var track = CurrentTrack;
            if (track is null)
                return;

            _loadTrackTokenSource?.Cancel();
            _loadTrackTokenSource = new();

            try
            {
                if (await LoadTrack(track, _loadTrackTokenSource.Token))
                    StartPlaying();
            }
            catch (TaskCanceledException)
            { }
        }
    }
}