using Avalonia.Controls;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SoundMist.Views;

public partial class DownloadedView : UserControl
{
    private readonly DownloadedViewModel _vm;

    public DownloadedView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<DownloadedViewModel>();
    }

    private async void ListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        await _vm.PlayQueue(LikedList.Items.Skip(LikedList.SelectedIndex).Select(x => (Track)x!)!);
    }
}