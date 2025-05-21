using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SoundMist;

public partial class SoundcloudDataInitializer
{
    private readonly ProgramSettings _settings;
    private readonly HttpManager _httpManager;
    private readonly ILogger _logger;
    private readonly MainWindowViewModel _mainWindowViewModel;
    private bool _mainViewOpened;

    public SoundcloudDataInitializer(ProgramSettings settings, HttpManager httpManager, ILogger logger, MainWindowViewModel mainWindowViewModel)
    {
        _settings = settings;
        _httpManager = httpManager;
        _logger = logger;
        _mainWindowViewModel = mainWindowViewModel;
    }

    public void Run()
    {
        Task.Run(async () =>
        {
            try
            {
                _settings.AppVersion = await GetAppVersion();
            }
            catch (Exception ex)
            {
                HandleExceptioon(ex, "Couldn't retrieve app version");
                return;
            }

            try
            {
                (_settings.ClientId, _settings.AnonymousUserId) = await GetClientAndAnonymousUserIds();
            }
            catch (Exception ex)
            {
                HandleExceptioon(ex, "Couldn't retrieve client and anonymous user IDs");
                _mainWindowViewModel.ShowInitializationErrorMessage("Initialization failed, please check the logs");
                return;
            }

            try
            {
                await LoadView();
            }
            catch (Exception ex)
            {
                HandleExceptioon(ex, "Couldn't load the main view");
                _mainWindowViewModel.ShowInitializationErrorMessage("Initialization failed, please check the logs");
                return;
            }

            if (!_mainViewOpened)
                return;

            try
            {
                await LoadLastTrack();
            }
            catch (Exception ex)
            {
                _logger.Error($"Couldn't load last track with id {_settings.LastTrackId}: {ex.Message}");
                NotificationManager.Show(new("Initialization failure", $"Couldn't load last track with id: {_settings.LastTrackId}."));
                return;
            }
        });
    }

    void HandleExceptioon(Exception ex, string message)
    {
        if (ex is HttpRequestException)
        {
            if (_settings.ProxyMode == ProxyMode.Always)
            {
                _logger.Error($"{message} while in always-proxy mode: {ex.Message}");
                _mainWindowViewModel.OpenProxyFailView();
                return;
            }
        }

        _logger.Fatal($"{message}: {ex.Message}");
        _mainWindowViewModel.ShowInitializationErrorMessage(message);
    }

    public async Task<int> GetAppVersion()
    {
        using var response = await _httpManager.DefaultClient.GetAsync("https://soundcloud.com/versions.json");

        response.EnsureSuccessStatusCode();
        var version = await response.Content.ReadFromJsonAsync<VersionResponse>();

        if (int.TryParse(version!.App, out int versionNumber))
            return versionNumber;

        throw new Exception($"App version json returned a non-numeric version: {version?.App}");
    }

    public async Task<(string clientId, string? userId)> GetClientAndAnonymousUserIds()
    {
        using var response = await _httpManager.DefaultClient.GetAsync("https://soundcloud.com");
        response.EnsureSuccessStatusCode();

        string? clientId = null;
        string? userId = null;
        string contents = await response.Content.ReadAsStringAsync();
        foreach (Match match in ScriptSourceLinkRegex().Matches(contents))
        {
            string link = match.Groups[1].Value;

            using var script = await _httpManager.DefaultClient.GetAsync(link);
            if (!script.IsSuccessStatusCode)
                continue;

            string scriptContent = await script.Content.ReadAsStringAsync();

            if (userId == null)
            {
                var idMatch = UserIdRegex().Match(scriptContent);
                if (idMatch.Success)
                    userId = idMatch.Groups[1].Value;
            }

            if (clientId == null)
            {
                var idMatch = ClientIdRegex().Match(scriptContent);
                if (idMatch.Success)
                    clientId = idMatch.Groups[1].Value;
            }

            if (clientId != null && userId != null)
                return (clientId, userId);
        }

        if (userId is null)
            _logger.Warn("Failed finding the anonymous user id");

        if (clientId is not null)
            return (clientId, userId);

        string message = $"Failed search for the soundcloud client id";
        _logger.Fatal(message);
        throw new NullReferenceException(message);
    }

    private async Task LoadView()
    {
        if (string.IsNullOrEmpty(_settings.AuthToken))
        {
            _mainWindowViewModel.OpenLoginView();
            return;
        }
        else
        {
            //check if token is still valid
            _httpManager.AuthorizedClient.DefaultRequestHeaders.Authorization = new("OAuth", _settings.AuthToken);

            using var response = await _httpManager.AuthorizedClient.GetAsync("me");
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<User>();
                _settings.UserId = user.Id;

                _mainWindowViewModel.OpenMainView();
                _mainViewOpened = true;
                return;
            }
        }

        _httpManager.AuthorizedClient.DefaultRequestHeaders.Authorization = null;
        _logger.Warn("The previously given authorization token expired");
        _mainWindowViewModel.OpenLoginView();
    }

    private async Task LoadLastTrack()
    {
        if (_settings.LastTrackId.HasValue)
        {
            var musicPlayer = App.GetService<IMusicPlayer>();
            var lastTrack = (await SoundCloudQueries.GetTracksById(_httpManager.DefaultClient, _settings.ClientId, _settings.AppVersion, [_settings.LastTrackId.Value]))
                .Single();
            await musicPlayer.LoadNewQueue([lastTrack], null, _settings.StartPlayingOnLaunch);
        }
    }

    [GeneratedRegex("<script[^>]+src=\"(.+)\"", RegexOptions.RightToLeft)]
    private static partial Regex ScriptSourceLinkRegex();

    [GeneratedRegex("client_id:\"([a-zA-Z0-9]+)\"")]
    private static partial Regex ClientIdRegex();

    [GeneratedRegex("user_id:\"([a-zA-Z0-9-]+)\"")]
    private static partial Regex UserIdRegex();

    private class VersionResponse
    {
        [JsonPropertyName("app")]
        public string App { get; set; } = string.Empty;

        [JsonPropertyName("serviceWorker")]
        public string ServiceWorker { get; set; } = string.Empty;

        [JsonPropertyName("serviceWorkerUnregistrationPatterns")]
        public List<object> ServiceWorkerUnregistrationPatterns { get; set; } = [];
    }
}