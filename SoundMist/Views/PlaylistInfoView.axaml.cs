using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class PlaylistInfoView : UserControl
{
    private readonly PlaylistInfoViewModel _vm;

    public PlaylistInfoView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<PlaylistInfoViewModel>();
    }

    private void ListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var lb = (ListBox)sender!;
        _vm.PlayFromIndex(lb.SelectedIndex);
    }

    private void TogglePreview(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _vm.ToggleFullImageCommand.Execute(null);
    }
}