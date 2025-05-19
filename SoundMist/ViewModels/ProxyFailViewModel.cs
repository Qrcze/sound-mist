using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Models;
using System;
using System.Linq;

namespace SoundMist.ViewModels
{
    public partial class ProxyFailViewModel : ViewModelBase
    {
        [ObservableProperty] private ProxyMode _proxyMode;
        [ObservableProperty] private ProxyProtocol _proxyProtocol;
        [ObservableProperty] private string _proxyHost;
        [ObservableProperty] private int _proxyPort;

        private readonly MainWindowViewModel _mainWindow;
        private readonly ProgramSettings _programSettings;

        public ProxyMode[] ProxyModes { get; } = Enum.GetValues<ProxyMode>().ToArray();
        public ProxyProtocol[] ProxyProtocols { get; } = Enum.GetValues<ProxyProtocol>();

        public RelayCommand RetryCommand { get; }

        public ProxyFailViewModel(MainWindowViewModel mainWindow, ProgramSettings programSettings)
        {
            _mainWindow = mainWindow;
            _programSettings = programSettings;

            (_proxyMode, _proxyProtocol, _proxyHost, _proxyPort) = _programSettings.GetProxySettings();

            RetryCommand = new(Retry);
        }

        private void Retry()
        {
            _programSettings.ApplyProxySettings(ProxyMode, ProxyProtocol, ProxyHost, ProxyPort);

            _mainWindow.OpenInitializationView();

            var initializer = App.GetService<SoundcloudDataInitializer>();
            initializer.Run();
        }
    }
}