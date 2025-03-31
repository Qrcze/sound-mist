using SoundMist.Helpers;
using SoundMist.Models;
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
    private readonly AuthorizedHttpClient _authorizedHttpClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly MainWindowViewModel _mainWindowViewModel;

    public SoundcloudDataInitializer(ProgramSettings settings, AuthorizedHttpClient authorizedHttpClient, HttpClient httpClient, ILogger logger, MainWindowViewModel mainWindowViewModel)
    {
        _settings = settings;
        _authorizedHttpClient = authorizedHttpClient;
        _httpClient = httpClient;
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
                _logger.Fatal($"Failed retrieving app version: {ex.Message}");
                _mainWindowViewModel.ShowErrorMessage("Initialization failed, please check the logs");
                return;
            }

            try
            {
                (_settings.ClientId, _settings.AnonymousUserId) = await GetClientAndAnonymousUserIds();
            }
            catch (Exception ex)
            {
                _logger.Fatal($"Failed retrieving client and anonymous user IDs: {ex.Message}");
                _mainWindowViewModel.ShowErrorMessage("Initialization failed, please check the logs");
                return;
            }

            try
            {
                await LoadView();
            }
            catch (Exception ex)
            {
                _logger.Fatal($"Failed loading the view: {ex.Message}");
                _mainWindowViewModel.ShowErrorMessage("Initialization failed, please check the logs");
                return;
            }

            try
            {
                await LoadLastTrack();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed loading last track with id {_settings.LastTrackId}: {ex.Message}");
                NotificationManager.Show(new("Initialization failure", $"Couldn't load last track with id: {_settings.LastTrackId}."));
                return;
            }
        });
    }

    public async Task<int> GetAppVersion()
    {
        using var response = await _httpClient.GetAsync("https://soundcloud.com/versions.json");
        response.EnsureSuccessStatusCode();
        var version = await response.Content.ReadFromJsonAsync<VersionResponse>();

        if (int.TryParse(version!.App, out int versionNumber))
            return versionNumber;

        throw new Exception($"App version json returned a non-numeric version: {version?.App}");
    }

    public async Task<(string clientId, string? userId)> GetClientAndAnonymousUserIds()
    {
        using var response = await _httpClient.GetAsync("https://soundcloud.com");
        response.EnsureSuccessStatusCode();

        string? clientId = null;
        string? userId = null;
        string contents = await response.Content.ReadAsStringAsync();
        foreach (Match match in ScriptSourceLinkRegex().Matches(contents))
        {
            string link = match.Groups[1].Value;

            using var script = await _httpClient.GetAsync(link);
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
            _authorizedHttpClient.DefaultRequestHeaders.Authorization = new("OAuth", _settings.AuthToken);

            using var response = await _authorizedHttpClient.GetAsync("me");
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<User>();
                _settings.UserId = user.Id;

                _mainWindowViewModel.OpenMainView();
                return;
            }
        }

        _authorizedHttpClient.DefaultRequestHeaders.Authorization = null;
        _logger.Info("The previously given authorization token expired");
        _mainWindowViewModel.OpenLoginView();
    }

    private async Task LoadLastTrack()
    {
        if (_settings.LastTrackId.HasValue)
        {
            var musicPlayer = App.GetService<IMusicPlayer>();
            var lastTrack = (await SoundCloudQueries.GetTracksById(_httpClient, _settings.ClientId, _settings.AppVersion, [_settings.LastTrackId.Value]))
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