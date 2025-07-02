using Avalonia.Controls;
using Avalonia.VisualTree;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SoundMist.Views;

public partial class TrackInfoView : UserControl
{
    private readonly TrackInfoViewModel _vm;

    public TrackInfoView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<TrackInfoViewModel>();
    }

    private void TogglePreview(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _vm.ToggleFullImageCommand.Execute(null);
    }

    private void OpenTagPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var b = (Button)sender!;

        Debug.Print($"pressed tag: {b.Tag}");
    }

    private async void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_vm.LoadingView)
            return;

        var sv = (ScrollViewer)sender!;
        
        ScrollToTopButton.IsVisible = sv.Offset.Y > 50;

        if (sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - 100)
            await _vm.LoadMoreComments();
    }

    private void ScrollToTheTop(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Scroll.ScrollToHome();
    }

    private void PlayFromCommentTimestamp(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var b = (Button)sender!;
        var item = b.FindAncestorOfType<ListBoxItem>();

        var comment = item!.Content as Comment;
        Task.Run(() => _vm.PlayTrackFromTimestamp(comment!.Timestamp));
    }
}