using Avalonia;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundMist.Models
{
    public enum AppColorTheme
    {
        System,
        Light,
        Dark
    }

    public class ProgramSettings
    {
        private const string SettingsFilePath = "settings.json";

        private bool _settingsInitialized; // a guard, otherwise json uses property to SetPropertyAndSave and it can break stuff

        private string? _authToken;
        private float _volume = 1;
        private bool _autoplayStationOnLastTrack = true;
        private bool _shuffle;
        private int? _lastTrackId;
        private MainViewTab _startingTabIndex = MainViewTab.LikedTracks;
        private bool _startPlayingOnLaunch;
        private AppColorTheme _appColorTheme;
        private int _historyLimit = 50;

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

            settings._settingsInitialized = true;

            return settings;
        }

        [JsonIgnore] public string ClientId { get; set; } = string.Empty;

        /// <summary> Used in some http requests, as a way to identify the anonymous user; different from actual UserId, which is stored in SoundCloud database </summary>
        [JsonIgnore] public string? AnonymousUserId { get; set; }

        [JsonIgnore] public int AppVersion { get; set; }

        [JsonIgnore] public int? UserId { get; set; }

        public string? AuthToken { get => _authToken; set => SetPropertyAndSave(ref _authToken, value); }
        public float Volume { get => _volume; set => SetPropertyAndSave(ref _volume, value); }
        public bool AutoplayStationOnLastTrack { get => _autoplayStationOnLastTrack; set => SetPropertyAndSave(ref _autoplayStationOnLastTrack, value); }
        public bool Shuffle { get => _shuffle; set => SetPropertyAndSave(ref _shuffle, value); }
        public int? LastTrackId { get => _lastTrackId; set => SetPropertyAndSave(ref _lastTrackId, value); }
        public MainViewTab StartingTabIndex { get => _startingTabIndex; set => SetPropertyAndSave(ref _startingTabIndex, value); }
        public bool StartPlayingOnLaunch { get => _startPlayingOnLaunch; set => SetPropertyAndSave(ref _startPlayingOnLaunch, value); }
        public int HistoryLimit { get => _historyLimit; set => SetPropertyAndSave(ref _historyLimit, value); }

        public AppColorTheme AppColorTheme
        {
            get => _appColorTheme;
            set
            {
                if (_appColorTheme == value)
                    return;

                _appColorTheme = value;

                switch (value)
                {
                    case AppColorTheme.System:
                        Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
                        break;

                    case AppColorTheme.Light:
                        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
                        break;

                    case AppColorTheme.Dark:
                        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
                        break;

                    default:
                        break;
                }

                if (_settingsInitialized)
                    SaveSettingsFile();
            }
        }

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
                SaveSettingsFile();
        }

        public bool IsBlockedUser(Track track)
        {
            if (track.User is null || !track.UserId.HasValue)
                return false;

            return BlockedUsers.Contains(new(track.UserId.Value, track.User.Username));
        }

        public void RemoveBlockedUser(BlockedEntry entry)
        {
            if (BlockedUsers.Remove(entry))
                SaveSettingsFile();
        }

        public void AddBlockedTrack(Track track)
        {
            if (BlockedTracks.Add(new(track.Id, track.FullLabel)))
                SaveSettingsFile();
        }

        public bool IsBlockedTrack(Track track)
        {
            return BlockedTracks.Contains(new(track.Id, track.FullLabel));
        }

        public void RemoveBlockedTrack(BlockedEntry entry)
        {
            if (BlockedTracks.Remove(entry))
                SaveSettingsFile();
        }

        private void SetPropertyAndSave<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            if (_settingsInitialized)
                SaveSettingsFile();
        }

        void SaveSettingsFile()
        {
            var json = JsonSerializer.Serialize(this);
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}