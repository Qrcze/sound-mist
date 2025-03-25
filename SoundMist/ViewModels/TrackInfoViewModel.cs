using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class TrackInfoViewModel : ViewModelBase
{
    [ObservableProperty] private Track? _track;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isCurrentTrack;
    [ObservableProperty] private int[] _samples = [];
    private double _position;

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

    private readonly HttpClient _httpClient;
    private readonly IMusicPlayer _musicPlayer;
    private readonly ILogger _logger;

    public IRelayCommand OpenUrlInBrowserCommand { get; }
    public IAsyncRelayCommand LikeTrackCommand { get; }
    public IAsyncRelayCommand PlayPauseCommand { get; }

    public TrackInfoViewModel(HttpClient httpClient, IMusicPlayer musicPlayer, ILogger logger)
    {
        Mediator.Default.Register(MediatorEvent.OpenTrackInfo, OpenTrack);
        _httpClient = httpClient;
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
        //TODO
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
        Samples = [];
        Track = obj as Track;
        if (Track is null)
            return;

        if (_musicPlayer.CurrentTrack is not null)
            TrackChanged(_musicPlayer.CurrentTrack);

        //for now just throw the task run, cancellation later i guess
        if (Track.WaveformUrl is not null)
            Task.Run(async () =>
            {
                try
                {
                    var e = await SoundCloudQueries.GetTrackWaveform(_httpClient, Track.WaveformUrl);

                    Samples = e.Samples;

                    //foreach (var sample in e.Samples)
                    //    Samples.Add(sample);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error($"Request for track waveform failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Faulure while loading track waveform: {ex.Message}");
                }
            });
    }
}