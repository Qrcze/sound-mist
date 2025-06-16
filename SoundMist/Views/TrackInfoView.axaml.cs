using Avalonia.Controls;
using Avalonia.VisualTree;
using SoundMist.ViewModels;
using System.Diagnostics;

namespace SoundMist.Views;

public partial class TrackInfoView : UserControl
{
    private readonly TrackInfoViewModel _vm;

    public TrackInfoView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<TrackInfoViewModel>();
    }

    private void TogglePreview(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _vm.ToggleFullImageCommand.Execute(null);
    }

    private void OpenTagPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var b = (Button)sender!;

        Debug.Print($"pressed tag: {b.Tag}");
    }
}