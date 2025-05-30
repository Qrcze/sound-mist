using Avalonia.Controls;
using SoundMist.ViewModels;

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
}