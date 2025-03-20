using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundMist.Models
{
    public class ProgramSettings
    {
        private const string SettingsFilePath = "settings.json";

        private bool _loaded; // a guard, otherwise json uses property to SetPropertyAndSave and it can break stuff

        private string? _authToken;
        private float _volume = 1;
        private bool _autoplayStationOnLastTrack = true;
        private bool _shuffle;
        private Track? _lastTrack;
        private MainViewTab _startingTabIndex = MainViewTab.LikedTracks;

        public static ProgramSettings Load()
        {
            ProgramSettings? settings = null;
            string json;

            if (File.Exists(SettingsFilePath))
            {
                json = File.ReadAllText(SettingsFilePath);

                try
                {
                    settings = JsonSerializer.Deserialize<ProgramSettings>(json);
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error($"Failed reading settings json: {ex.Message}");
                    File.Copy(SettingsFilePath, SettingsFilePath + ".old", overwrite: true);
                }
            }

            if (settings == null)
            {
                settings = new();
                json = JsonSerializer.Serialize(settings);
                File.WriteAllText(SettingsFilePath, json);
            }

            settings._loaded = true;

            return settings;
        }

        [JsonIgnore] public string? ClientId { get; set; }

        /// <summary> Used in some http requests, as a way to identify the anonymous user; different from actual UserId, which is stored in SoundCloud database </summary>
        [JsonIgnore] public string? AnonymousUserId { get; set; }

        [JsonIgnore] public int AppVersion { get; set; }

        [JsonIgnore] public int? UserId { get; set; }

        public string? AuthToken { get => _authToken; set => SetPropertyAndSave(ref _authToken, value); }
        public float Volume { get => _volume; set => SetPropertyAndSave(ref _volume, value); }
        public bool AutoplayStationOnLastTrack { get => _autoplayStationOnLastTrack; set => SetPropertyAndSave(ref _autoplayStationOnLastTrack, value); }
        public bool Shuffle { get => _shuffle; set => SetPropertyAndSave(ref _shuffle, value); }
        public Track? LastTrack { get => _lastTrack; set => SetPropertyAndSave(ref _lastTrack, value); }
        public MainViewTab StartingTabIndex { get => _startingTabIndex; set => SetPropertyAndSave(ref _startingTabIndex, value); }

        /// <summary>
        /// Do not modify directly; use <see cref="AddBlockedUser(User)"/> instead
        /// </summary>
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public HashSet<BlockedEntry> BlockedUsers { get; } = [];

        /// <summary>
        /// Do not modify directly; use <see cref="AddBlockedTrack(Track)"/> instead
        /// </summary>
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public HashSet<BlockedEntry> BlockedTracks { get; } = [];

        public void AddBlockedUser(User user)
        {
            if (BlockedUsers.Add(new(user.Id, user.Username)))
            {
                var json = JsonSerializer.Serialize(this);
                File.WriteAllText(SettingsFilePath, json);
            }
        }

        public void AddBlockedTrack(Track track)
        {
            if (BlockedTracks.Add(new(track.Id, track.FullLabel)))
            {
                var json = JsonSerializer.Serialize(this);
                File.WriteAllText(SettingsFilePath, json);
            }
        }

        public bool IsBlockedUser(Track track) => BlockedUsers.Any(x => x.Id == track.UserId);

        public bool IsBlockedTrack(Track track) => BlockedUsers.Any(x => x.Id == track.Id);

        private void SetPropertyAndSave<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            if (_loaded)
            {
                var json = JsonSerializer.Serialize(this);
                File.WriteAllText(SettingsFilePath, json);
            }
        }
    }
}