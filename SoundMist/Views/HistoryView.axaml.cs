using Avalonia.Controls;
using Avalonia.LogicalTree;
using SoundMist.Models;
using SoundMist.ViewModels;
using System;

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

    private async void OnlineScrollChangedAsync(object? sender, ScrollChangedEventArgs e)
    {
        var sv = ((ListBox)sender!).Scroll!;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
            await _vm.GetMoreOnlineHistory();
    }

    private void HistoryView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.TabChanged();
    }

    private void OpenAboutPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>();
        _vm.OpenAboutPage(item?.DataContext!);
    }

    private void TrackItem_AboutUser(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>();
        if (item?.DataContext is Track track)
            _vm.OpenAboutPage(track.User!);
    }

    private void PlaylistItem_AboutUser(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>();
        if (item?.DataContext is Playlist playlist)
            _vm.OpenAboutPage(playlist.User);
    }

    private void Playlist_ViewMore(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>()?.DataContext as Playlist ?? throw new InvalidCastException($"expected a {nameof(Playlist)} in the ListBoxItem's DataContext");

        Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, item);
    }

    private void ListBox_DoubleTapped_PlaylistItem(object? sender, Avalonia.Input.TappedEventArgs e)
    {
    }
}