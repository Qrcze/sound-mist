using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Models;
using System.Collections.ObjectModel;

namespace SoundMist.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isVisible;

    public MainViewTab DefaultTabOnLaunch { get => _settings.StartingTabIndex; set => _settings.StartingTabIndex = value; }
    public MainViewTab[] TabsSelection { get; } = { MainViewTab.Search, MainViewTab.LikedTracks, MainViewTab.Downloaded };
    public ObservableCollection<BlockedEntry> BlockedUsers { get; } = [];
    public ObservableCollection<BlockedEntry> BlockedTracks { get; } = [];

    public IRelayCommand CloseCommand { get; }

    private readonly ProgramSettings _settings;

    public SettingsViewModel(ProgramSettings settings)
    {
        _settings = settings;

        Mediator.Default.Register(MediatorEvent.OpenSettings, _ => IsVisible = true);

        CloseCommand = new RelayCommand(() => IsVisible = false);
    }

    partial void OnIsVisibleChanged(bool value)
    {
        if (!value)
            return;

        BlockedUsers.Clear();
        BlockedTracks.Clear();

        foreach (var item in _settings.BlockedTracks)
            BlockedTracks.Add(item);
        foreach (var item in _settings.BlockedUsers)
            BlockedUsers.Add(item);
    }

    public void RemoveBlockedTrack(BlockedEntry entry)
    {
        BlockedTracks.Remove(entry);
        _settings.BlockedTracks.Remove(entry);
    }

    public void RemoveBlockedUser(BlockedEntry entry)
    {
        BlockedUsers.Remove(entry);
        _settings.BlockedUsers.Remove(entry);
    }
}