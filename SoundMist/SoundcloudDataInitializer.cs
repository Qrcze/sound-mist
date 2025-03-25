﻿using SoundMist.Models;
using SoundMist.ViewModels;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SoundMist;

public partial class SoundcloudDataInitializer
{
    private readonly ProgramSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly MainWindowViewModel _mainWindowViewModel;

    public SoundcloudDataInitializer(ProgramSettings settings, HttpClient httpClient, ILogger logger, MainWindowViewModel mainWindowViewModel)
    {
        _settings = settings;
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

            await LoadView();
            await LoadLastTrack();
        });
    }

    public async Task<int> GetAppVersion()
    {
        var response = await _httpClient.GetAsync("https://soundcloud.com/versions.json");
        response.EnsureSuccessStatusCode();
        var version = await response.Content.ReadFromJsonAsync<VersionResponse>();

        if (int.TryParse(version!.App, out int versionNumber))
            return versionNumber;

        throw new Exception($"App version json returned a non-numeric version: {version?.App}");
    }

    public async Task<(string clientId, string? userId)> GetClientAndAnonymousUserIds()
    {
        //todo try validating the client id somehow? possibly with http send OPTION to somewhere?
        //if (!string.IsNullOrEmpty(_settings.ClientId))
        //{
        //}

        var response = await _httpClient.GetAsync("https://soundcloud.com");
        response.EnsureSuccessStatusCode();

        string? clientId = null;
        string? userId = null;
        string contents = await response.Content.ReadAsStringAsync();
        foreach (Match match in ScriptSourceLinkRegex().Matches(contents))
        {
            string link = match.Groups[1].Value;

            var script = await _httpClient.GetAsync(link);
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
            _httpClient.DefaultRequestHeaders.Authorization = new("OAuth", _settings.AuthToken);

            var response = await _httpClient.GetAsync("me");
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<User>();
                _settings.UserId = user.Id;

                _mainWindowViewModel.OpenMainView();
                return;
            }
        }

        _httpClient.DefaultRequestHeaders.Authorization = null;
        _logger.Info("The previously given authorization token expired");
        _mainWindowViewModel.OpenLoginView();
    }

    private async Task LoadLastTrack()
    {
        if (_settings.LastTrack != null)
        {
            var musicPlayer = App.GetService<IMusicPlayer>();
            await musicPlayer.LoadNewQueue([_settings.LastTrack], null, _settings.StartPlayingOnLaunch);
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