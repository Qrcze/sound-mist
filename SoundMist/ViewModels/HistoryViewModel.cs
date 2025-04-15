using CommunityToolkit.Mvvm.ComponentModel;
using SoundMist.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    [ObservableProperty] private bool _loadingView;
    [ObservableProperty] private int _openedTabIndex;

    History.List OpenedTab { get; set; }

    private readonly HttpClient _httpClient;
    private readonly IDatabase _database;
    private readonly ProgramSettings _settings;
    private readonly ILogger _logger;
    private readonly History _history;

    private CancellationTokenSource? _loadingTokenSource;

    public ObservableCollection<Track> Played { get; } = [];
    public ObservableCollection<Track> Tracks { get; } = [];
    public ObservableCollection<User> Users { get; } = [];
    public ObservableCollection<Playlist> Playlists { get; } = [];

    public HistoryViewModel(HttpClient httpClient, IDatabase database, ProgramSettings settings, ILogger logger, History history)
    {
        _httpClient = httpClient;
        _database = database;
        _settings = settings;
        _logger = logger;
        _history = history;
    }

    partial void OnOpenedTabIndexChanged(int value)
    {
        OpenedTab = (History.List)value;
        TabChanged();
    }

    public void TabChanged()
    {
        _loadingTokenSource?.Cancel();
        _loadingTokenSource = new();
        var token = _loadingTokenSource.Token;

        Task.Run(() => LoadOpenedTab(token), token);
    }

    async Task LoadOpenedTab(CancellationToken token)
    {
        LoadingView = true;

        try
        {
            switch (OpenedTab)
            {
                case History.List.PlayHistory:
                    foreach (var item in await _database.GetTracksById(_history.PlayHistory.Except(Played.Select(x => x.Id)), token))
                        Played.Add(item);
                    break;

                case History.List.TracksHistory:
                    foreach (var item in await _database.GetTracksById(_history.TracksHistory.Except(Tracks.Select(x => x.Id)), token))
                        Tracks.Add(item);
                    break;

                case History.List.UsersHistory:
                    foreach (var item in await _database.GetUsersById(_history.UsersHistory.Except(Users.Select(x => x.Id)), token))
                        Users.Add(item);
                    break;

                case History.List.PlaylistsHistory:
                    foreach (var item in await _database.GetPlaylistsById(_history.PlaylistsHistory.Except(Playlists.Select(x => x.Id)), token))
                        Playlists.Add(item);
                    break;

                default:
                    break;
            }
        }
        catch (TaskCanceledException ex)
        { }
        catch (HttpRequestException ex)
        {
            _logger.Error($"Failed tracks info request for the history for tab {OpenedTab}: {ex.Message}");

            NotificationManager.Show(new($"Failed loading {OpenedTab}",
                "Error while trying to load the history list, please check the logs",
                Avalonia.Controls.Notifications.NotificationType.Error,
                TimeSpan.Zero));
        }
        catch (Exception ex)
        {
            _logger.Error($"Unhandled exception getting the history for tab {OpenedTab}: {ex.Message}");

            NotificationManager.Show(new($"Failed loading {OpenedTab}",
                "Error while trying to load the history list, please check the logs",
                Avalonia.Controls.Notifications.NotificationType.Error,
                TimeSpan.Zero));
        }
        finally
        {
            LoadingView = false;
        }
    }
}