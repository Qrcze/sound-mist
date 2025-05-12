using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace SoundMist.Windows;

internal class Program
{
    private static IMusicPlayer _musicPlayer = null!;
    private static ILogger _logger = null!;
    private static MediaPlayer _mp = null!;
    private static SystemMediaTransportControls _smtc = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        App.ServiceConfigured += SetupWindowsIntegration;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            FileLogger.Instance.Fatal($"Program crashed unexpectedly: {ex.Message}");
        }
    }

    private static void SetupWindowsIntegration(ServiceProvider services)
    {
        _musicPlayer = services.GetRequiredService<IMusicPlayer>();
        _logger = services.GetRequiredService<ILogger>();

        _mp = new MediaPlayer();
        _smtc = _mp.SystemMediaTransportControls;
        _smtc.IsEnabled = true;
        _smtc.IsNextEnabled = true;
        _smtc.IsPreviousEnabled = true;
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.IsStopEnabled = false;

        _smtc.PlaybackPositionChangeRequested += (s, e) => _musicPlayer.SetPosition(e.RequestedPlaybackPosition.TotalMilliseconds);
        _smtc.ButtonPressed += _smtc_ButtonPressed;

        _musicPlayer.TrackChanging += (t) =>
        {
            TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Indeterminate);
            _smtc.PlaybackStatus = MediaPlaybackStatus.Changing;
            UpdateTrackMetadata(t);
        };

        _musicPlayer.TrackChanged += (t) =>
        {
            TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Normal);
            TaskbarManager.SetProgressValue(0, t.FullDuration);
            _smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
        };

        _musicPlayer.TrackTimeUpdated += (ms) =>
        {
            var track = _musicPlayer.CurrentTrack;
            if (track is null)
                return;
            TaskbarManager.SetProgressValue((int)ms, track.FullDuration);
        };

        _musicPlayer.PlayStateUpdated += (s, m) =>
        {
            switch (s)
            {
                case PlayState.Error:
                    TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Error);
                    _smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
                    break;

                case PlayState.Paused:
                    TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Paused);
                    _smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;

                case PlayState.Playing:
                    TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Normal);
                    _smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
            }
        };
    }

    private static void _smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        switch (args.Button)
        {
            case SystemMediaTransportControlsButton.Play:
                _musicPlayer.Play();
                break;

            case SystemMediaTransportControlsButton.Pause:
                _musicPlayer.Pause();
                break;

            case SystemMediaTransportControlsButton.Next:
                _musicPlayer.PlayNext();
                break;

            case SystemMediaTransportControlsButton.Previous:
                _musicPlayer.PlayPrev();
                break;

            default:
                _logger.Warn($"Pressed an unhandled button on the windows media controls: {args.Button}");
                break;
        }
    }

    static void UpdateTrackMetadata(Track track)
    {
        var updater = _smtc.DisplayUpdater;
        updater.Type = MediaPlaybackType.Music;
        updater.MusicProperties.Title = track.Title;
        updater.MusicProperties.Artist = track.ArtistName;
        if (track.ArtworkUrlLarge != null)
            updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(track.ArtworkUrlLarge));
        updater.Update();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}