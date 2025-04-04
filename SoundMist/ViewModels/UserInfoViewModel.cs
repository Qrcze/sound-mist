﻿using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class UserInfoViewModel : ViewModelBase
{
    [ObservableProperty] private User? _user;
    [ObservableProperty] private bool _loadingView;

    private readonly HttpClient _httpClient;
    private readonly ProgramSettings _settings;
    private readonly ILogger _logger;

    private CancellationTokenSource? _tokenSource;

    public IRelayCommand OpenInBrowserCommand { get; }

    public UserInfoViewModel(HttpClient httpClient, ProgramSettings settings, ILogger logger)
    {
        Mediator.Default.Register(MediatorEvent.OpenUserInfo, OpenUser);

        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;

        OpenInBrowserCommand = new RelayCommand(OpenInBrowser);
    }

    private void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(User?.PermalinkUrl))
            return;

        Process.Start(new ProcessStartInfo(User.PermalinkUrl) { UseShellExecute = true });
    }

    private void OpenUser(object? obj)
    {
        LoadingView = true;
        User = null;

        if (obj is not User userWithIdOnly)
            return;

        _tokenSource?.Cancel();
        _tokenSource = new();
        var token = _tokenSource.Token;

        Task.Run(async () =>
        {
            try
            {
                User = await SoundCloudQueries.GetUserInfo(_httpClient, _settings, userWithIdOnly.Id, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Failed retrieving full info for the user: {ex.Message}");
                NotificationManager.Show(new("Failed retrieving user info",
                    $"SC responded with {(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : "unknown code")} {ex.StatusCode}",
                    NotificationType.Error,
                    TimeSpan.Zero));
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception getting full info for the user: {ex.Message}");
                NotificationManager.Show(new("Failed retrieving user info", "Unhandled exception, please check logs.", NotificationType.Error, TimeSpan.Zero));
            }

            LoadingView = false;
        }, token);
    }
}