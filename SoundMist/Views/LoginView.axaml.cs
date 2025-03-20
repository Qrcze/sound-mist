using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContext = App.GetService<LoginViewModel>();
    }
}