using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class HistoryView : UserControl
{
    private readonly HistoryViewModel _vm;

    public HistoryView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<HistoryViewModel>();
        Loaded += HistoryView_Loaded;
    }

    private void HistoryView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.TabChanged();
    }

    private void OpenAboutPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }
}