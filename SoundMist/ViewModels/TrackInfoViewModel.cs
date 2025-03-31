using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class TrackInfoViewModel : ViewModelBase
{
    [ObservableProperty] private Track? _track;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isCurrentTrack;
    [ObservableProperty] private bool _trackLiked;
    [ObservableProperty] private bool _loadingView;
    [ObservableProperty] private int[] _samples = [];
    private double _position;

    private CancellationTokenSource? _loadTrackCancellationToken;

    public bool TrackOpened => Track is not null;

    public double Position
    {
        get => _position;
        set
        {
            SetProperty(ref _position, value);
            _musicPlayer?.SetPosition(value);
        }
    }

    private readonly AuthorizedHttpClient _authorizedHttpClient;
    private readonly HttpClient _httpClient;
    private readonly ProgramSettings _settings;
    private readonly IMusicPlayer _musicPlayer;
    private readonly ILogger _logger;

    public IRelayCommand OpenUrlInBrowserCommand { get; }
    public IAsyncRelayCommand LikeTrackCommand { get; }
    public IAsyncRelayCommand PlayPauseCommand { get; }

    public TrackInfoViewModel(AuthorizedHttpClient authorizedHttpClient, HttpClient httpClient, ProgramSettings settings, IMusicPlayer musicPlayer, ILogger logger)
    {
        Mediator.Default.Register(MediatorEvent.OpenTrackInfo, OpenTrack);
        _authorizedHttpClient = authorizedHttpClient;
        _httpClient = httpClient;
        _settings = settings;
        _musicPlayer = musicPlayer;
        _logger = logger;
        OpenUrlInBrowserCommand = new RelayCommand(OpenUrlInBrowser);
        LikeTrackCommand = new AsyncRelayCommand(LikeTrack);
        PlayPauseCommand = new AsyncRelayCommand(PlayPause);

        _musicPlayer.TrackChanged += TrackChanged;
        _musicPlayer.PlayStateUpdated += TrackStateUpdated;
    }

    private void TrackStateUpdated(PlayState state, string message)
    {
        if (Track is null || Track.Id != _musicPlayer.CurrentTrack?.Id)
            return;

        IsPlaying = state switch
        {
            PlayState.Playing => true,
            _ => false,
        };
    }

    private void TrackChanged(Track track)
    {
        if (Track is null)
            return;

        if (track.Id == Track.Id)
        {
            IsCurrentTrack = true;
            IsPlaying = _musicPlayer.Playing;
            _musicPlayer.TrackTimeUpdated += TrackTimeUpdated;
        }
        else
        {
            IsCurrentTrack = false;
            IsPlaying = false;
            _musicPlayer.TrackTimeUpdated -= TrackTimeUpdated;
        }
    }

    private void TrackTimeUpdated(double pos)
    {
        SetProperty(ref _position, pos, nameof(Position));
    }

    private async Task LikeTrack()
    {
        if (Track is null)
            return;

        if (!_settings.UserId.HasValue)
        {
            NotificationManager.Show(new("User not logged-in",
                "Please log-in to save the track to your likes",
                NotificationType.Warning));
            return;
        }

        (bool success, string message) = await SoundCloudCommands.ToggleLikedDisliked(TrackLiked, _authorizedHttpClient, Track.Id, _settings.UserId.Value, _settings.ClientId, _settings.AppVersion);
        if (success)
        {
            string title = TrackLiked ? "Track Added to Liked" : "Track Removed from Liked";
            string notifMessage = $"{Track.Title}: {(TrackLiked ? "liked" : "removed")}";

            NotificationManager.Show(new(title, notifMessage, NotificationType.Success));
        }
        else
        {
            TrackLiked = !TrackLiked; //undo toggle

            string title = TrackLiked ? "Like Failure" : "Dislike Failure";
            string notifMessage = TrackLiked ? $"Failed liking the track {Track.Title}:\n{message}"
                : $"Failed removing the track from likes {Track.Title}:\n{message}";

            NotificationManager.Show(new(title,
                notifMessage,
                NotificationType.Error,
                TimeSpan.Zero));
            _logger.Error($"failed sending a like request: {message}");
        }
    }

    private void OpenUrlInBrowser()
    {
        if (Track?.PermalinkUrl is null)
            return;

        Process.Start(new ProcessStartInfo(Track.PermalinkUrl) { UseShellExecute = true });
    }

    async Task PlayPause(CancellationToken token)
    {
        if (Track is null)
            return;

        if (Track.Id == _musicPlayer.CurrentTrack?.Id)
            await _musicPlayer.PlayPause(token);
        else
            await _musicPlayer.LoadNewQueue([Track]);
    }

    private void OpenTrack(object? obj)
    {
        _loadTrackCancellationToken?.Cancel();
        _loadTrackCancellationToken = new CancellationTokenSource();
        var token = _loadTrackCancellationToken.Token;

        LoadingView = true;
        Samples = [];
        Track = obj as Track;
        if (Track is null)
            return;

        if (_musicPlayer.CurrentTrack is not null)
            TrackChanged(_musicPlayer.CurrentTrack);

        Task.Run(async () =>
        {
            try
            {
                if (_authorizedHttpClient.IsAuthorized)
                {
                    (var likedTracksIds, string message) = await SoundCloudQueries.GetUsersLikedTracksIds(_authorizedHttpClient, _settings.ClientId, _settings.AppVersion, token);
                    if (token.IsCancellationRequested)
                        return;

                    if (likedTracksIds is not null)
                        TrackLiked = likedTracksIds.Contains(Track.Id);
                    else
                    {
                        NotificationManager.Show(new("Failed retrieving liked list", "Please check the logs", NotificationType.Warning, TimeSpan.FromSeconds(10)));
                        _logger.Error($"Failed retrieving liked tracks: {message}");
                    }
                }

                if (Track.WaveformUrl is not null)
                {
                    var e = await SoundCloudQueries.GetTrackWaveform(_httpClient, Track.WaveformUrl, token);
                    if (token.IsCancellationRequested)
                        return;

                    Samples = e.Samples;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Request for track waveform failed: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error($"Faulure while loading track waveform: {ex.Message}");
            }

            LoadingView = false;
        }, token);
    }
}