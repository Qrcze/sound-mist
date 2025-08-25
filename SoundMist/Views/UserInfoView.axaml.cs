using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SoundMist.ViewModels;

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
            await _vm.LoadTab();
    }

    private void TogglePreview(object? sender, TappedEventArgs e)
    {
        _vm.ToggleFullImageCommand.Execute(null);
    }
}