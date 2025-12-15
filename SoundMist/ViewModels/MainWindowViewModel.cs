using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Views;
using System.Timers;

namespace SoundMist.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private Control _currentControl = new InitializationView();
    [ObservableProperty] private string _trackTitle = "SoundCloud Player";
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;
    [ObservableProperty] private PixelPoint _position;

    private readonly IMusicPlayer _musicPlayer;
    private readonly ProgramSettings _settings;
    private readonly Timer _windowSizeSaveTimer;

    public MainWindowViewModel(IMusicPlayer musicPlayer, ProgramSettings settings)
    {
        _musicPlayer = musicPlayer;
        _settings = settings;
        _musicPlayer.TrackChanging += (t) =>
        {
            TrackTitle = $"{t.Title} by {t.ArtistName}";
        };

        _width = settings.WindowPos.Width;
        _height = settings.WindowPos.Height;
        _position = new((int)settings.WindowPos.Position.X, (int)settings.WindowPos.Position.Y);
        _settings.WindowPosReset += size =>
        {
            SetProperty(ref _width, size.Width, nameof(Width));
            SetProperty(ref _height, size.Height, nameof(Height));
        };

        _windowSizeSaveTimer = new Timer
        {
            Interval = 2_000,
            AutoReset = false,
        };
        _windowSizeSaveTimer.Elapsed += SaveWindowSize;
    }

    public void OpenInitializationView()
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new InitializationView(); });
    }

    public void OpenMainView()
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new MainView(); });
    }

    public void OpenLoginView()
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new LoginView(); });
    }

    public void OpenProxyFailView()
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new ProxyFailView(); });
    }

    public void ShowInitializationErrorMessage(string message)
    {
        Dispatcher.UIThread.Post(() => { CurrentControl = new InitializationErrorView() { Text = message, MainWindowViewModel = this }; });
    }

    private void SaveWindowSize(object? sender, ElapsedEventArgs e)
    {
        Rect newSize = new(Position.X, Position.Y, Width, Height);
        if (newSize.Position.NearlyEquals(_settings.WindowPos.Position) && newSize.Size.NearlyEquals(_settings.WindowPos.Size))
            return;

        _settings.WindowPos = newSize;
    }

    partial void OnWidthChanged(double value)
    {
        _windowSizeSaveTimer.Stop();
        _windowSizeSaveTimer.Start();
    }

    partial void OnHeightChanging(double value)
    {
        _windowSizeSaveTimer.Stop();
        _windowSizeSaveTimer.Start();
    }

    partial void OnPositionChanged(PixelPoint value)
    {
        _windowSizeSaveTimer.Stop();
        _windowSizeSaveTimer.Start();
    }
}