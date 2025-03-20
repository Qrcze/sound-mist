using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundMist.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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

    private readonly IMusicPlayer _musicPlayer;
    private readonly ILogger _logger;
    private readonly ProgramSettings _settings;

    public DownloadedViewModel(IMusicPlayer musicPlayer, ILogger logger, ProgramSettings settings)
    {
        _musicPlayer = musicPlayer;
        _logger = logger;
        _settings = settings;
        AppendToQueueCommand = new AsyncRelayCommand(AppendToQueue);
        PlayStationCommand = new AsyncRelayCommand(PlayStation);
        PrependToQueueCommand = new RelayCommand(PrependToQueue);

        LoadDowloadedTracks();
    }

    private void LoadDowloadedTracks()
    {
        if (!Directory.Exists(Globals.LocalDownloadsPath))
            return;

        foreach (var jsonPath in Directory.GetFiles(Globals.LocalDownloadsPath, "*.json"))
        {
            string trackLabel = Path.GetFileNameWithoutExtension(jsonPath);
            string mp3Path = $"{Globals.LocalDownloadsPath}/{trackLabel}.mp3";

            //remove unused json files
            if (!File.Exists(mp3Path))
            {
                File.Delete(jsonPath);
                _logger.Info($"removed unused downloaded json file for track: {trackLabel}");
                continue;
            }

            try
            {
                var json = File.ReadAllText(jsonPath);
                var track = JsonSerializer.Deserialize<Track>(json);
                TracksList.Add(track);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed reading json file for track: {trackLabel}; {ex.Message}");
                TracksList.Add(Track.CreatePlaceholderTrack("[Failed loading track]", trackLabel));
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

        await _musicPlayer.PlayNewQueue([SelectedTrack]);
    }

    private void PrependToQueue()
    {
        if (SelectedTrack == null)
            return;

        Debug.Print("TODO");
    }

    public async Task PlayQueue(IEnumerable<Track> tracks)
    {
        await _musicPlayer.PlayNewQueue(tracks);
    }
}