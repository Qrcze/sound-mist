using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    [ObservableProperty] private bool _showFullImage;
    [ObservableProperty] private int[] _samples = [];
    private double _position;
    private bool _loadingComments;
    private bool _commentsEnded;
    private string? _commentsNextHref;
    private HashSet<long> _commentsLookup = [];
    private CancellationTokenSource? _commentsTokenSource;
    private CancellationTokenSource? _loadTrackCancellationToken;

    public ObservableCollection<Comment> Comments { get; } = [];

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

    private readonly HttpManager _httpManager;
    private readonly ProgramSettings _settings;
    private readonly IMusicPlayer _musicPlayer;
    private readonly ILogger _logger;
    private readonly History _history;

    public IRelayCommand OpenUrlInBrowserCommand { get; }
    public IAsyncRelayCommand LikeTrackCommand { get; }
    public IAsyncRelayCommand PlayPauseCommand { get; }
    public IRelayCommand ToggleFullImageCommand { get; }
    public IRelayCommand OpenArtistProfileCommand { get; }

    public TrackInfoViewModel(HttpManager httpManager, ProgramSettings settings, IMusicPlayer musicPlayer, ILogger logger, History history)
    {
        Mediator.Default.Register(MediatorEvent.OpenTrackInfo, OpenTrack);
        _httpManager = httpManager;
        _settings = settings;
        _musicPlayer = musicPlayer;
        _logger = logger;
        _history = history;
        OpenUrlInBrowserCommand = new RelayCommand(OpenUrlInBrowser);
        LikeTrackCommand = new AsyncRelayCommand(LikeTrack);
        PlayPauseCommand = new AsyncRelayCommand(PlayPause);
        ToggleFullImageCommand = new RelayCommand(() => ShowFullImage = !ShowFullImage);
        OpenArtistProfileCommand = new RelayCommand(OpenArtistProfile);

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
            PlayState.Paused => false,
            PlayState.Error => false,
            _ => IsPlaying
        };
    }

    private void TrackChanged(Track track)
    {
        if (Track is null)
            return;

        if (track.Id == Track.Id)
        {
            IsCurrentTrack = true;
            IsPlaying = _musicPlayer.IsPlaying;
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

        (bool success, string message) = await SoundCloudCommands.ToggleLikedDisliked(TrackLiked, _httpManager.AuthorizedClient, Track.Id, _settings.UserId.Value, _settings.ClientId, _settings.AppVersion);
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

    public async Task LoadMoreComments(bool force = false)
    {
        if (Track is null)
            return;

        if (_loadingComments && !force || _commentsEnded)
            return;

        _commentsTokenSource?.Cancel();
        _commentsTokenSource = new CancellationTokenSource();
        var token = _commentsTokenSource.Token;

        _loadingComments = true;

        var (response, error) = await SoundCloudQueries.GetTrackComments(_httpManager.DefaultClient, _commentsNextHref, _commentsLookup, Track.Id, _settings.ClientId, _settings.AppVersion, token);

        if (token.IsCancellationRequested)
            return;

        if (!string.IsNullOrEmpty(error))
        {
            _loadingComments = false;
            _logger.Error(error);
            Dispatcher.UIThread.Post(() => NotificationManager.Show(new("Failed retrieving comments", "Please check logs for more information.", NotificationType.Error)));
        }

        _commentsNextHref = response!.NextHref;
        _commentsEnded = string.IsNullOrEmpty(_commentsNextHref);

        Dispatcher.UIThread.Post(() =>
        {
            foreach (var item in response.Collection)
            {
                if (token.IsCancellationRequested)
                    break;

                Comments.Add(item);
            }

            _loadingComments = false;
        });
    }

    private void OpenArtistProfile()
    {
        if (Track is null)
            return;
        if (Track.User is null)
        {
            _logger.Warn($"Track id {Track.Id} didn't have a user reference");
            return;
        }
        Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, Track.User);
    }

    private void OpenUrlInBrowser()
    {
        if (Track?.PermalinkUrl is null)
            return;

        SystemHelpers.OpenInBrowser(Track.PermalinkUrl);
    }

    async Task PlayPause(CancellationToken token)
    {
        if (Track is null)
            return;

        if (Track.Id == _musicPlayer.CurrentTrack?.Id)
            _musicPlayer.PlayPause();
        else
            await _musicPlayer.LoadNewQueue([Track]);
    }

    internal async Task PlayTrackFromTimestamp(int timestamp)
    {
        if (Track is null)
            return;

        if (Track.Id == _musicPlayer.CurrentTrack?.Id)
        {
            _musicPlayer.SetPosition(timestamp);
            _musicPlayer.Play();
        }
        else
        {
            await _musicPlayer.LoadNewQueue([Track]);
            _musicPlayer.SetPosition(timestamp);
        }
    }

    private void OpenTrack(object? obj)
    {
        if (obj is not Track track)
            throw new ArgumentException($"{MediatorEvent.OpenTrackInfo} mediator event is expected to provide a {nameof(Track)} object as parameter");

        if (track == Track)
            return;

        _loadTrackCancellationToken?.Cancel();
        _loadTrackCancellationToken = new CancellationTokenSource();
        var token = _loadTrackCancellationToken.Token;

        LoadingView = true;
        ShowFullImage = false;

        Comments.Clear();
        _commentsEnded = false;
        _commentsNextHref = null;

        Samples = [];
        Track = track;
        _history.AddTrackInfoHistory(Track);

        if (_musicPlayer.CurrentTrack is not null)
            TrackChanged(_musicPlayer.CurrentTrack);

        Task.Run(async () =>
        {
            try
            {
                if (_httpManager.AuthorizedClient.IsAuthorized)
                {
                    var (response, message) = await SoundCloudQueries.GetUsersLikedTracksIds(_httpManager.AuthorizedClient, _settings.ClientId, _settings.AppVersion, token);
                    if (token.IsCancellationRequested)
                        return;

                    if (response is not null)
                        TrackLiked = response.Collection.Contains(Track.Id);
                    else
                    {
                        _logger.Error($"Failed retrieving liked tracks: {message}");
                        Dispatcher.UIThread.Post(() => NotificationManager.Show(new("Failed retrieving liked list", "Please check the logs", NotificationType.Warning, TimeSpan.FromSeconds(10))));
                    }
                }

                if (Track.WaveformUrl is not null)
                {
                    var e = await SoundCloudQueries.GetTrackWaveform(_httpManager.DefaultClient, Track.WaveformUrl, token);
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

            var (commentsAll, error) = await SoundCloudQueries.GetTrackCommentsAhead(_httpManager.DefaultClient, null, Track.Id, _settings.ClientId, _settings.AppVersion, token);

            if (!string.IsNullOrEmpty(error))
            {
                _logger.Error(error);
                Dispatcher.UIThread.Post(() => NotificationManager.Show(new("Failed retrieving comments", "Please check logs for more information.", NotificationType.Error)));
            }
            else
            {
                _commentsLookup = commentsAll!.Collection.Select(x => x.Id).ToHashSet();
                await LoadMoreComments(true);
            }
        }, token);
    }
}