using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        DataContext = App.GetService<MainViewModel>();
    }
}