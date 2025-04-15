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
    public int NewId { get; }
    public int? RemovedId { get; }
    public object NewObject { get; }

    public HistoryChangedEventArgs(History.List list, int newId, int? removedId, object newObject)
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
        TracksHistory,
        UsersHistory,
        PlaylistsHistory,
    }

    private const string FilePath = "history.json";

    public int Limit { get; set; } = 50;

    [JsonIgnore] public IReadOnlyCollection<int> PlayHistory => _playHistory;
    [JsonIgnore] public IReadOnlyCollection<int> TracksHistory => _tracksHistory;
    [JsonIgnore] public IReadOnlyCollection<int> UsersHistory => _usersHistory;
    [JsonIgnore] public IReadOnlyCollection<int> PlaylistsHistory => _playlistsHistory;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(PlayHistory))]
    [JsonInclude]
    private readonly LinkedList<int> _playHistory = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(TracksHistory))]
    [JsonInclude]
    private readonly LinkedList<int> _tracksHistory = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(UsersHistory))]
    [JsonInclude]
    private readonly LinkedList<int> _usersHistory = [];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName(nameof(PlaylistsHistory))]
    [JsonInclude]
    private readonly LinkedList<int> _playlistsHistory = [];

    public event EventHandler<HistoryChangedEventArgs>? HistoryChanged;

    public static History Load()
    {
        if (!File.Exists(FilePath))
        {
            Debug.Print("creating new history file");
            File.Create(FilePath);
            return new();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var history = JsonSerializer.Deserialize<History>(json)!;
            return history;
        }
        catch (JsonException ex)
        {
            FileLogger.Instance.Error($"Failed deserializing the history json: {ex.Message}");
            string oldFile = $"{FilePath}.old";
            File.Delete(oldFile);
            File.Move(FilePath, oldFile);
            return new();
        }
        catch (Exception ex)
        {
            FileLogger.Instance.Error($"Exception while reading the history json: {ex.Message}");
            string oldFile = $"{FilePath}.old";
            File.Delete(oldFile);
            File.Move(FilePath, oldFile);
            return new();
        }
    }

    public void AddPlayedHistory(Track track) => PushToLimitAndSave(_playHistory, List.PlayHistory, track.Id, track);

    public void AddTrackInfoHistory(Track track) => PushToLimitAndSave(_tracksHistory, List.TracksHistory, track.Id, track);

    public void AddUserInfoHistory(User user) => PushToLimitAndSave(_usersHistory, List.UsersHistory, user.Id, user);

    public void AddPlaylistInfoHistory(Playlist playlist) => PushToLimitAndSave(_playlistsHistory, List.PlaylistsHistory, playlist.Id, playlist);

    void PushToLimitAndSave(LinkedList<int> list, List listId, int id, object addedObject)
    {
        if (list.First?.Value == id)
            return;

        list.Remove(id);

        list.AddFirst(id);

        int? removed = null;
        if (list.Count > Limit)
        {
            removed = list.Last?.Value;
            list.RemoveLast();
        }

        HistoryChanged?.Invoke(this, new(listId, id, removed, addedObject));

        var json = JsonSerializer.Serialize(this);
        File.WriteAllText(FilePath, json);
    }
}