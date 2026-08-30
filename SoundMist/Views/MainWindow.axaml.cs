using Avalonia.Controls;
using SoundMist.ViewModels;
using Avalonia.Input;
using Avalonia.Interactivity;
using SoundMist.Models.Audio;

namespace SoundMist.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly IMusicPlayer _musicPlayer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<MainWindowViewModel>();
        _musicPlayer = App.GetService<IMusicPlayer>();
        AddHandler(InputElement.KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel);

        Position = _vm.Position;
        PositionChanged += MainWindow_PositionChanged;
    }

    private void MainWindow_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        _vm.Position = e.Point;
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.KeyModifiers != KeyModifiers.None || e.Source is TextBox)
            return;

        e.Handled = true;
        _musicPlayer.PlayPause();
    }
}
