using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NLog;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using SoundMist;
using System.Linq;

namespace SoundMist.Views;

public partial class LikedLibraryView : UserControl
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

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
                _logger.Warn("Failed getting the ScrollViewer for Liked ListBox!");
        };

        Loaded += ViewLoaded;
    }

    private async void ViewLoaded(object? sender, RoutedEventArgs e)
    {
        if (_vm.LoadAllLikedTracks)
            await _vm.DownloadAllTrackList();
        else
            await _vm.DownloadTrackList();
        await _vm.DownloadLikedPlaylists();
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
        if (e.Source is Control source)
        {
            var item = source.FindAncestorOfType<ListBoxItem>();
            if (item?.DataContext is Track track)
                _vm.SelectedTrack = track;
        }

        if (LikedList.SelectedIndex < 0)
            return;

        await _vm.PlayQueue(LikedList.Items.Skip(LikedList.SelectedIndex).Select(x => (Track)x!)!);
    }

    private void Playlist_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (e.Source is Control source && source.FindAncestorOfType<ListBoxItem>()?.DataContext is Playlist playlist)
            Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, playlist);
    }
}
