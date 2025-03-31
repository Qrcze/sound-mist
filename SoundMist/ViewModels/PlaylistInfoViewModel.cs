using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels
{
    public partial class PlaylistInfoViewModel : ViewModelBase
    {
        [ObservableProperty] private Playlist? _playlist;
        [ObservableProperty] private bool _loadingView;
        [ObservableProperty] private bool _isLiked;
        [ObservableProperty] private Track? _selectedTrack;

        private CancellationTokenSource? _tokenSource;
        private readonly HttpClient _httpClient;
        private readonly AuthorizedHttpClient _authorizedHttpClient;
        private readonly ProgramSettings _settings;
        private readonly ILogger _logger;
        private readonly IMusicPlayer _musicPlayer;

        public ObservableCollection<Track> Tracks { get; } = [];

        public IRelayCommand OpenPlaylistInBrowserCommand { get; }
        public IRelayCommand OpenTrackInBrowserCommand { get; }
        public IRelayCommand OpenTrackInfoCommand { get; }
        public IRelayCommand OpenUserInfoCommand { get; }

        public PlaylistInfoViewModel(HttpClient httpClient, AuthorizedHttpClient authorizedHttpClient, ProgramSettings settings, ILogger logger, IMusicPlayer musicPlayer)
        {
            _httpClient = httpClient;
            _authorizedHttpClient = authorizedHttpClient;
            _settings = settings;
            _logger = logger;
            _musicPlayer = musicPlayer;
            Mediator.Default.Register(MediatorEvent.OpenPlaylistInfo, Open);

            OpenPlaylistInBrowserCommand = new RelayCommand(OpenPlaylistInBrowser);
            OpenTrackInBrowserCommand = new RelayCommand(OpenTrackInBrowser);
            OpenTrackInfoCommand = new RelayCommand(OpenTrackInfo);
            OpenUserInfoCommand = new RelayCommand(OpenUserInfo);
        }

        private void OpenPlaylistInBrowser()
        {
            if (Playlist?.PermalinkUrl is null)
                return;

            Process.Start(new ProcessStartInfo(Playlist.PermalinkUrl) { UseShellExecute = true });
        }

        private void OpenTrackInBrowser()
        {
            if (SelectedTrack?.PermalinkUrl is null)
                return;

            Process.Start(new ProcessStartInfo(SelectedTrack.PermalinkUrl) { UseShellExecute = true });
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

            if (obj is not Playlist playlist)
                throw new ArgumentException($"{MediatorEvent.OpenPlaylistInfo} mediator event is expected to provide a {nameof(Playlist)} object as parameter");

            LoadingView = true;
            Playlist = playlist;
            Tracks.Clear();

            Task.Run(async () =>
            {
                try
                {
                    if (_authorizedHttpClient.IsAuthorized)
                    {
                        var (ids, message) = await SoundCloudQueries.GetUsersLikedPlaylistsIds(_authorizedHttpClient, _settings.ClientId, _settings.AppVersion, token);
                        if (ids is null)
                        {
                            _logger.Error($"Failed getting liked playlists list: {message}");
                            NotificationManager.Show(new("Failed getting liked playlists", message, Avalonia.Controls.Notifications.NotificationType.Warning));
                        }
                        else
                        {
                            if (ids.Contains(Playlist.Id))
                                IsLiked = true;
                        }
                    }

                    var neededTracks = Playlist.Tracks.Where(x => x.User is null).Select(x => x.Id);
                    var tracks = await SoundCloudQueries.GetTracksById(_httpClient, _settings.ClientId, _settings.AppVersion, neededTracks, token);

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