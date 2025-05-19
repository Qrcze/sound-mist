using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class ProxyFailView : UserControl
{
    public ProxyFailView()
    {
        InitializeComponent();
        DataContext = App.GetService<ProxyFailViewModel>();
    }
}