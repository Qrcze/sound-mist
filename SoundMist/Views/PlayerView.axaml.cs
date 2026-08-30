using Avalonia.Controls;
using Avalonia.VisualTree;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System;

namespace SoundMist.Views;

public partial class PlayerView : UserControl
{
    private readonly PlayerViewModel _vm;

    public PlayerView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<PlayerViewModel>();
    }

    private async void RemoveTrackFromQueue(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var c = (Control)sender!;

        var item = c.FindAncestorOfType<ListBoxItem>() ?? throw new Exception($"{nameof(RemoveTrackFromQueue)} method is expected to be called from a button inside a ListBoxItem");
        if (item.DataContext is not Track t)
            throw new ArgumentException("queue is supposed to hold Tracks in the list");
        await _vm.RemoveTrackFromQueue(t);
    }

    private async void ChangeToSelectedFromQueue(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        await _vm.LoadTrackSelectedInPlaylistQueue();
    }

    private async void PlaylistItem_Play(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _vm.LoadTrackSelectedInPlaylistQueue();
    }

    private void PlaylistItem_AboutTrack(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.ShowingPlaylist = false;
        Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, _vm.TrackSelectedInQueue);
    }

    private void PlaylistItem_AboutUploader(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.ShowingPlaylist = false;
        Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, _vm.TrackSelectedInQueue!.User);
    }

    private void VolumeSlider_PointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        const float step = 0.05f;
        var volume = Math.Clamp(_vm.DesiredVolume + (float)(e.Delta.Y * step), 0f, 1f);

        _vm.DesiredVolume = volume;
        VolumeSlider.Value = volume;
        e.Handled = true;
    }
}
