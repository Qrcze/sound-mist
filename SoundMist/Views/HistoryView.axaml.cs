using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System;

namespace SoundMist.Views;

public partial class HistoryView : UserControl, ISCDataTemplatesController
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

    private void HistoryView_Loaded(object? sender, RoutedEventArgs e)
    {
        _vm.TabChanged();
    }

    public void OpenAboutPage(object? sender, RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>();
        _vm.OpenAboutPage(item?.DataContext!);
    }

    public void TrackItem_AboutUser(object? sender, RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>();
        if (item?.DataContext is Track track)
            _vm.OpenAboutPage(track.User!);
    }

    public void PlaylistItem_AboutUser(object? sender, RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>();
        if (item?.DataContext is Playlist playlist)
            _vm.OpenAboutPage(playlist.User);
    }

    public void Playlist_ViewMore(object? sender, RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>()?.DataContext as Playlist ?? throw new InvalidCastException($"expected a {nameof(Playlist)} in the ListBoxItem's DataContext");

        Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, item);
    }

    public void ListBox_DoubleTapped_PlaylistItem(object? sender, Avalonia.Input.TappedEventArgs e)
    {
    }

    public void PlaySelectedTrack(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var box = (ListBox)sender!;
        var track = box.SelectedItem as Track ?? throw new InvalidCastException($"{nameof(PlaySelectedTrack)} can should be only called on lists with items of type {nameof(Track)}");
        _vm.PlayTrack(track);
    }

    public void OpenSelectedUser(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var box = (ListBox)sender!;
        var user = box.SelectedItem as User ?? throw new InvalidCastException($"{nameof(OpenSelectedUser)} can should be only called on lists with items of type {nameof(User)}");
        Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, user);
    }

    public void OpenSelectedPlaylist(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var box = (ListBox)sender!;
        var playlist = box.SelectedItem as Playlist ?? throw new InvalidCastException($"{nameof(OpenSelectedPlaylist)} can should be only called on lists with items of type {nameof(Playlist)}");
        Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, playlist);
    }
}