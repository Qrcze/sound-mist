using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<MainWindowViewModel>();
    }
}