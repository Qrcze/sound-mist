using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    [ObservableProperty] private bool _playing;
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private bool _playEnabled;
    [ObservableProperty] private string _loadingMessage;
    [ObservableProperty] private string _trackTimeFormatted = "00:00";
    [ObservableProperty] private string _trackLengthFormatted = "00:00";
    [ObservableProperty] private string _trackTitle = string.Empty;
    [ObservableProperty] private string _trackAuthor = string.Empty;
    [ObservableProperty] private Track? _currentTrack;

    private readonly IMusicPlayer _musicPlayer;
    private readonly ProgramSettings _settings;
    private readonly ILogger _logger;
    private double _trackTime;
    private double _trackLength;
    private bool _showHoursOnTime;

    public ObservableCollection<Track> TracksQueue { get; } = [];

    public float DesiredVolume { get => _musicPlayer.DesiredVolume; set => _musicPlayer.DesiredVolume = value; }

    public bool Shuffle
    {
        get => _settings.Shuffle;
        set
        {
            _settings.Shuffle = value;
            _musicPlayer.TracksPlaylist.ChangeShuffle(value);
        }
    }

    public double TrackTime
    {
        get => _trackTime;
        set
        {
            UpdateTime(value);
            _musicPlayer.SetPosition(value);
        }
    }

    public double TrackLength
    {
        get => _trackLength;
        set
        {
            SetProperty(ref _trackLength, value);
            TrackLengthFormatted = TimeSpan.FromMilliseconds(value).ToString(_showHoursOnTime ? @"hh\:mm\:ss" : @"mm\:ss");
        }
    }

    public IAsyncRelayCommand PlayPauseCommand { get; }
    public IAsyncRelayCommand PlayNextTrackCommand { get; }
    public IAsyncRelayCommand PlayPrevTrackCommand { get; }
    public IRelayCommand ClearPlaylistCommand { get; }
    public IAsyncRelayCommand BlockUserCommand { get; }
    public IAsyncRelayCommand BlockTrackCommand { get; }
    public IRelayCommand OpenUserInfoCommand { get; }
    public IRelayCommand OpenTrackInfoCommand { get; }

    public PlayerViewModel(IMusicPlayer musicPlayer, ProgramSettings settings, ILogger logger)
    {
        _musicPlayer = musicPlayer;
        _settings = settings;
        _logger = logger;

        _musicPlayer.TrackChanging += TrackChanging;
        _musicPlayer.TrackTimeUpdated += UpdateTime;
        _musicPlayer.PlayStateUpdated += PlayStateUpdated;
        _musicPlayer.TracksPlaylist.ListChanged += TracksPlaylist_ListChanged;

        PlayPauseCommand = new AsyncRelayCommand(PlayPause);
        PlayNextTrackCommand = new AsyncRelayCommand(PlayNextTrack);
        PlayPrevTrackCommand = new AsyncRelayCommand(PlayPrevTrack);
        ClearPlaylistCommand = new RelayCommand(ClearPlaylist);
        BlockUserCommand = new AsyncRelayCommand(BlockUser);
        BlockTrackCommand = new AsyncRelayCommand(BlockTrack);
        OpenUserInfoCommand = new RelayCommand(OpenUserInfo);
        OpenTrackInfoCommand = new RelayCommand(OpenTrackInfo);

        //when the music player got initialized before this view
        if (_musicPlayer.CurrentTrack != null)
        {
            TrackChanging(_musicPlayer.CurrentTrack);
            TracksQueue.Add(_musicPlayer.CurrentTrack);
            if (_musicPlayer.PlayerReady)
                PlayStateUpdated(PlayState.Loaded, string.Empty);
        }
    }

    private void OpenUserInfo()
    {
        if (CurrentTrack is null)
            return;

        Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, CurrentTrack.User);
    }

    private void OpenTrackInfo()
    {
        if (CurrentTrack is null)
            return;

        Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, CurrentTrack);
    }

    private void UpdateTime(double value)
    {
        SetProperty(ref _trackTime, value, nameof(TrackTime));
        TrackTimeFormatted = TimeSpan.FromMilliseconds(value).ToString(_showHoursOnTime ? @"hh\:mm\:ss" : @"mm\:ss");
    }

    private void PlayStateUpdated(PlayState state, string message)
    {
        switch (state)
        {
            case PlayState.Playing:
                Playing = true;
                PlayEnabled = true;
                Loading = false;
                break;

            case PlayState.Paused:
                Playing = false;
                PlayEnabled = true;
                Loading = false;
                break;

            case PlayState.Loading:
                Playing = false;
                PlayEnabled = false;
                Loading = true;
                LoadingMessage = message;
                break;

            case PlayState.Loaded:
                Playing = false;
                PlayEnabled = true;
                Loading = false;
                break;

            case PlayState.Error:
                Playing = false;
                PlayEnabled = false;
                Loading = true;
                LoadingMessage = message;
                _logger.Error(message);
                break;

            default:
                break;
        }
    }

    private async Task PlayPause(CancellationToken token)
    {
        await _musicPlayer.PlayPause(token);
    }

    private async Task PlayNextTrack()
    {
        await _musicPlayer.PlayNext();
    }

    private async Task PlayPrevTrack()
    {
        await _musicPlayer.PlayPrev();
    }

    private void ClearPlaylist()
    {
        _musicPlayer.ClearQueue();
    }

    private void TrackChanging(Track track)
    {
        Playing = false;
        Loading = true;

        _showHoursOnTime = TimeSpan.FromMilliseconds(track.FullDuration).Hours > 0;

        TrackTime = 0;
        TrackLength = track.FullDuration;
        TrackAuthor = track.ArtistName;
        TrackTitle = track.Title;

        CurrentTrack = track;
    }

    private void TracksPlaylist_ListChanged(TracksPlaylist.Changetype change, System.Collections.Generic.IEnumerable<Track> tracks)
    {
        switch (change)
        {
            case TracksPlaylist.Changetype.Added:
                foreach (var item in tracks)
                    TracksQueue.Add(item);
                break;

            case TracksPlaylist.Changetype.Removed:
                foreach (var item in tracks)
                    TracksQueue.Remove(item);
                break;

            case TracksPlaylist.Changetype.Cleared:
                TracksQueue.Clear();
                break;

            case TracksPlaylist.Changetype.Shuffled:
                TracksQueue.Clear();
                foreach (var item in tracks)
                    TracksQueue.Add(item);
                break;

            default:
                break;
        }
    }

    private async Task BlockUser()
    {
        if (_musicPlayer.CurrentTrack == null)
            return;

        if (_musicPlayer.CurrentTrack.User is null)
        {
            _logger.Warn($"Track with id {_musicPlayer.CurrentTrack.Id} does not contain user, failed blocking them.");
            return;
        }

        _settings.AddBlockedUser(_musicPlayer.CurrentTrack.User);
        await _musicPlayer.SkipUser(_musicPlayer.CurrentTrack.User.Id);
    }

    private async Task BlockTrack()
    {
        if (_musicPlayer.CurrentTrack == null)
            return;

        _settings.AddBlockedTrack(_musicPlayer.CurrentTrack);
        await _musicPlayer.SkipTrack(_musicPlayer.CurrentTrack.Id);
    }
}