using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
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
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty] private Track _track = Track.CreatePlaceholderTrack();
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isCurrentTrack;
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

    private readonly SoundCloudQueries _soundCloudQueries;
    private readonly IMusicPlayer _musicPlayer;
    private readonly History _history;
    private readonly IDatabase _database;

    public IRelayCommand OpenUrlInBrowserCommand { get; }
    public IAsyncRelayCommand PlayPauseCommand { get; }
    public IRelayCommand ToggleFullImageCommand { get; }
    public IRelayCommand OpenArtistProfileCommand { get; }

    public TrackInfoViewModel(SoundCloudQueries soundCloudQueries, IMusicPlayer musicPlayer, History history, IDatabase database)
    {
        Mediator.Default.Register(MediatorEvent.OpenTrackInfo, OpenTrack);
        _soundCloudQueries = soundCloudQueries;
        _musicPlayer = musicPlayer;
        _history = history;
        _database = database;
        OpenUrlInBrowserCommand = new RelayCommand(OpenUrlInBrowser);
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

        var (response, error) = await _soundCloudQueries.GetTrackComments(_commentsNextHref, _commentsLookup, Track.Id, token);

        if (token.IsCancellationRequested)
            return;

        if (!string.IsNullOrEmpty(error))
        {
            _loadingComments = false;
            _logger.Error(error);
            NotificationManager.Show(new("Failed retrieving comments", "Please check logs for more information.", NotificationType.Error));
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
            _logger.Warn("Track id {id} didn't have a user reference", Track.Id);
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
                Track = await _database.GetTrackById(Track.Id, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Failed retrieving full info for the track");
                NotificationManager.Show(new("Failed retrieving track info",
                    $"SC responded with {(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : "unknown code")} {ex.StatusCode}",
                    NotificationType.Error,
                    TimeSpan.Zero));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception getting full info for the track");
                NotificationManager.Show(new("Failed retrieving track info", "Unhandled exception, please check logs.", NotificationType.Error, TimeSpan.Zero));
            }

            if (!string.IsNullOrEmpty(Track.WaveformUrl))
            {
                var (waveform, errorMessage) = await _soundCloudQueries.GetTrackWaveform(Track.WaveformUrl, token);
                if (token.IsCancellationRequested)
                    return;

                if (waveform is not null)
                    Samples = waveform.Samples;
                else
                {
                    _logger.Warn(errorMessage!);
                    NotificationManager.Show(new("Failed retrieving waveform data", "Please check the logs", NotificationType.Warning, TimeSpan.FromSeconds(10)));
                }
            }

            LoadingView = false;

            var (commentsAll, error) = await _soundCloudQueries.GetTrackCommentsAhead(null, Track.Id, token);
            if (token.IsCancellationRequested)
                return;

            if (!string.IsNullOrEmpty(error))
            {
                _logger.Error(error);
                NotificationManager.Show(new("Failed retrieving comments", "Please check logs for more information.", NotificationType.Error));
            }
            else
            {
                _commentsLookup = commentsAll!.Collection.Select(x => x.Id).ToHashSet();
                await LoadMoreComments(true);
            }
        }, token);
    }
}
