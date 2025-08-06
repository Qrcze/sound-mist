using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Helpers;
using SoundMist.Models;
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
    [ObservableProperty] private string _tracksFilter = string.Empty;
    [ObservableProperty] private Track? _selectedTrack;

    public ObservableCollection<Track> TracksList { get; } = [];

    public IAsyncRelayCommand AppendToQueueCommand { get; }
    public IAsyncRelayCommand PlayStationCommand { get; }
    public IRelayCommand PrependToQueueCommand { get; }

    private readonly SoundCloudQueries _queries;
    private readonly IMusicPlayer _musicPlayer;
    private readonly ILogger _logger;

    public DownloadedViewModel(SoundCloudQueries queries, IMusicPlayer musicPlayer, ILogger logger)
    {
        _queries = queries;
        _musicPlayer = musicPlayer;
        _logger = logger;
        AppendToQueueCommand = new AsyncRelayCommand(AppendToQueue);
        PlayStationCommand = new AsyncRelayCommand(PlayStation);
        PrependToQueueCommand = new RelayCommand(PrependToQueue);

        Task.Run(LoadDowloadedTracks);
    }

    private async Task LoadDowloadedTracks()
    {
        if (!Directory.Exists(Globals.LocalDownloadsPath))
            return;

        string[] idFiles = Directory.GetFiles(Globals.LocalDownloadsPath, "*.id");
        List<(long id, string label)> ids = new(idFiles.Length);

        foreach (var idPath in idFiles)
        {
            string trackLabel = Path.GetFileNameWithoutExtension(idPath);
            string mp3Path = $"{Globals.LocalDownloadsPath}/{trackLabel}.mp3";

            //remove unused json files
            if (!File.Exists(mp3Path))
            {
                File.Delete(idPath);
                _logger.Info($"removed unused downloaded id file for track: {trackLabel}");
                continue;
            }

            string idString = File.ReadAllText(idPath);
            if (long.TryParse(idString, out long id))
                ids.Add((id, trackLabel));
            else
            {
                _logger.Warn($"failed reading track id for the track {trackLabel}: \"{idString}\"");
                File.Delete(idPath);
            }
        }

        var tracksData = await _queries.GetTracksById(ids.Select(x => x.id));

        foreach (var filePath in Directory.GetFiles(Globals.LocalDownloadsPath, "*.mp3"))
        {
            string trackLabel = Path.GetFileNameWithoutExtension(filePath);
            (long trackId, _) = ids.FirstOrDefault(x => x.label == trackLabel);

            var trackData = tracksData.FirstOrDefault(x => x.FullLabel == trackLabel);
            if (trackData is not null)
            {
                TracksList.Add(trackData);
            }
            else
            {
                using var tags = TagLib.File.Create(filePath);
                string artist = tags.Tag.FirstAlbumArtist;
                string title = tags.Tag.Title;
                TracksList.Add(Track.CreatePlaceholderTrack(artist, title));
            }
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