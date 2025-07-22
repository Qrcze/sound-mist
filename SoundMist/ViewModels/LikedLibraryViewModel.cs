using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

namespace SoundMist.ViewModels
{
    public partial class LikedLibraryViewModel : ViewModelBase
    {
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
        [ObservableProperty] private Track? _selectedTrack;

        private volatile bool _loadingItems;

        private readonly IHttpManager _httpManager;
        private readonly ProgramSettings _settings;
        private readonly SoundCloudDownloader _downloader;
        private readonly IDatabase _database;
        private readonly IMusicPlayer _musicPlayer;
        private readonly ILogger _logger;
        private string? _nextHref;
        private readonly Timer _filterDelay;

        public LikedLibraryViewModel(IHttpManager httpManager, ProgramSettings settings, SoundCloudDownloader downloader, IDatabase database, IMusicPlayer musicPlayer, ILogger logger)
        {
            _httpManager = httpManager;
            _settings = settings;
            _downloader = downloader;
            _database = database;
            _musicPlayer = musicPlayer;
            _logger = logger;
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

                tracks = await response.Content.ReadFromJsonAsync<QueryResponse<LikedTrack>>() ?? throw new Exception("lol");

                //await File.WriteAllTextAsync("likedTracks.json", await response.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed retrieving liked tracks: {ex.Message}");
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

            var newTracks = tracks.Collection.Select(x => x.Track);
            _fullTracksList.AddRange(newTracks);

            foreach (var track in newTracks)
                _database.AddTrack(track);

            foreach (var track in newTracks.Where(x => x.FullLabel.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase)))
                TracksList.Add(track);

            Debug.Print($"track list contains {TracksList.Count} elements");

            _loadingItems = false;
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
            _nextHref = _baseHref;
            await DownloadTrackList();
        }

        private async Task Download()
        {
            if (SelectedTrack == null)
                return;

            var notif = new Notification($"Downloading {SelectedTrack.FullLabel}", "Downloading started...", NotificationType.Information, TimeSpan.Zero);
            NotificationManager.Show(notif);

            (bool success, string errorMessage) = await _downloader.SaveTrackLocally(SelectedTrack, (message) =>
            {
                notif.Message = message;
            });

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
                notif.Title = $"Failed downloading {SelectedTrack.FullLabel}";
                notif.Message = errorMessage;
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