using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SoundMist.Models;
using SoundMist.ViewModels;
using System;

namespace SoundMist.Views;

public partial class SearchView : UserControl
{
    private readonly SearchViewModel _vm;
    private volatile bool _pauseLoadingMore;

    public SearchView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<SearchViewModel>();

        ResultsList.Loaded += (s, e) =>
        {
            var scrollViewer = ResultsList.FindDescendantOfType<ScrollViewer>();
            if (scrollViewer != null)
                scrollViewer.ScrollChanged += OnScrollChangedAsync;
            else
                FileLogger.Instance.Warn("Failed getting the ScrollViewer for Liked ListBox!");
        };
    }

    private async void OnScrollChangedAsync(object? sender, ScrollChangedEventArgs e)
    {
        if (_pauseLoadingMore)
            return;

        var sv = (ScrollViewer)sender!;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
            await _vm.RunSearch(false);
    }

    private async void TextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Return || e.Key == Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            _pauseLoadingMore = true;
            await _vm.RunSearch();
            _pauseLoadingMore = false;
        }
    }

    private async void ListBox_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        await _vm.RunSelectedItem();
    }

    private async void ListBox_DoubleTapped_PlaylistItem(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        e.Handled = true; // to prevent the parent ListBox from triggering DoubleTapped

        if (sender is not ListBox listBox)
            return;

        var parent = listBox.FindAncestorOfType<ListBoxItem>() ?? throw new NullReferenceException("Couldn't find the playlist track's parent ListBoxItem");
        if (parent.DataContext is not Playlist playlist)
            throw new ArgumentException("Clicked item in playlist is not actually within a playlist ListBox");

        await _vm.PlayFromPlaylist(playlist, listBox.SelectedIndex);
    }

    private void OpenAboutPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.OpenAboutPage();
    }

    private void TrackItem_AboutUser(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm.SelectedItem is Track track)
            _vm.OpenAboutPage(track.User);
    }

    private void PlaylistItem_AboutUser(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>();
        if (item?.DataContext is Playlist playlist)
            _vm.OpenAboutPage(playlist.User);
    }
}