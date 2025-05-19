using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SoundMist.Models.Audio;
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