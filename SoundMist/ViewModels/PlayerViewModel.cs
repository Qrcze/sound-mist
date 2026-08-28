using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty] private bool _playing;
    [ObservableProperty] private bool _playEnabled;
    [ObservableProperty] private bool _showingPlaylist;
    [ObservableProperty] private string _loadingMessage = string.Empty;
    [ObservableProperty] private string _trackTimeFormatted = "00:00";
    [ObservableProperty] private string _trackLengthFormatted = "00:00";
    [ObservableProperty] private string _trackTitle = string.Empty;
    [ObservableProperty] private string _trackAuthor = string.Empty;
    [ObservableProperty] private string? _trackThumbnail = string.Empty;
    [ObservableProperty] private Track _trackSelectedInQueue = Track.CreatePlaceholderTrack();
    [ObservableProperty] private bool _volumeHigh;
    [ObservableProperty] private bool _volumeMid;
    [ObservableProperty] private bool _volumeLow;
    [ObservableProperty] private bool _volumeMuted;

    private readonly IMusicPlayer _musicPlayer;
    private readonly ProgramSettings _settings;
    private readonly History _history;
    private double _trackTime;
    private double _trackLength;
    private bool _showHoursOnTime;

    public ObservableCollection<Track> TracksQueue { get; } = [];

    public float DesiredVolume
    {
        get => _musicPlayer.DesiredVolume;
        set
        {
            _musicPlayer.DesiredVolume = value;
            UpdateVolumeMode();
        }
    }

    public bool Shuffle
    {
        get => _settings.Shuffle;
        set
        {
            _settings.Shuffle = value;
            _musicPlayer.TracksPlaylist.ChangeShuffle(value);
        }
    }

    public RepeatMode RepeatMode
    {
        get => _settings.RepeatMode;
        set
        {
            if (_settings.RepeatMode == value)
                return;

            _settings.RepeatMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RepeatOne));
            OnPropertyChanged(nameof(RepeatIconOpacity));
            OnPropertyChanged(nameof(RepeatTooltip));
        }
    }

    public bool RepeatOne => RepeatMode == RepeatMode.One;
    public double RepeatIconOpacity => RepeatMode == RepeatMode.Off ? 0.4 : 1;
    public string RepeatTooltip => RepeatMode switch
    {
        RepeatMode.Queue => "Repeat queue",
        RepeatMode.One => "Repeat one",
        _ => "Repeat off",
    };

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

    public IRelayCommand PlayPauseCommand { get; }
    public IAsyncRelayCommand PlayNextTrackCommand { get; }
    public IAsyncRelayCommand PlayPrevTrackCommand { get; }
    public IRelayCommand ClearPlaylistCommand { get; }
    public IAsyncRelayCommand BlockUserCommand { get; }
    public IAsyncRelayCommand BlockTrackCommand { get; }
    public IRelayCommand OpenUserInfoCommand { get; }
    public IRelayCommand OpenTrackInfoCommand { get; }
    public IRelayCommand TogglePlaylistCommand { get; }
    public IRelayCommand MuteVolumeCommand { get; }
    public IRelayCommand CycleRepeatCommand { get; }

    public PlayerViewModel(IMusicPlayer musicPlayer, ProgramSettings settings, History history)
    {
        _musicPlayer = musicPlayer;
        _settings = settings;
        _history = history;

        _musicPlayer.TrackChanging += TrackChanging;
        _musicPlayer.TrackTimeUpdated += UpdateTime;
        _musicPlayer.PlayStateUpdated += PlayStateUpdated;
        _musicPlayer.TracksPlaylist.ListChanged += TracksPlaylist_ListChanged;

        PlayPauseCommand = new RelayCommand(_musicPlayer.PlayPause);
        PlayNextTrackCommand = new AsyncRelayCommand(_musicPlayer.PlayNext);
        PlayPrevTrackCommand = new AsyncRelayCommand(_musicPlayer.PlayPrev);
        ClearPlaylistCommand = new RelayCommand(_musicPlayer.ClearQueue);
        BlockUserCommand = new AsyncRelayCommand(BlockUser);
        BlockTrackCommand = new AsyncRelayCommand(BlockTrack);
        OpenUserInfoCommand = new RelayCommand(OpenUserInfo);
        OpenTrackInfoCommand = new RelayCommand(OpenTrackInfo);
        TogglePlaylistCommand = new RelayCommand(() => ShowingPlaylist = !ShowingPlaylist);
        MuteVolumeCommand = new RelayCommand(ToggleMute);
        CycleRepeatCommand = new RelayCommand(CycleRepeat);

        //when the music player got initialized before this view
        if (_musicPlayer.CurrentTrack != null)
        {
            TrackChanging(_musicPlayer.CurrentTrack);
            TracksQueue.Add(_musicPlayer.CurrentTrack);
            if (_musicPlayer.PlayerReady)
                PlayStateUpdated(PlayState.Loaded, string.Empty);
        }

        UpdateVolumeMode();
    }

    private void CycleRepeat()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.Queue,
            RepeatMode.Queue => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.Off,
        };
    }

    private void ToggleMute()
    {
        _musicPlayer.Mute = VolumeMuted = !_musicPlayer.Mute;
    }

    private void UpdateVolumeMode()
    {
        VolumeMuted = false;
        switch (_musicPlayer.DesiredVolume)
        {
            case < 0.33f:
                VolumeHigh = false;
                VolumeMid = false;
                VolumeLow = true;
                break;

            case < 0.66f:
                VolumeHigh = false;
                VolumeMid = true;
                VolumeLow = false;
                break;

            default:
                VolumeHigh = true;
                VolumeMid = false;
                VolumeLow = false;
                break;
        }
    }

    private void OpenUserInfo()
    {
        if (_musicPlayer.CurrentTrack is null)
            return;

        Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, _musicPlayer.CurrentTrack.User);
    }

    private void OpenTrackInfo()
    {
        if (_musicPlayer.CurrentTrack is null)
            return;

        Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, _musicPlayer.CurrentTrack);
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
                break;

            case PlayState.Paused:
                Playing = false;
                PlayEnabled = true;
                break;

            case PlayState.Loading:
                LoadingMessage = message;
                break;

            case PlayState.Loaded:
                PlayEnabled = true;
                LoadingMessage = string.Empty;
                _history.AddPlayedHistory(_musicPlayer.CurrentTrack!);
                break;

            case PlayState.Error:
                Playing = false;
                PlayEnabled = false;
                LoadingMessage = message;
                break;

            default:
                break;
        }
    }

    private void TrackChanging(Track track)
    {
        Playing = false;
        PlayEnabled = false;

        _showHoursOnTime = TimeSpan.FromMilliseconds(track.FullDuration).Hours > 0;

        TrackTime = 0;
        TrackLength = track.FullDuration;
        TrackAuthor = track.ArtistName;
        TrackTitle = track.Title;
        TrackThumbnail = track.ArtworkUrlSmall;

        TrackSelectedInQueue = track;
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
        var track = _musicPlayer.CurrentTrack;

        if (track is null)
            return;

        if (!track.UserId.HasValue || track.User is null)
        {
            _logger.Warn("Track with id {id} does not contain user id, failed blocking them.", track.Id);
            return;
        }

        _logger.Info("Blocking user: {userId}", track.UserId);

        _settings.AddBlockedUser(track.User);
        _musicPlayer.TracksPlaylist.RemoveAll(x => x.UserId == track.UserId);

        //if there are no tracks available, add the last track temporarily to generate the autoplay out of
        if (_musicPlayer.TracksPlaylist.Count == 0)
        {
            _musicPlayer.TracksPlaylist.Add(track);
            await _musicPlayer.PlayNext();
            _musicPlayer.TracksPlaylist.RemoveAll(x => x.Id == track.Id);
        }
        else
        {
            await _musicPlayer.ReloadCurrentTrack();
        }
    }

    private async Task BlockTrack()
    {
        var track = _musicPlayer.CurrentTrack;

        if (track == null)
            return;

        _logger.Info("Blocking track: {id}", track.Id);

        _settings.AddBlockedTrack(track);
        _musicPlayer.TracksPlaylist.RemoveAll(x => x.Id == track.Id);

        //if there are no tracks available, add the last track temporarily to generate the autoplay out of
        if (_musicPlayer.TracksPlaylist.Count == 0)
        {
            _musicPlayer.TracksPlaylist.Add(track);
            await _musicPlayer.PlayNext();
            _musicPlayer.TracksPlaylist.RemoveAll(x => x.Id == track.Id);
        }
        else
        {
            await _musicPlayer.ReloadCurrentTrack();
        }
    }

    internal async Task LoadTrackSelectedInPlaylistQueue()
    {
        if (TrackSelectedInQueue is null)
            return;

        _musicPlayer.TracksPlaylist.TryMovePositionToTrack(TrackSelectedInQueue);

        await _musicPlayer.ReloadCurrentTrack();
    }

    internal async Task RemoveTrackFromQueue(Track t)
    {
        _musicPlayer.TracksPlaylist.RemoveAll(x => x.Id == t.Id);
        TracksQueue.Remove(t);

        if (_musicPlayer.CurrentTrack?.Id == t.Id)
            await _musicPlayer.ReloadCurrentTrack();
    }
}