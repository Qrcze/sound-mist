using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

public class UserTabData : ObservableObject
{
    private bool _loading;

    public ObservableCollection<object> Items { get; } = [];
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
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty] private User _user = User.CreateDummyUser();
    [ObservableProperty] private bool _loadingView;
    [ObservableProperty] private bool _showFullImage;

    private int _openedTabIndex;

    public int OpenedTabIndex
    {
        get => _openedTabIndex;
        set
        {
            SetProperty(ref _openedTabIndex, value);

            Task.Run(() => LoadTab());
        }
    }

    private readonly IDatabase _database;
    private readonly SoundCloudQueries _soundCloudQueries;
    private readonly SoundCloudCommands _soundCloudCommands;
    private readonly History _history;
    private readonly IMusicPlayer? _musicPlayer;

    private CancellationTokenSource? _tokenSource;
    private HashSet<long> _likedTrackIds = [];

    public UserTabData All { get; } = new();
    public UserTabData PopularTracks { get; } = new();
    public UserTabData Tracks { get; } = new();
    public UserTabData Albums { get; } = new();
    public UserTabData Playlists { get; } = new();
    public UserTabData Reposts { get; } = new();

    public IRelayCommand OpenInBrowserCommand { get; }
    public IRelayCommand ToggleFullImageCommand { get; }

    public UserInfoViewModel(IDatabase database, SoundCloudQueries soundCloudQueries, SoundCloudCommands soundCloudCommands, History history, IMusicPlayer? musicPlayer = null)
    {
        Mediator.Default.Register(MediatorEvent.OpenUserInfo, OpenUser);

        _database = database;
        _soundCloudQueries = soundCloudQueries;
        _soundCloudCommands = soundCloudCommands;
        _history = history;
        _musicPlayer = musicPlayer;
        _soundCloudCommands.TrackLikeChanged += OnTrackLikeChanged;
        OpenInBrowserCommand = new RelayCommand(OpenInBrowser);
        ToggleFullImageCommand = new RelayCommand(() => ShowFullImage = !ShowFullImage);
    }

    private void OnTrackLikeChanged(Track changedTrack, bool liked)
    {
        if (liked)
            _likedTrackIds.Add(changedTrack.Id);
        else
            _likedTrackIds.Remove(changedTrack.Id);

        foreach (var track in All.Items.Concat(PopularTracks.Items).Concat(Tracks.Items).Concat(Reposts.Items).OfType<Track>().Where(track => track.Id == changedTrack.Id))
            track.IsLiked = liked;
    }

    internal async Task PlayTrack(Track track)
    {
        if (_musicPlayer is null)
            return;

        _database.AddTrack(track);
        await _musicPlayer.LoadNewQueue([track]);
    }

    private void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(User?.PermalinkUrl))
            return;

        SystemHelpers.OpenInBrowser(User.PermalinkUrl);
    }

    public async Task LoadTab(bool force = false, UserTab? requestedTab = null)
    {
        if (_tokenSource is null || User is null)
            return;

        var token = _tokenSource.Token;
        var tab = requestedTab ?? (UserTab)OpenedTabIndex;

        switch (tab)
        {
            case UserTab.All:
                await LoadTab(force, All, _soundCloudQueries.GetUserAll, "hasn't uploaded anything yet.", token);
                break;

            case UserTab.PopularTracks:
                await LoadTab(force, PopularTracks, _soundCloudQueries.GetUserTopTracks, "hasn't uploaded anything yet.", token);
                break;

            case UserTab.Tracks:
                await LoadTab(force, Tracks, _soundCloudQueries.GetUserTracks, "hasn't uploaded any tracks yet.", token);
                while (!Tracks.ReachedEnd && !token.IsCancellationRequested)
                    await LoadTab(true, Tracks, _soundCloudQueries.GetUserTracks, "hasn't uploaded any tracks yet.", token);
                break;

            case UserTab.Albums:
                await LoadTab(force, Albums, _soundCloudQueries.GetUserAlbums, "hasn't created any albums yet.", token);
                break;

            case UserTab.Playlists:
                await LoadTab(force, Playlists, _soundCloudQueries.GetUserPlaylists, "hasn't created any playlists yet.", token);
                break;

            case UserTab.Reposts:
                await LoadTab(force, Reposts, _soundCloudQueries.GetUserReposts, "hasn't reposted any sounds yet.", token);
                break;

            default:
                _logger.Error("Tried opening a tab that is not defined by enum: {tab}", tab);
                break;
        }
    }

    private delegate Task<(QueryResponse<T>? tracks, string? errorMessage)> TabQuery<T>(long i1, string? i2, CancellationToken token);

    private async Task LoadTab<T>(bool force, UserTabData tabData, TabQuery<T> getObjects, string emptyMessage, CancellationToken token)
    {
        if (tabData.Loading || tabData.ReachedEnd)
            return;

        tabData.Loading = true;

        if (!force && tabData.Items.Count > 0)
        {
            tabData.Loading = false;
            return;
        }

        await LoadTabItems(tabData, getObjects, token);

        if (tabData.Items.Count == 0 && tabData.ReachedEnd)
            tabData.Items.Add($"{User?.Username} {emptyMessage}");

        tabData.Loading = false;
    }

    private async Task LoadTabItems<T>(UserTabData tabData, TabQuery<T> getObjects, CancellationToken token)
    {
        var (response, errorMessage) = await getObjects(User!.Id, tabData.NextHref, token);
        if (response == null)
        {
            _logger.Error(errorMessage!);
            tabData.ReachedEnd = true;
            return;
        }

        bool entryFail = false;
        foreach (var item in response.Collection)
        {
            if (item is UserEntry entry)
            {
                if (entry.Track is not null)
                {
                    entry.Track.IsLiked = _likedTrackIds.Contains(entry.Track.Id);
                    tabData.Items.Add(entry.Track);
                }
                else if (entry.Playlist is not null)
                    tabData.Items.Add(entry.Playlist);
                else
                {
                    entryFail = true;
                    _logger.Error("Received a user entry that doesn't contain track nor playlist - entry type: {entryType}", entry.Type);
                }
            }
            else if (item is not null)
            {
                if (item is Track track)
                    track.IsLiked = _likedTrackIds.Contains(track.Id);
                tabData.Items.Add(item);
            }
            else
            {
                entryFail = true;
                _logger.Error("Received an unknown object for user view: {item} (checked user id: {id}, on tab: {tab})", item, User.Id, (UserTab)OpenedTabIndex);
            }
        }
        if (entryFail)
            NotificationManager.Show(new("Unhandled items", "SoundCloud returned unexpected items. Please check logs for further info.", NotificationType.Error, TimeSpan.Zero));

        tabData.NextHref = response.NextHref;
        tabData.ReachedEnd = string.IsNullOrEmpty(response.NextHref);
    }

    private void OpenUser(object? obj)
    {
        if (obj is not User userWithIdOnly)
            return;
        if (userWithIdOnly.Id == User?.Id)
            return;

        LoadingView = true;
        User = null;
        ShowFullImage = false;

        All.Clear();
        PopularTracks.Clear();
        Tracks.Clear();
        Albums.Clear();
        Playlists.Clear();
        Reposts.Clear();

        _history.AddUserInfoHistory(userWithIdOnly);

        _tokenSource?.Cancel();
        _tokenSource = new();
        _likedTrackIds = [];
        var token = _tokenSource.Token;

        Task.Run(async () =>
        {
            try
            {
                var (likedTracks, _) = await _soundCloudQueries.GetUsersLikedTracksIds(token);
                if (likedTracks is not null)
                    _likedTrackIds = likedTracks.Collection.ToHashSet();

                User = (await _database.GetUserById(userWithIdOnly.Id, token));
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Failed retrieving full info for the user");
                NotificationManager.Show(new("Failed retrieving user info",
                    $"SC responded with {(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : "unknown code")} {ex.StatusCode}",
                    NotificationType.Error,
                    TimeSpan.Zero));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception getting full info for the user");
                NotificationManager.Show(new("Failed retrieving user info", "Unhandled exception, please check logs.", NotificationType.Error, TimeSpan.Zero));
            }

            SetProperty(ref _openedTabIndex, (int)UserTab.All, nameof(OpenedTabIndex));
            await LoadTab(true); //todo make it a setting to load the default user tab
            LoadingView = false;
        }, token);
    }
}
