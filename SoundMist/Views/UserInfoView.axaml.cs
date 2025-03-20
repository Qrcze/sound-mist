using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class UserInfoView : UserControl
{
    public UserInfoView()
    {
        InitializeComponent();
        DataContext = App.GetService<UserInfoViewModel>();
    }
}