using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SoundMist.Models;
using SoundMist.Views;

namespace SoundMist.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private Control _currentControl = new InitializationView();
    [ObservableProperty] private string _trackTitle = "SoundCloud Player";

    private readonly IMusicPlayer _musicPlayer;

    public MainWindowViewModel(IMusicPlayer musicPlayer)
    {
        _musicPlayer = musicPlayer;

        _musicPlayer.TrackChanging += (t) =>
        {
            TrackTitle = $"{t.Title} by {t.ArtistName}";
        };

#if OS_WINDOWS

        _musicPlayer.TrackChanging += (t) =>
        {
            TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Indeterminate);
        };

        _musicPlayer.TrackChanged += (t) =>
        {
            TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Normal);
            TaskbarManager.SetProgressValue(0, t.FullDuration);
        };

        _musicPlayer.TrackTimeUpdated += (ms) =>
        {
            TaskbarManager.SetProgressValue((int)ms, _musicPlayer.CurrentTrack!.FullDuration);
        };

        _musicPlayer.PlayStateUpdated += (s, m) =>
        {
            switch (s)
            {
                case PlayState.Error:
                    TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Error);
                    break;

                case PlayState.Paused:
                    TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Paused);
                    break;

                case PlayState.Playing:
                    TaskbarManager.SetProgressState(TaskbarProgressBarStatus.Normal);
                    break;
            }
        };

#endif
    }

    public void OpenMainView()
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new MainView(); });
    }

    public void OpenLoginView()
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new LoginView(); });
    }

    public void ShowErrorMessage(string message)
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new TextBlock() { Text = message }; });
    }
}