using Avalonia.Interactivity;

namespace SoundMist.Models;

public interface ISCDataTemplatesController
{
    void OpenAboutPage(object? sender, RoutedEventArgs e);

    void PlaylistItem_AboutUser(object? sender, RoutedEventArgs e);

    void Playlist_ViewMore(object? sender, RoutedEventArgs e);

    void TrackItem_AboutUser(object? sender, RoutedEventArgs e);

    void ListBox_DoubleTapped_PlaylistItem(object? sender, Avalonia.Input.TappedEventArgs e);
}
