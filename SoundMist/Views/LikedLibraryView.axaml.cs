using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System.Linq;

namespace SoundMist.Views;

public partial class LikedLibraryView : UserControl
{
    private readonly LikedLibraryViewModel _vm;

    public LikedLibraryView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<LikedLibraryViewModel>();

        LikedList.Loaded += (s, e) =>
        {
            var scrollViewer = LikedList.FindDescendantOfType<ScrollViewer>();
            if (scrollViewer != null)
                scrollViewer.ScrollChanged += OnScrollChanged;
            else
                FileLogger.Instance.Warn("Failed getting the ScrollViewer for Liked ListBox!");
        };

        Loaded += ViewLoaded;
    }

    private async void ViewLoaded(object? sender, RoutedEventArgs e)
    {
        await _vm.DownloadTrackList();
        Loaded -= ViewLoaded;
    }

    private async void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var sv = (ScrollViewer)sender!;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
            await _vm.DownloadTrackList();
    }

    private async void ListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        await _vm.PlayQueue(LikedList.Items.Skip(LikedList.SelectedIndex).Select(x => (Track)x!)!);
    }
}