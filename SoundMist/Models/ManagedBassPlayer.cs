using Avalonia.Controls.Notifications;
using ManagedBass;
using SoundMist.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace SoundMist.Models
{
    public class ManagedBassPlayer : IMusicPlayer
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

                ChangeBassVolume(value);
            }
        }

        public double BassVolume
        {
            get
            {
                return _musicChannel != 0 ? Bass.ChannelGetAttribute(_musicChannel, ChannelAttribute.Volume) : 0;
            }
            set
            {
                if (_musicChannel != 0)
                    ChangeBassVolume(value);
            }
        }

        public bool PlayerReady => _musicChannel != 0;

        public bool Playing => _playing;

        private volatile int _musicChannel;
        private volatile bool _playing;

        public event Action<PlayState, string>? PlayStateUpdated;

        public event Action<string>? ErrorCallback;

        public event Action<Track>? TrackChanging;

        public event Action<Track>? TrackChanged;

        public event Action<double>? TrackTimeUpdated;

        private readonly HttpClient _httpClient;
        private readonly ProgramSettings _settings;
        private Func<Task<IEnumerable<Track>>>? _continueDownloader;
        private CancellationTokenSource? _loadTrackTokenSource;
        private CancellationTokenSource? _playPauseTokenSource;
        private readonly System.Timers.Timer _timeUpdateTimer;
        private double _positionInSeconds;

        public ManagedBassPlayer(HttpClient httpClient, ProgramSettings settings, ILogger logger)
        {
            _httpClient = httpClient;
            _settings = settings;

            _timeUpdateTimer = new(250);
            _timeUpdateTimer.Elapsed += _timeUpdateTimer_Elapsed;

            ErrorCallback += m =>
            {
                NotificationManager.Show(new Notification("Player error", m, NotificationType.Error, TimeSpan.Zero));
                logger.Error(m);
            };

            InterceptKeys.PlayPausedTriggered += PlayPauseTriggered;
            InterceptKeys.PrevTrackTriggered += () => Task.Run(async () => await PlayPrev());
            InterceptKeys.NextTrackTriggered += () => Task.Run(async () => await PlayNext());

            Bass.Init();
        }

        private void ChangeBassVolume(double value)
        {
            if (_musicChannel != 0)
                Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, Math.Clamp(value, 0, 1));
        }

        public void SetPosition(double value)
        {
            if (_musicChannel == 0)
                return;

            long bytePosition = Bass.ChannelSeconds2Bytes(_musicChannel, value / 1000);
            Bass.ChannelSetPosition(_musicChannel, bytePosition);
        }

        private void _timeUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_musicChannel == 0 || TrackTimeUpdated is null)
                return;

            long pos = Bass.ChannelGetPosition(_musicChannel);
            _positionInSeconds = Bass.ChannelBytes2Seconds(_musicChannel, pos);
            TrackTimeUpdated.Invoke(_positionInSeconds * 1000);
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
            if (_musicChannel != 0)
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
            if (_musicChannel == 0)
                return;

            _loadTrackTokenSource?.Cancel();
            _loadTrackTokenSource = new();

            if (_positionInSeconds > 5)
            {
                Bass.ChannelSetPosition(_musicChannel, 0);
            }
            else if (TracksPlaylist.TryMoveBack(out var track))
            {
                try
                {
                    if (await LoadTrack(track, _loadTrackTokenSource.Token))
                        StartPlaying();
                }
                catch (TaskCanceledException)
                { }
            }
        }

        private async Task<bool> LoadTrack(Track track, CancellationToken token)
        {
            _playing = false;

            TrackChanging?.Invoke(track);
            PlayStateUpdated?.Invoke(PlayState.Loading, "Loading track...");

            Bass.ChannelStop(_musicChannel);
            Bass.StreamFree(_musicChannel);

            if (File.Exists(track.LocalFilePath))
            {
                PlayStateUpdated?.Invoke(PlayState.Loading, "Loading track...");
                _musicChannel = Bass.CreateStream(track.LocalFilePath, 0, 0, BassFlags.Default);
            }
            else if (track.Policy == "BLOCK")
            {
                PlayStateUpdated?.Invoke(PlayState.Error, "Track is region-blocked, no workaround implemented yet.");
                return false;
            }

            //else if (track.Policy == "SNIP")
            //{
            //    var links = await ScDownloader.GetTrackLinks(_httpClient, track, _settings.ClientId!, token);

            //}
            else if (track.Policy == "ALLOW")
            {
                byte[]? data = await FullyDownloadTrack(track, token);
                if (data is null)
                    return false;

                _musicChannel = Bass.CreateStream(data, 0, data.Length, BassFlags.Default);
            }
            else
            {
                PlayStateUpdated?.Invoke(PlayState.Error, $"Unhandled track policy: {track.Policy}, cancelling.");
                return false;
            }

            if (_musicChannel == 0)
            {
                PlayStateUpdated?.Invoke(PlayState.Error, $"Sound library failed loading track: {Bass.LastError}");
                return false;
            }
            Bass.ChannelSetSync(_musicChannel, SyncFlags.End, 0, TrackEnded);

            PlayStateUpdated?.Invoke(PlayState.Loaded, string.Empty);
            TrackChanged?.Invoke(track);
            _settings.LastTrackId = track.Id;
            return true;
        }

        void TrackEnded(int handle, int channel, int data, nint user)
        {
            Bass.StreamFree(channel);
            Task.Run(PlayNext);
        }

        private async Task<byte[]?> FullyDownloadTrack(Track track, CancellationToken token)
        {
            (var links, string error) = await SoundCloudDownloader.GetTrackLinks(_httpClient, track, _settings.ClientId!, token);
            if (links is null)
            {
                PlayStateUpdated?.Invoke(PlayState.Error, error);
                return null;
            }

            void statusCallback(string message) => PlayStateUpdated?.Invoke(PlayState.Loading, message);

            (byte[]? data, error) = await SoundCloudDownloader.DownloadTrackData(_httpClient, links, statusCallback, token);
            if (data is null)
            {
                PlayStateUpdated?.Invoke(PlayState.Error, error);
                return null;
            }

            return data;
        }

        private void StartPlaying()
        {
            if (_musicChannel == 0)
                return;

            _playing = true;
            _timeUpdateTimer.Start();
            Bass.ChannelPlay(_musicChannel);
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

            (var tracks, string error) = await SoundCloudDownloader.GetRelatedTracks(_httpClient, track, _settings.UserId.Value, _settings.ClientId, _settings.AppVersion);
            if (tracks is null)
            {
                ErrorCallback?.Invoke(error);
                return [];
            }

            return tracks;
        }


        private void PlayPauseTriggered()
        {
            _playPauseTokenSource?.Cancel();
            _playPauseTokenSource = new();
            Task.Run(() => PlayPause(_playPauseTokenSource.Token), _playPauseTokenSource.Token);
        }

        public async Task PlayPause(CancellationToken token)
        {
            if (_musicChannel == 0)
                return;

            if (_playing)
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

            _playing = !_playing;
        }

        private async Task FadeOut(CancellationToken token)
        {
            int stepDelay = 20; // in ms
            double fadeDuration = 150; // in ms
            double steps = fadeDuration / stepDelay;
            double volumeChange = DesiredVolume / steps;

            while (BassVolume > 0)
            {
                if (token.IsCancellationRequested)
                    return;
                BassVolume -= volumeChange;
                await Task.Delay(stepDelay);
            }

            Bass.ChannelPause(_musicChannel);
        }

        private async Task FadeIn(CancellationToken token)
        {
            int stepDelay = 20; // in ms
            double fadeDuration = 150; // in ms
            double steps = fadeDuration / stepDelay;
            double volumeChange = DesiredVolume / steps;

            Bass.ChannelPlay(_musicChannel);
            while (BassVolume < DesiredVolume)
            {
                if (token.IsCancellationRequested)
                    return;
                BassVolume += volumeChange;
                await Task.Delay(stepDelay);
            }
        }

        public async Task SkipUser(int id)
        {
            if (TracksPlaylist.Count == 0)
                return;

            TracksPlaylist.RemoveAll(x => x.UserId == id);

            if (CurrentTrack!.UserId == id)
                await PlayNext();
        }

        public async Task SkipTrack(int id)
        {
            if (TracksPlaylist.Count == 0)
                return;

            TracksPlaylist.RemoveAll(x => x.Id == id);

            if (CurrentTrack!.Id == id)
                await PlayNext();
        }
    }
}