using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NLog;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.ViewModels;

internal partial class DownloadedViewModel : ViewModelBase
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    [ObservableProperty] private string _tracksFilter = string.Empty;
    [ObservableProperty] private Track _selectedTrack = Track.CreatePlaceholderTrack();
    [ObservableProperty] private bool _sortDescending = true;
    [ObservableProperty] private string _selectedOrdering = "Date Added";

    public string[] Orderings { get; } = [
        "Date Added",
        "Track Name",
        "Uploader/Artist Name",
        "Duration",
    ];

    public ObservableCollection<Track> TracksList { get; } = [];
    private readonly List<Track> _tracksList = new(500);

    public IAsyncRelayCommand AppendToQueueCommand { get; }
    public IAsyncRelayCommand PlayStationCommand { get; }
    public IRelayCommand PrependToQueueCommand { get; }
    public IRelayCommand ClearFilterCommand { get; }
    public IAsyncRelayCommand RefreshListCommand { get; }

    private readonly IMusicPlayer _musicPlayer;

    public DownloadedViewModel(IMusicPlayer musicPlayer)
    {
        _musicPlayer = musicPlayer;
        AppendToQueueCommand = new AsyncRelayCommand(AppendToQueue);
        PlayStationCommand = new AsyncRelayCommand(PlayStation);
        PrependToQueueCommand = new RelayCommand(PrependToQueue);
        ClearFilterCommand = new RelayCommand(() => TracksFilter = string.Empty);
        RefreshListCommand = new AsyncRelayCommand(LoadDownloadedTracks);

        RefreshListCommand.ExecuteAsync(null);
        RefreshListCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadDownloadedTracks(CancellationToken token)
    {
        TracksList.Clear();
        foreach (var item in _tracksList)
            item.ArtworkImage?.Dispose();
        _tracksList.Clear();

        if (!Directory.Exists(Globals.LocalDownloadsPath))
            return;

        foreach (var filePath in Directory.GetFiles(Globals.LocalDownloadsPath, "*.mp3").OrderByDescending(x => new FileInfo(x).CreationTimeUtc))
        {
            if (token.IsCancellationRequested) return;
            var track = await Task.Run(() => LoadTrack(filePath));
            if (token.IsCancellationRequested) return;

            if (string.IsNullOrWhiteSpace(TracksFilter) || track.Title.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase) || track.ArtistName.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase))
                TracksList.Add(track);
            _tracksList.Add(track);
        }
    }

    private static Task<Track> LoadTrack(string filePath)
    {
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

        return Task.FromResult(track);
    }

    partial void OnTracksFilterChanged(string value)
    {
        TracksList.Clear();

        if (string.IsNullOrEmpty(value))
        {
            foreach (var track in _tracksList)
                TracksList.Add(track);

            return;
        }

        foreach (var track in _tracksList)
            if (track.Title.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase) || track.ArtistName.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase))
                TracksList.Add(track);
    }

    partial void OnSortDescendingChanged(bool value)
    {
        OnSelectedOrderingChanged(SelectedOrdering);
    }

    partial void OnSelectedOrderingChanged(string value)
    {
        ShowOrderTracks(GetTracksOrderedBy(value));
    }

    IEnumerable<Track> GetTracksOrderedBy(string value)
    {
        switch (value)
        {
            case "Date Added":
                if (SortDescending)
                    return _tracksList.OrderByDescending(x => new FileInfo(x.LocalFilePath).CreationTimeUtc);
                else
                    return _tracksList.OrderBy(x => new FileInfo(x.LocalFilePath).CreationTimeUtc);

            case "Track Name":
                if (SortDescending)
                    return _tracksList.OrderByDescending(x => x.Title);
                else
                    return _tracksList.OrderBy(x => x.Title);

            case "Uploader/Artist Name":
                if (SortDescending)
                    return _tracksList.OrderByDescending(x => x.ArtistName);
                else
                    return _tracksList.OrderBy(x => x.ArtistName);

            case "Duration":
                if (SortDescending)
                    return _tracksList.OrderByDescending(x => x.FullDuration);
                else
                    return _tracksList.OrderBy(x => x.FullDuration);

            default:
                _logger.Warn("Unexpected tracks order: {0}", value);
                return _tracksList;
        }
    }

    void ShowOrderTracks(IEnumerable<Track> orderedTracks)
    {
        TracksList.Clear();
        if (string.IsNullOrWhiteSpace(TracksFilter))
            foreach (var track in orderedTracks)
                TracksList.Add(track);
        else
            foreach (var track in orderedTracks)
                if (track.Title.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase) || track.ArtistName.Contains(TracksFilter, StringComparison.InvariantCultureIgnoreCase))
                    TracksList.Add(track);
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