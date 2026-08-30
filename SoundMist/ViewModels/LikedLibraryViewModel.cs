using Avalonia.Controls.Notifications;
using Avalonia.Threading;
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
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace SoundMist.ViewModels
{
    public partial class LikedLibraryViewModel : ViewModelBase
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        [ObservableProperty] private ObservableCollection<Track> _tracksList = [];

        private readonly List<Track> _fullTracksList = [];

        public IRelayCommand OpenTrackPageCommand { get; }
        public IRelayCommand OpenUserPageCommand { get; }
        public IRelayCommand PrependToQueueCommand { get; }
        public IAsyncRelayCommand AppendToQueueCommand { get; }
        public IAsyncRelayCommand PlayStationCommand { get; }
        public IAsyncRelayCommand DownloadCommand { get; }
        public IAsyncRelayCommand RefreshListCommand { get; }
        public IRelayCommand ClearFilterCommand { get; }

        private readonly string _baseHref;
        [ObservableProperty] private string _tracksFilter = string.Empty;
        [ObservableProperty] private Track _selectedTrack = Track.CreatePlaceholderTrack();
        [ObservableProperty] private bool _loadingLikedPlaylists;

        public ObservableCollection<Playlist> LikedPlaylists { get; } = [];

        private volatile bool _loadingItems;

        private readonly IHttpManager _httpManager;
        private readonly ProgramSettings _settings;
        private readonly SoundCloudDownloader _downloader;
        private readonly SoundCloudQueries? _queries;
        private readonly IDatabase _database;
        private readonly IMusicPlayer _musicPlayer;
        private string? _nextHref;
        private readonly Timer _filterDelay;

        public bool LoadAllLikedTracks => _settings.LoadAllLikedTracks;

        public LikedLibraryViewModel(IHttpManager httpManager, ProgramSettings settings, SoundCloudDownloader downloader, IDatabase database, IMusicPlayer musicPlayer, SoundCloudQueries? queries = null)
        {
            _httpManager = httpManager;
            _settings = settings;
            _downloader = downloader;
            _queries = queries;
            _database = database;
            _musicPlayer = musicPlayer;
            musicPlayer.TrackChanged += (t) => SelectedTrack = t;

            _filterDelay = new Timer(500) { AutoReset = false };
            _filterDelay.Elapsed += UpdateTracksList;

            OpenTrackPageCommand = new RelayCommand(OpenTrackPage);
            OpenUserPageCommand = new RelayCommand(OpenUserPage);
            PrependToQueueCommand = new RelayCommand(PrependToQueue);
            AppendToQueueCommand = new AsyncRelayCommand(AppendToQueue);
            PlayStationCommand = new AsyncRelayCommand(PlayStation);
            DownloadCommand = new AsyncRelayCommand(Download);
            RefreshListCommand = new AsyncRelayCommand(RefreshList);
            ClearFilterCommand = new RelayCommand(ClearFilter);

            _baseHref = $"users/{_settings.UserId}/track_likes?client_id={_settings.ClientId}&limit=24&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
            _nextHref = _baseHref;
        }

        private void ClearFilter()
        {
            TracksFilter = string.Empty;
        }

        partial void OnTracksFilterChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                TracksList.Clear();
                foreach (var track in _fullTracksList)
                    TracksList.Add(track);
                return;
            }

            _filterDelay.Stop();
            _filterDelay.Start();
        }

        private void UpdateTracksList(object? sender, ElapsedEventArgs e)
        {
            Debug.Print("filter timer elapsed");
            Dispatcher.UIThread.Post(() =>
            {
                Debug.Print("updating tracks list to match the filter");
                TracksList.Clear();

                foreach (var track in _fullTracksList.Where(x => x.FullLabel.Contains(TracksFilter, StringComparison.CurrentCultureIgnoreCase)))
                {
                    TracksList.Add(track);
                }
            });
        }

        public async Task DownloadTrackList()
        {
            if (_loadingItems)
                return;

            if (string.IsNullOrEmpty(_nextHref))
                return;

            _loadingItems = true;

            Debug.Print("downloading liked tracks list");

            QueryResponse<LikedTrack> tracks;
            var auth = _httpManager.DefaultClient.DefaultRequestHeaders.Authorization;
            _httpManager.DefaultClient.DefaultRequestHeaders.Authorization = null;
            try
            {
                using var response = await _httpManager.DefaultClient.GetAsync(_nextHref);
                response.EnsureSuccessStatusCode();

                tracks = await response.Content.ReadFromJsonAsync<QueryResponse<LikedTrack>>() ?? throw new Exception("Empty liked tracks response");

                //await File.WriteAllTextAsync("likedTracks.json", await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed retrieving liked tracks");
                _loadingItems = false;
                return;
            }
            finally
            {
                _httpManager.DefaultClient.DefaultRequestHeaders.Authorization = auth;
            }
            if (!string.IsNullOrEmpty(tracks.NextHref))
                _nextHref = tracks.NextHref + $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";
            else
                _nextHref = null;

            var newTracks = tracks.Collection
                .Select(x => x.Track)
                .Where(track => _fullTracksList.All(existing => existing.Id != track.Id))
                .ToList();
            _fullTracksList.AddRange(newTracks);

            foreach (var track in newTracks)
            {
                _database.AddTrack(track);
            }

            foreach (var track in newTracks.Where(x => x.FullLabel.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase)))
                TracksList.Add(track);

            Debug.Print($"track list contains {TracksList.Count} elements");

            _loadingItems = false;
        }

        /// <summary>
        /// Fully materializes the paginated liked library so filtering and
        /// shuffle playback never depend on scroll position.
        /// </summary>
        public async Task DownloadAllTrackList()
        {
            while (!string.IsNullOrEmpty(_nextHref))
            {
                var hrefBefore = _nextHref;
                var countBefore = _fullTracksList.Count;
                await DownloadTrackList();

                // An empty page can still advance pagination, but a failed
                // request leaves the cursor unchanged and must stop the loop.
                if (_fullTracksList.Count == countBefore && _nextHref == hrefBefore)
                    break;
            }
        }

        public async Task DownloadLikedPlaylists()
        {
            if (LikedPlaylists.Count > 0 || !_httpManager.AuthorizedClient.IsAuthorized)
                return;

            LoadingLikedPlaylists = true;
            try
            {
                if (_queries is null)
                    return;

                var (response, errorMessage) = await _queries.GetUsersLikedPlaylistsIds(System.Threading.CancellationToken.None);
                if (response is null)
                {
                    _logger.Warn("Failed retrieving liked playlists: {errorMessage}", errorMessage);
                    return;
                }

                var playlists = await _database.GetPlaylistsById(response.Collection, System.Threading.CancellationToken.None);
                foreach (var playlist in playlists)
                {
                    _database.AddPlaylist(playlist);
                    LikedPlaylists.Add(playlist);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed retrieving liked playlists");
            }
            finally
            {
                LoadingLikedPlaylists = false;
            }
        }

        public async Task PlayQueue(IEnumerable<Track> tracks)
        {
            await _musicPlayer.LoadNewQueue(tracks, DownloadMoreLikedTracks);
        }

        private void PrependToQueue()
        {
            if (SelectedTrack == null)
                return;

            //_musicPlayer.PrependToQueue(SelectedTrack, false, DownloadMore);
        }

        private async Task AppendToQueue()
        {
            if (SelectedTrack == null)
                return;

            await _musicPlayer.AddToQueue(SelectedTrack, DownloadMoreLikedTracks);
        }

        private async Task PlayStation()
        {
            if (SelectedTrack == null)
                return;

            await _musicPlayer.LoadNewQueue([SelectedTrack]);
        }

        private async Task RefreshList()
        {
            TracksList.Clear();
            _fullTracksList.Clear();
            _nextHref = _baseHref;
            if (LoadAllLikedTracks)
                await DownloadAllTrackList();
            else
                await DownloadTrackList();

            LikedPlaylists.Clear();
            await DownloadLikedPlaylists();
        }

        private async Task Download()
        {
            if (SelectedTrack == null)
                return;
            var trackToDownload = SelectedTrack;

            var notif = new Notification($"Downloading {trackToDownload.FullLabel}", "Downloading started...", NotificationType.Information, TimeSpan.Zero);
            NotificationManager.Show(notif);

            bool success = false;
            string errorMessage = string.Empty;
            try
            {
                (success, errorMessage) = await _downloader.SaveTrackLocally(trackToDownload, (message) =>
                {
                    notif.Message = message;
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed downloading the track {0}", trackToDownload.Title);
            }

            if (success)
            {
                notif.Type = NotificationType.Success;
                notif.Expiration = TimeSpan.FromSeconds(5);
                notif.Message = "Downloaded!";
            }
            else
            {
                notif.Type = NotificationType.Error;
                notif.Expiration = TimeSpan.Zero;
                notif.Title = $"Failed downloading {trackToDownload.FullLabel}";
                notif.Message = errorMessage;
                _logger.Error("Failed downloading a track: {errorMessage}", errorMessage);
            }
        }

        private async Task<IEnumerable<Track>> DownloadMoreLikedTracks()
        {
            var startIndex = TracksList.Count;
            await DownloadTrackList();
            return TracksList.Skip(startIndex);
        }

        public void OpenTrackPage()
        {
            if (SelectedTrack is null)
                return;

            Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, SelectedTrack);
        }

        public void OpenUserPage()
        {
            if (SelectedTrack is null)
                return;

            Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, SelectedTrack.User);
        }
    }
}
