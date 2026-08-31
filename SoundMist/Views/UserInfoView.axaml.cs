using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SoundMist.ViewModels;
using SoundMist.Models.SoundCloud;
using System;

namespace SoundMist.Views;

public partial class UserInfoView : UserControl
{
    private readonly UserInfoViewModel _vm;

    public UserInfoView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<UserInfoViewModel>();

        AllList.Loaded += LoadListView;
        PopularTracksList.Loaded += LoadListView;
        TracksList.Loaded += LoadListView;
        AlbumsList.Loaded += LoadListView;
        PlaylistsList.Loaded += LoadListView;
        RepostsList.Loaded += LoadListView;
    }

    private void LoadListView(object? sender, RoutedEventArgs e)
    {
        var list = (ListBox)sender!;
        list.Loaded -= LoadListView;

        var scroll = list.FindDescendantOfType<ScrollViewer>()!;
        scroll.Tag = list.Tag;

        scroll.ScrollChanged += ScrollChanged;
    }

    private async void ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_vm.LoadingView)
            return;

        var sv = (ScrollViewer)sender!;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
        {
            if (sv.Tag is string tabName && Enum.TryParse<UserTab>(tabName, out var tab))
                await _vm.LoadTab(true, tab);
            else
                await _vm.LoadTab(true);
        }
    }

    private void TogglePreview(object? sender, TappedEventArgs e)
    {
        _vm.ToggleFullImageCommand.Execute(null);
    }

    private async void ListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source && source.FindAncestorOfType<ListBoxItem>()?.DataContext is Track track)
            await _vm.PlayTrack(track);
    }
}
