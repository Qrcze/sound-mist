using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

internal partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty] private string _validationMessage = string.Empty;
    [ObservableProperty] private string _authToken = string.Empty;
    private readonly HttpManager _httpManager;
    private readonly ProgramSettings _settings;
    private readonly ILogger _logger;
    private readonly MainWindowViewModel _mainWindowViewModel;

    public IAsyncRelayCommand GuestLoginCommand { get; }
    public IAsyncRelayCommand UseTokenCommand { get; }
    public IRelayCommand OpenSoundcloudPageCommand { get; }

    public LoginViewModel(HttpManager httpManager, ProgramSettings settings, ILogger logger, MainWindowViewModel mainViewModel)
    {
        _httpManager = httpManager;
        _settings = settings;
        _logger = logger;
        _mainWindowViewModel = mainViewModel;
        GuestLoginCommand = new AsyncRelayCommand(GuestLogin);
        UseTokenCommand = new AsyncRelayCommand(UseToken);
        OpenSoundcloudPageCommand = new RelayCommand(OpenSoundcloudPage);
    }

    private void OpenSoundcloudPage()
    {
        SystemHelpers.OpenInBrowser("https://soundcloud.com");
    }

    private async Task UseToken()
    {
        _logger.Info("Checking authorization token");
        AuthToken = AuthToken.Trim();
        _httpManager.AuthorizedClient.Authorization = new("OAuth", AuthToken);

        using var response = await _httpManager.AuthorizedClient.GetAsync("me");
        if (!response.IsSuccessStatusCode)
        {
            _logger.Info("Provided authorization token was not valid");
            _httpManager.AuthorizedClient.Authorization = null;
            ValidationMessage = "Provided token is invalid. Are you sure you copied the value from \"oauth_token\" cookie?";
            return;
        }

        _logger.Info("Provided authorization token was valid");

        var user = await response.Content.ReadFromJsonAsync<User>();
        _settings.UserId = user.Id;
        _settings.AuthToken = AuthToken;

        _mainWindowViewModel.OpenMainView();
    }

    partial void OnAuthTokenChanged(string? oldValue, string newValue)
    {
        ValidationMessage = string.Empty;
    }

    private async Task GuestLogin()
    {
        ValidationMessage = "WIP";
    }
}