using Avalonia.Controls;
using Avalonia.Interactivity;
using SoundMist.Helpers;
using SoundMist.ViewModels;

namespace SoundMist.Views;

public partial class InitializationErrorView : UserControl
{
    public string? Text
    {
        get => ErrorTextBlock.Text;
        set => ErrorTextBlock.Text = value;
    }

    public required MainWindowViewModel MainWindowViewModel { get; init; }

    public InitializationErrorView()
    {
        InitializeComponent();
    }

    private void RetryInit(object? sender, RoutedEventArgs e)
    {
        MainWindowViewModel.OpenInitializationView();

        var initializer = App.GetService<SoundcloudDataInitializer>();
        initializer.Run();
    }

    private void OpenSoundCloud(object? sender, RoutedEventArgs e)
    {
        SystemHelpers.OpenInBrowser("https://soundcloud.com");
    }
}