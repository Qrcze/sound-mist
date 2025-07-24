using CommunityToolkit.Mvvm.ComponentModel;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    [ObservableProperty] private bool _loadingView;
    [ObservableProperty] private int _openedTabIndex;
    [ObservableProperty] private bool _userLoggedIn;

    History.List OpenedTab { get; set; }

    private readonly SoundCloudQueries _queries;
    private readonly IMusicPlayer _musicPlayer;
    private readonly IDatabase _database;
    private readonly ILogger _logger;
    private readonly History _history;

    private CancellationTokenSource? _loadingTokenSource;
    private CancellationTokenSource? _getOnlineTokenSource;
    private string? _nextOnlineHistoryHref;

    public ObservableCollection<Track> Played { get; } = [];
    public ObservableCollection<Track> PlayedOnline { get; } = [];
    public ObservableCollection<Track> Tracks { get; } = [];
    public ObservableCollection<User> Users { get; } = [];
    public ObservableCollection<Playlist> Playlists { get; } = [];

    public HistoryViewModel(IHttpManager httpManager, SoundCloudQueries queries, IMusicPlayer musicPlayer, IDatabase database, ILogger logger, History history)
    {
        _queries = queries;
        _musicPlayer = musicPlayer;
        _database = database;
        _logger = logger;
        _history = history;

        if (httpManager.AuthorizedClient.IsAuthorized)
            UserLoggedIn = true;

        musicPlayer.TrackChanged += (t) =>
        {
            Played.Remove(t);
            Played.Insert(0, t);
        };
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
                    Played.Clear();
                    foreach (var item in await _database.GetTracksById(_history.PlayHistory, token))
                    {
                        token.ThrowIfCancellationRequested();
                        Played.Add(item);
                    }
                    break;

                case History.List.OnlinePlayHistory:
                    break;

                case History.List.TracksHistory:
                    Tracks.Clear();
                    foreach (var item in await _database.GetTracksById(_history.TracksHistory, token))
                    {
                        token.ThrowIfCancellationRequested();
                        Tracks.Add(item);
                    }
                    break;

                case History.List.UsersHistory:
                    Users.Clear();
                    foreach (var item in await _database.GetUsersById(_history.UsersHistory, token))
                    {
                        token.ThrowIfCancellationRequested();
                        Users.Add(item);
                    }
                    break;

                case History.List.PlaylistsHistory:
                    Playlists.Clear();
                    foreach (var item in await _database.GetPlaylistsById(_history.PlaylistsHistory, token))
                    {
                        token.ThrowIfCancellationRequested();
                        Playlists.Add(item);
                    }
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

    public void OpenAboutPage(object item)
    {
        if (item is User user)
        {
            _database.AddUser(user);
            Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, user);
        }
        else if (item is Track track)
        {
            _database.AddTrack(track);
            Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, track);
        }
        else if (item is Playlist playlist)
        {
            _database.AddPlaylist(playlist);
            Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, playlist);
        }
    }

    internal async Task GetMoreOnlineHistory(bool refresh = false)
    {
        Debug.Print("getting more online history");
        LoadingView = true;

        _getOnlineTokenSource?.Cancel();
        _getOnlineTokenSource = new();
        var token = _getOnlineTokenSource.Token;

        if (refresh)
        {
            _nextOnlineHistoryHref = null;
            PlayedOnline.Clear();
        }

        try
        {
            var (response, errorMessage) = await _queries.GetPlayHistory(_nextOnlineHistoryHref, PlayedOnline.Count, token);
            if (response is null)
            {
                _logger.Warn($"Failed getting online history: {errorMessage}");
                return;
            }

            _nextOnlineHistoryHref = response.NextHref;
            foreach (var track in response.Collection.Select(x => x.Track!))
            {
                _database.AddTrack(track);
                PlayedOnline.Add(track);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error($"exception while getting online history: {ex.Message}");
        }
        finally
        {
            LoadingView = false;
        }
    }

    internal void PlayTrack(Track track)
    {
        _musicPlayer.LoadNewQueue([track]);
    }
}