using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class UserInfoViewModel : ViewModelBase
{
    [ObservableProperty] private User? _user;
    [ObservableProperty] private bool _loadingView;

    private readonly IDatabase _database;
    private readonly ILogger _logger;
    private readonly History _history;

    private CancellationTokenSource? _tokenSource;

    public IRelayCommand OpenInBrowserCommand { get; }

    public UserInfoViewModel(IDatabase database, ILogger logger, History history)
    {
        Mediator.Default.Register(MediatorEvent.OpenUserInfo, OpenUser);

        _database = database;
        _logger = logger;
        _history = history;
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

        if (userWithIdOnly.Id == User?.Id)
            return;

        _history.AddUserInfoHistory(userWithIdOnly);

        _tokenSource?.Cancel();
        _tokenSource = new();
        var token = _tokenSource.Token;

        Task.Run(async () =>
        {
            try
            {
                User = (await _database.GetUserById(userWithIdOnly.Id, token));
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