using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SoundMist.ViewModels;

public enum ProxyMode
{
    Disable,
    BypassOnly,
    Always
}

public enum ProxyProtocol
{
    Http,
    Socks4,
    Socks5,
}

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private AppColorTheme _selectedTheme;

    [ObservableProperty] private ProxyMode _proxyMode;
    [ObservableProperty] private ProxyProtocol _proxyProtocol;
    [ObservableProperty] private string _proxyHost;
    [ObservableProperty] private int _proxyPort;

    //public ProxyMode ProxyMode { get => _settings.ProxyMode; set => _settings.ProxyMode = value; }
    //public ProxyProtocol ProxyProtocol { get => _settings.ProxyProtocol; set => _settings.ProxyProtocol = value; }
    //public string ProxyHost { get => _settings.ProxyHost; set => _settings.ProxyHost = value; }
    //public int ProxyPort { get => _settings.ProxyPort; set => _settings.ProxyPort = value; }

    public MainViewTab DefaultTabOnLaunch { get => _settings.StartingTabIndex; set => _settings.StartingTabIndex = value; }
    public bool StartPlayingOnLaunch { get => _settings.StartPlayingOnLaunch; set => _settings.StartPlayingOnLaunch = value; }
    public int HistoryLimit { get => _settings.HistoryLimit; set => _settings.HistoryLimit = value; }

    public bool OnWindows { get; }
    public bool AlternativeMediaKeys { get => _settings.AlternativeWindowsMediaKeysHandling; set => _settings.AlternativeWindowsMediaKeysHandling = value; }

    public MainViewTab[] TabsSelection { get; } = { MainViewTab.Search, MainViewTab.LikedTracks, MainViewTab.Downloaded, MainViewTab.History };
    public AppColorTheme[] Themes { get; } = Enum.GetValues<AppColorTheme>().ToArray();
    public ProxyMode[] ProxyModes { get; } = Enum.GetValues<ProxyMode>().ToArray();
    public ProxyProtocol[] ProxyProtocols { get; } = Enum.GetValues<ProxyProtocol>().ToArray();

    public ObservableCollection<BlockedEntry> BlockedUsers { get; } = [];
    public ObservableCollection<BlockedEntry> BlockedTracks { get; } = [];

    public RelayCommand CloseCommand { get; }
    public RelayCommand ResetWindowSizeCommand { get; }

    private readonly ProgramSettings _settings;

    public SettingsViewModel(ProgramSettings settings)
    {
#if OS_WINDOWS
        OnWindows = true;
#endif

        _settings = settings;
        SelectedTheme = _settings.AppColorTheme;

        (_proxyMode, _proxyProtocol, _proxyHost, _proxyPort) = _settings.GetProxySettings();

        Mediator.Default.Register(MediatorEvent.OpenSettings, _ => IsVisible = true);

        CloseCommand = new RelayCommand(() => IsVisible = false);
        ResetWindowSizeCommand = new RelayCommand(_settings.ResetWindowSize);

        if (Avalonia.Controls.Design.IsDesignMode)
            IsVisible = true;
    }

    partial void OnIsVisibleChanged(bool value)
    {
        if (!value)
        {
            SetProxySettings();
            return;
        }

        BlockedUsers.Clear();
        BlockedTracks.Clear();

        foreach (var item in _settings.BlockedTracks)
            BlockedTracks.Add(new(item.Key, item.Value));
        foreach (var item in _settings.BlockedUsers)
            BlockedUsers.Add(new(item.Key, item.Value));
    }

    private void SetProxySettings()
    {
        if (_settings.ProxyMode == ProxyMode &&
            _settings.ProxyProtocol == ProxyProtocol &&
            _settings.ProxyHost == ProxyHost &&
            _settings.ProxyPort == ProxyPort)
        {
            return;
        }

        _settings.ApplyProxySettings(ProxyMode, ProxyProtocol, ProxyHost, ProxyPort);
    }

    partial void OnSelectedThemeChanged(AppColorTheme value)
    {
        _settings.AppColorTheme = value;
    }

    public void RemoveBlockedTrack(BlockedEntry entry)
    {
        BlockedTracks.Remove(entry);
        _settings.RemoveBlockedTrack(entry.Id);
    }

    public void RemoveBlockedUser(BlockedEntry entry)
    {
        BlockedUsers.Remove(entry);
        _settings.RemoveBlockedUser(entry.Id);
    }
}