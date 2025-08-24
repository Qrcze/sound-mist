using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

public enum UserTab
{
    All,
    PopularTracks,
    Tracks,
    Albums,
    Playlists,
    Reposts,
}

public class UserTabData<T> : ObservableObject
{
    private bool _loading;

    public ObservableCollection<T> Items { get; } = [];
    public bool Loading { get => _loading; set => SetProperty(ref _loading, value); }
    public string? NextHref { get; set; }
    public bool ReachedEnd { get; set; }

    internal void Clear()
    {
        Items.Clear();
        Loading = false;
        NextHref = null;
        ReachedEnd = false;
    }
}

public partial class UserInfoViewModel : ViewModelBase
{
    [ObservableProperty] private User? _user;
    [ObservableProperty] private bool _loadingView;
    [ObservableProperty] private bool _showFullImage;

    private int _openedTabIndex;

    public int OpenedTabIndex
    {
        get => _openedTabIndex;
        set
        {
            SetProperty(ref _openedTabIndex, value);

            if (_tokenSource == null)
                return;

            Task.Run(LoadTab);
        }
    }

    private readonly IDatabase _database;
    private readonly SoundCloudQueries _soundCloudQueries;
    private readonly ILogger _logger;
    private readonly History _history;

    private CancellationTokenSource? _tokenSource;

    public UserTabData<object> All { get; } = new();
    public UserTabData<object> PopularTracks { get; } = new();
    public UserTabData<object> Tracks { get; } = new();
    public UserTabData<object> Albums { get; } = new();
    public UserTabData<object> Playlists { get; } = new();
    public UserTabData<object> Reposts { get; } = new();

    public IRelayCommand OpenInBrowserCommand { get; }
    public IRelayCommand ToggleFullImageCommand { get; }

    public UserInfoViewModel(IDatabase database, SoundCloudQueries soundCloudQueries, ILogger logger, History history)
    {
        Mediator.Default.Register(MediatorEvent.OpenUserInfo, OpenUser);

        _database = database;
        _soundCloudQueries = soundCloudQueries;
        _logger = logger;
        _history = history;
        OpenInBrowserCommand = new RelayCommand(OpenInBrowser);
        ToggleFullImageCommand = new RelayCommand(() => ShowFullImage = !ShowFullImage);
    }

    private void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(User?.PermalinkUrl))
            return;

        SystemHelpers.OpenInBrowser(User.PermalinkUrl);
    }

    public async Task LoadTab()
    {
        if (_tokenSource is null || User is null)
            return;

        var token = _tokenSource.Token;
        var tab = (UserTab)OpenedTabIndex;

        switch (tab)
        {
            case UserTab.All:
                await LoadObjectsTab(All, _soundCloudQueries.GetUserAll, token);
                AddTextIfEmpty(All, $"{User.Username} hasn't uploaded anything yet.");
                break;

            case UserTab.PopularTracks:
                await LoadObjectsTab(PopularTracks, _soundCloudQueries.GetUserTopTracks, token);
                AddTextIfEmpty(PopularTracks, $"{User.Username} hasn't uploaded anything yet.");
                break;

            case UserTab.Tracks:
                await LoadObjectsTab(Tracks, _soundCloudQueries.GetUserTracks, token);
                AddTextIfEmpty(Tracks, $"{User.Username} hasn't uploaded any tracks yet.");
                break;

            case UserTab.Albums:
                await LoadObjectsTab(Albums, _soundCloudQueries.GetUserAlbums, token);
                AddTextIfEmpty(Albums, $"{User.Username} hasn't created any albums yet.");
                break;

            case UserTab.Playlists:
                await LoadObjectsTab(Playlists, _soundCloudQueries.GetUserPlaylists, token);
                AddTextIfEmpty(Playlists, $"{User.Username} hasn't created any playlists yet.");
                break;

            case UserTab.Reposts:
                await LoadObjectsTab(Reposts, _soundCloudQueries.GetUserReposts, token);
                AddTextIfEmpty(Reposts, $"{User.Username} hasn't reposted any sounds yet.");
                break;

            default:
                _logger.Error($"Tried opening a tab that is not defined by enum: {tab}");
                break;
        }
    }

    private void AddTextIfEmpty(UserTabData<object> tabData, string emptyMessage)
    {
        if (tabData.Items.Count == 0)
            tabData.Items.Add(emptyMessage);
    }

    private async Task LoadObjectsTab<T>(UserTabData<object> tabData, Func<long, string?, CancellationToken, Task<(QueryResponse<T>? tracks, string? errorMessage)>> getObjects, CancellationToken token)
    {
        if (tabData.Loading || tabData.ReachedEnd)
            return;
        tabData.Loading = true;

        var (response, errorMessage) = await getObjects(User!.Id, tabData.NextHref, token);
        if (response == null)
        {
            _logger.Error(errorMessage!);
            return;
        }

        bool entryFail = false;
        foreach (var item in response.Collection)
        {
            if (item is UserEntry entry)
            {
                if (entry.Track is not null)
                    tabData.Items.Add(entry.Track);
                else if (entry.Playlist is not null)
                    tabData.Items.Add(entry.Playlist);
                else
                {
                    entryFail = true;
                    _logger.Error($"Received a user entry that doesn't contain track nor playlist - entry type: {entry.Type}");
                }
            }
            else if (item is not null)
            {
                tabData.Items.Add(item);
            }
            else
            {
                entryFail = true;
                _logger.Error($"Received an unknown object for user view: {item} (checked user id: {User.Id}, on tab: {(UserTab)OpenedTabIndex})");
            }
        }
        if (entryFail)
            NotificationManager.Show(new("Unhandled items", "SoundCloud returned unexpected items. Please check logs for further info.", NotificationType.Error, TimeSpan.Zero));

        tabData.NextHref = response.NextHref;
        tabData.ReachedEnd = string.IsNullOrEmpty(response.NextHref);

        tabData.Loading = false;
    }

    private void OpenUser(object? obj)
    {
        LoadingView = true;
        User = null;
        ShowFullImage = false;

        All.Clear();
        PopularTracks.Clear();
        Tracks.Clear();
        Albums.Clear();
        Playlists.Clear();
        Reposts.Clear();

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

            SetProperty(ref _openedTabIndex, (int)UserTab.All, nameof(OpenedTabIndex));
            await LoadTab(); //todo make it a setting to load the default user tab
            LoadingView = false;
        }, token);
    }
}