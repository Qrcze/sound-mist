using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class SearchView : UserControl
{
    private readonly SearchViewModel _vm;

    public SearchView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<SearchViewModel>();

        ResultsList.Loaded += StartObservingScrollViewer;
    }

    private void StartObservingScrollViewer(object? sender, RoutedEventArgs e)
    {
        ResultsList.Loaded -= StartObservingScrollViewer;

        var scrollViewer = ResultsList.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer != null)
            scrollViewer.ScrollChanged += OnScrollChangedAsync;
        else
            FileLogger.Instance.Warn("Failed getting the ScrollViewer for Liked ListBox!");
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

    private async void UseQueryResult(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (QueriesFlyout.SelectedItem is not SearchQuery query)
            return;

        _vm.SearchFilter = query.Output;
        SearchBox.CaretIndex = SearchBox.Text?.Length ?? 0;
        await _vm.RunSearch();
    }
}