using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using SoundMist.Helpers;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

internal partial class DownloadedViewModel : ViewModelBase
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty] private string _tracksFilter = string.Empty;
    [ObservableProperty] private Track _selectedTrack = Track.CreatePlaceholderTrack();

    public ObservableCollection<Track> TracksList { get; } = [];

    public IAsyncRelayCommand AppendToQueueCommand { get; }
    public IAsyncRelayCommand PlayStationCommand { get; }
    public IRelayCommand PrependToQueueCommand { get; }
    public IAsyncRelayCommand RefreshListCommand { get; }

    private readonly SoundCloudQueries _queries;
    private readonly IMusicPlayer _musicPlayer;

    public DownloadedViewModel(SoundCloudQueries queries, IMusicPlayer musicPlayer)
    {
        _queries = queries;
        _musicPlayer = musicPlayer;
        AppendToQueueCommand = new AsyncRelayCommand(AppendToQueue);
        PlayStationCommand = new AsyncRelayCommand(PlayStation);
        PrependToQueueCommand = new RelayCommand(PrependToQueue);
        RefreshListCommand = new AsyncRelayCommand(LoadDowloadedTracks);

        Task.Run(LoadDowloadedTracks);
    }

    private async Task LoadDowloadedTracks()
    {
        var previousTracks = new List<Track>(TracksList);
        TracksList.Clear();
        foreach (var item in previousTracks)
            item.ArtworkImage?.Dispose();

        if (!Directory.Exists(Globals.LocalDownloadsPath))
            return;

        foreach (var filePath in Directory.GetFiles(Globals.LocalDownloadsPath, "*.mp3").OrderByDescending(x => new FileInfo(x).CreationTimeUtc))
        {
            string trackLabel = Path.GetFileNameWithoutExtension(filePath);

            using var tags = TagLib.File.Create(filePath);
            long trackId = tags.Tag.Track;
            string artist = tags.Tag.FirstAlbumArtist;
            string title = tags.Tag.Title;
            int duration = (int)tags.Properties.Duration.TotalMilliseconds;
            var picture = tags.Tag.Pictures.FirstOrDefault();
            Bitmap? artwork = null;

            if (picture != null)
            {
                using var pictureStream = new MemoryStream(picture.Data.Data);
                using var fullImage = new Bitmap(pictureStream);
                artwork = fullImage.CreateScaledBitmap(new(200, 200));
            }

            Track track = new()
            {
                Id = trackId,
                Title = title,
                User = new()
                {
                    Username = artist,
                },
                FullDuration = duration,
                ArtworkImage = artwork,
            };

            TracksList.Add(track);
        }
    }

    private async Task AppendToQueue()
    {
        if (SelectedTrack == null)
            return;

        await _musicPlayer.AddToQueue(SelectedTrack);
    }

    private async Task PlayStation()
    {
        if (SelectedTrack == null)
            return;

        await _musicPlayer.LoadNewQueue([SelectedTrack]);
    }

    private void PrependToQueue()
    {
        if (SelectedTrack == null)
            return;

        Debug.Print("TODO");
    }

    public async Task PlayQueue(IEnumerable<Track> tracks)
    {
        await _musicPlayer.LoadNewQueue(tracks);
    }
}