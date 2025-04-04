using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SoundMist.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private AppColorTheme _selectedTheme;

    public MainViewTab DefaultTabOnLaunch { get => _settings.StartingTabIndex; set => _settings.StartingTabIndex = value; }
    public bool StartPlayingOnLaunch { get => _settings.StartPlayingOnLaunch; set => _settings.StartPlayingOnLaunch = value; }

    public MainViewTab[] TabsSelection { get; } = { MainViewTab.Search, MainViewTab.LikedTracks, MainViewTab.Downloaded };
    public AppColorTheme[] Themes { get; } = Enum.GetValues<AppColorTheme>().ToArray();

    public ObservableCollection<BlockedEntry> BlockedUsers { get; } = [];
    public ObservableCollection<BlockedEntry> BlockedTracks { get; } = [];

    public IRelayCommand CloseCommand { get; }

    private readonly ProgramSettings _settings;

    public SettingsViewModel(ProgramSettings settings)
    {
        _settings = settings;
        SelectedTheme = _settings.AppColorTheme;

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

    partial void OnSelectedThemeChanged(AppColorTheme value)
    {
        _settings.AppColorTheme = value;
    }

    public void RemoveBlockedTrack(BlockedEntry entry)
    {
        BlockedTracks.Remove(entry);
        _settings.RemoveBlockedTrack(entry);
    }

    public void RemoveBlockedUser(BlockedEntry entry)
    {
        BlockedUsers.Remove(entry);
        _settings.RemoveBlockedUser(entry);
    }
}