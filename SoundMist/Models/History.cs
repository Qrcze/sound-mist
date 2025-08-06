using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundMist.Models;

public class HistoryChangedEventArgs : EventArgs
{
    public History.List List { get; }
    public long NewId { get; }
    public long? RemovedId { get; }
    public object NewObject { get; }

    public HistoryChangedEventArgs(History.List list, long newId, long? removedId, object newObject)
    {
        List = list;
        NewId = newId;
        RemovedId = removedId;
        NewObject = newObject;
    }
}

public class History
{
    public enum List
    {
        PlayHistory,
        OnlinePlayHistory,
        TracksHistory,
        UsersHistory,
        PlaylistsHistory,
    }

    [JsonIgnore] public IReadOnlyCollection<long> PlayHistory => _playHistory;
    [JsonIgnore] public IReadOnlyCollection<long> TracksHistory => _tracksHistory;
    [JsonIgnore] public IReadOnlyCollection<long> UsersHistory => _usersHistory;
    [JsonIgnore] public IReadOnlyCollection<long> PlaylistsHistory => _playlistsHistory;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(PlayHistory))]
    [JsonInclude]
    private readonly LinkedList<long> _playHistory = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(TracksHistory))]
    [JsonInclude]
    private readonly LinkedList<long> _tracksHistory = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(UsersHistory))]
    [JsonInclude]
    private readonly LinkedList<long> _usersHistory = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(PlaylistsHistory))]
    [JsonInclude]
    private readonly LinkedList<long> _playlistsHistory = [];

    public event EventHandler<HistoryChangedEventArgs>? HistoryChanged;

    private ProgramSettings _settings = null!;

    public History(ProgramSettings settings) => _settings = settings;

    [JsonConstructor]
    protected History() { }

    public static History Load(ProgramSettings settings)
    {
        if (!File.Exists(Globals.HistoryFilePath))
        {
            Debug.Print("creating new history file");
            var history = new History(settings);
            File.WriteAllText(Globals.HistoryFilePath, JsonSerializer.Serialize(history));
            return history;
        }

        try
        {
            var json = File.ReadAllText(Globals.HistoryFilePath);
            var history = JsonSerializer.Deserialize<History>(json)!;
            history._settings = settings;
            return history;
        }
        catch (JsonException ex)
        {
            FileLogger.Instance.Error($"Failed deserializing the history json: {ex.Message}");
            string oldFile = $"{Globals.HistoryFilePath}.old";
            File.Delete(oldFile);
            File.Move(Globals.HistoryFilePath, oldFile);
            return new(settings);
        }
        catch (Exception ex)
        {
            FileLogger.Instance.Error($"Exception while reading the history json: {ex.Message}");
            string oldFile = $"{Globals.HistoryFilePath}.old";
            File.Delete(oldFile);
            File.Move(Globals.HistoryFilePath, oldFile);
            return new(settings);
        }
    }

    public void AddPlayedHistory(Track track) => PushToLimitAndSave(_playHistory, List.PlayHistory, track.Id, track);

    public void AddTrackInfoHistory(Track track) => PushToLimitAndSave(_tracksHistory, List.TracksHistory, track.Id, track);

    public void AddUserInfoHistory(User user) => PushToLimitAndSave(_usersHistory, List.UsersHistory, user.Id, user);

    public void AddPlaylistInfoHistory(Playlist playlist) => PushToLimitAndSave(_playlistsHistory, List.PlaylistsHistory, playlist.Id, playlist);

    void PushToLimitAndSave(LinkedList<long> list, List listId, long id, object addedObject)
    {
        if (list.First?.Value == id)
            return;

        list.Remove(id);

        list.AddFirst(id);

        long? removed = null;
        if (list.Count > _settings.HistoryLimit)
        {
            removed = list.Last?.Value;
            list.RemoveLast();
        }

        HistoryChanged?.Invoke(this, new(listId, id, removed, addedObject));

        var json = JsonSerializer.Serialize(this);
        File.WriteAllText(Globals.HistoryFilePath, json);
    }
}