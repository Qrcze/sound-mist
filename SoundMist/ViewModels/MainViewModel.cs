using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Models;
using System;

namespace SoundMist.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private MainViewTab _openedTabIndex;
    [ObservableProperty] private bool _hasOpenedTrackInfo;
    [ObservableProperty] private bool _hasOpenedUserInfo;

    private readonly ProgramSettings _programSettings;

    public IRelayCommand OpenSettingsCommand { get; }

    public MainViewModel(ProgramSettings programSettings)
    {
        Mediator.Default.Register(MediatorEvent.OpenTrackInfo, OpenTrackInfoTab);
        Mediator.Default.Register(MediatorEvent.OpenUserInfo, OpenUserInfoTab);

        _programSettings = programSettings;

        OpenedTabIndex = _programSettings.StartingTabIndex;

        OpenSettingsCommand = new RelayCommand(OpenSettings);
    }

    private void OpenTrackInfoTab(object? o)
    {
        HasOpenedTrackInfo = true;
        OpenedTabIndex = MainViewTab.TrackInfo;
    }
    
    private void OpenUserInfoTab(object? o)
    {
        HasOpenedUserInfo = true;
        OpenedTabIndex = MainViewTab.UserInfo;
    }

    public void OpenSettings()
    {
        Mediator.Default.Invoke(MediatorEvent.OpenSettings);
    }
}