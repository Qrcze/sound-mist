using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class PlayerView : UserControl
{
    public PlayerView()
    {
        InitializeComponent();
        DataContext = App.GetService<PlayerViewModel>();
    }

    private void ShowPlaylist(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Control c)
            return;

        FlyoutBase.ShowAttachedFlyout(c);
    }

    private void ClosePlaylist(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Control c)
            return;

        var f = FlyoutBase.GetAttachedFlyout(c);
        f?.Hide();
    }
}