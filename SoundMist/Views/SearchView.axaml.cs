using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System;
using static SoundMist.ViewModels.SearchViewModel;

namespace SoundMist.Views;

public partial class SearchView : UserControl
{
    private readonly SearchViewModel _vm;

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
        var sv = (ScrollViewer)sender!;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
            await _vm.RunSearch(false);
    }

    private async void TextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Return || e.Key == Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            await _vm.RunSearch();
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

    private async void UseQueryResult(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (QueriesFlyout.SelectedItem is not SearchQuery query)
            return;

        _vm.SearchFilter = query.Output;
        SearchBox.CaretIndex = SearchBox.Text?.Length ?? 0;
        await _vm.RunSearch();
    }

    private void Playlist_ViewMore(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = sender as Control;
        var item = c.FindLogicalAncestorOfType<ListBoxItem>()?.DataContext as Playlist ?? throw new InvalidCastException($"expected a {nameof(Playlist)} in the ListBoxItem's DataContext");

        Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, item);
    }
}