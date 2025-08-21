using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SoundMist.Models;
using SoundMist.ViewModels;
using System.Diagnostics;

namespace SoundMist.Views;

public partial class UserInfoView : UserControl, ISCDataTemplatesController
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
        var sv = (ScrollViewer)sender!;
        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
            await _vm.LoadTab();
    }

    private void TogglePreview(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _vm.ToggleFullImageCommand.Execute(null);
    }

    public void TestAction(object? sender)
    {
        Debug.Print("called test action from the user info view");
    }

    public void OpenAboutPage(object? sender, RoutedEventArgs e)
    {
        
    }

    public void PlaylistItem_AboutUser(object? sender, RoutedEventArgs e)
    {
        
    }

    public void Playlist_ViewMore(object? sender, RoutedEventArgs e)
    {
        
    }

    public void TrackItem_AboutUser(object? sender, RoutedEventArgs e)
    {
        
    }

    public void ListBox_DoubleTapped_PlaylistItem(object? sender, TappedEventArgs e)
    {
        
    }
}