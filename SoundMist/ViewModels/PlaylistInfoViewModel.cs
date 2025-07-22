using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels
{
    public partial class PlaylistInfoViewModel : ViewModelBase
    {
        [ObservableProperty] private Playlist? _playlist;
        [ObservableProperty] private bool _loadingView;
        [ObservableProperty] private bool _isLiked;
        [ObservableProperty] private bool _showFullImage;
        [ObservableProperty] private Track? _selectedTrack;

        private CancellationTokenSource? _tokenSource;
        private readonly IHttpManager _httpManager;
        private readonly SoundCloudQueries _queries;
        private readonly IDatabase _database;
        private readonly ProgramSettings _settings;
        private readonly ILogger _logger;
        private readonly IMusicPlayer _musicPlayer;
        private readonly History _history;

        public ObservableCollection<Track> Tracks { get; } = [];

        public IRelayCommand OpenPlaylistInBrowserCommand { get; }
        public IRelayCommand OpenTrackInBrowserCommand { get; }
        public IRelayCommand OpenTrackInfoCommand { get; }
        public IRelayCommand OpenUserInfoCommand { get; }
        public IRelayCommand ToggleFullImageCommand { get; }

        public PlaylistInfoViewModel(IHttpManager httpManager, SoundCloudQueries queries, IDatabase database, ProgramSettings settings, ILogger logger, IMusicPlayer musicPlayer, History history)
        {
            _httpManager = httpManager;
            _queries = queries;
            _database = database;
            _settings = settings;
            _logger = logger;
            _musicPlayer = musicPlayer;
            _history = history;
            Mediator.Default.Register(MediatorEvent.OpenPlaylistInfo, Open);

            OpenPlaylistInBrowserCommand = new RelayCommand(OpenPlaylistInBrowser);
            OpenTrackInBrowserCommand = new RelayCommand(OpenTrackInBrowser);
            OpenTrackInfoCommand = new RelayCommand(OpenTrackInfo);
            OpenUserInfoCommand = new RelayCommand(OpenUserInfo);
            ToggleFullImageCommand = new RelayCommand(() => ShowFullImage = !ShowFullImage);
        }

        private void OpenPlaylistInBrowser()
        {
            if (Playlist?.PermalinkUrl is null)
                return;

            SystemHelpers.OpenInBrowser(Playlist.PermalinkUrl);
        }

        private void OpenTrackInBrowser()
        {
            if (SelectedTrack?.PermalinkUrl is null)
                return;

            SystemHelpers.OpenInBrowser(SelectedTrack.PermalinkUrl);
        }

        private void OpenTrackInfo()
        {
            if (SelectedTrack is null)
                return;

            Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, SelectedTrack);
        }

        private void OpenUserInfo()
        {
            if (SelectedTrack?.User is null)
                return;

            Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, SelectedTrack.User);
        }

        private void Open(object? obj)
        {
            _tokenSource?.Cancel();
            _tokenSource = new CancellationTokenSource();
            var token = _tokenSource.Token;

            ShowFullImage = false;

            if (obj is not Playlist playlist)
                throw new ArgumentException($"{MediatorEvent.OpenPlaylistInfo} mediator event is expected to provide a {nameof(Playlist)} object as parameter");

            if (playlist == Playlist)
                return;

            LoadingView = true;
            Playlist = playlist;
            Tracks.Clear();

            _history.AddPlaylistInfoHistory(playlist);

            Task.Run(async () =>
            {
                try
                {
                    if (_httpManager.AuthorizedClient.IsAuthorized)
                    {
                        var (response, message) = await _queries.GetUsersLikedPlaylistsIds(token);
                        if (response is null)
                        {
                            _logger.Error($"Failed getting liked playlists list: {message}");
                            Dispatcher.UIThread.Post(() => NotificationManager.Show(new("Failed getting liked playlists", message, Avalonia.Controls.Notifications.NotificationType.Warning)));
                        }
                        else
                        {
                            if (response.Collection.Contains(Playlist.Id))
                                IsLiked = true;
                        }
                    }

                    var neededTracks = Playlist.Tracks.Except(Playlist.FirstFiveTracks).Select(x => x.Id);
                    var tracks = await _database.GetTracksById(neededTracks, token);

                    if (token.IsCancellationRequested)
                        return;

                    foreach (var track in Playlist.FirstFiveTracks)
                    {
                        if (token.IsCancellationRequested)
                            return;
                        Tracks.Add(track);
                    }

                    foreach (var track in tracks)
                    {
                        if (token.IsCancellationRequested)
                            return;
                        Tracks.Add(track);
                    }

                    LoadingView = false;
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }

        internal void PlayFromIndex(int selectedIndex)
        {
            _musicPlayer.LoadNewQueue(Tracks.Skip(selectedIndex));
        }
    }
}