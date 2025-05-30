using Avalonia.Controls;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class UserInfoView : UserControl
{
    private readonly UserInfoViewModel _vm;

    public UserInfoView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<UserInfoViewModel>();
    }

    private void TogglePreview(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _vm.ToggleFullImageCommand.Execute(null);
    }
}