using Avalonia;
using Avalonia.Styling;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        public const int SettingsVersion = 1;

        private static readonly Rect DefaultWindowPos = new(200, 200, 1150, 800);

        public int Version { get; } = SettingsVersion;

        private bool _settingsInitialized; // a guard, otherwise json uses property to SetPropertyAndSave and it can break stuff

        private string? _authToken;
        private float _volume = 1;
        private bool _autoplayStationOnLastTrack = true;
        private bool _shuffle;
        private long? _lastTrackId;
        private MainViewTab _startingTabIndex = MainViewTab.LikedTracks;
        private bool _startPlayingOnLaunch;
        private AppColorTheme _appColorTheme;
        private int _historyLimit = 50;

        private ProxyMode _proxyMode;
        private ProxyProtocol _proxyProtocol;
        private string _proxyHost;
        private int _proxyPort;
        private bool _alternativeWindowsMediaKeysHandling;
        private Rect _windowPos = DefaultWindowPos;

        public static ProgramSettings Load()
        {
            ProgramSettings? settings = null;
            string json;

            if (File.Exists(Globals.SettingsFilePath))
            {
                json = File.ReadAllText(Globals.SettingsFilePath);

                try
                {
                    settings = GetUpdatedSettings(json);
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error($"Failed reading settings json: {ex.Message}");
                    File.Copy(Globals.SettingsFilePath, Globals.SettingsFilePath + ".old", overwrite: true);
                    NotificationManager.Show(new("Failed loading settings",
                        "Program has failed loading the settings file, please check the logs for further info.",
                        Avalonia.Controls.Notifications.NotificationType.Error, TimeSpan.Zero));
                }
            }

            if (settings == null)
            {
                settings = new();
                json = JsonSerializer.Serialize(settings);
                File.WriteAllText(Globals.SettingsFilePath, json);
            }

            settings._settingsInitialized = true;

            return settings;
        }

        public static ProgramSettings? GetUpdatedSettings(string json)
        {
            var jsonNode = JsonNode.Parse(json)!.AsObject();

            int version = 0;

            var v = jsonNode[nameof(Version)];
            if (v is not null)
                version = (int)v;

            if (version != SettingsVersion)
            {
                do
                {
                    jsonNode = version switch
                    {
                        0 => UpdateFromVersion0(jsonNode),
                        _ => throw new NotImplementedException(),
                    };
                    version = (int)jsonNode[nameof(Version)]!;
                } while (version != SettingsVersion);
            }

            return jsonNode.Deserialize<ProgramSettings>();
        }

        private static JsonObject UpdateFromVersion0(JsonObject jsonObject)
        {
            FileLogger.Instance.Info("Updating ProgramSettings from version 0");

            var users = jsonObject["BlockedUsers"].Deserialize<HashSet<BlockedEntry>>()!;
            var tracks = jsonObject["BlockedTracks"].Deserialize<HashSet<BlockedEntry>>()!;

            var newUsers = new Dictionary<long, string>();
            foreach (var user in users)
                newUsers.Add(user.Id, user.Title);
            var newTracks = new Dictionary<long, string>();
            foreach (var track in tracks)
                newTracks.Add(track.Id, track.Title);

            jsonObject["BlockedUsers"]!.ReplaceWith(newUsers);
            jsonObject["BlockedTracks"]!.ReplaceWith(newTracks);

            jsonObject["Version"] = 1;
            return jsonObject;
        }

        [JsonIgnore] public string ClientId { get; set; } = string.Empty;

        /// <summary> Used in some http requests, as a way to identify the anonymous user; different from actual UserId, which is stored in SoundCloud database </summary>
        [JsonIgnore] public string? AnonymousUserId { get; set; }

        [JsonIgnore] public int AppVersion { get; set; }

        [JsonIgnore] public long? UserId { get; set; }

        public event Action<Rect>? WindowPosReset;

        public string? AuthToken { get => _authToken; set => SetPropertyAndSave(ref _authToken, value); }
        public float Volume { get => _volume; set => SetPropertyAndSave(ref _volume, value); }
        public bool AutoplayStationOnLastTrack { get => _autoplayStationOnLastTrack; set => SetPropertyAndSave(ref _autoplayStationOnLastTrack, value); }
        public bool Shuffle { get => _shuffle; set => SetPropertyAndSave(ref _shuffle, value); }
        public long? LastTrackId { get => _lastTrackId; set => SetPropertyAndSave(ref _lastTrackId, value); }
        public MainViewTab StartingTabIndex { get => _startingTabIndex; set => SetPropertyAndSave(ref _startingTabIndex, value); }
        public bool StartPlayingOnLaunch { get => _startPlayingOnLaunch; set => SetPropertyAndSave(ref _startPlayingOnLaunch, value); }
        public int HistoryLimit { get => _historyLimit; set => SetPropertyAndSave(ref _historyLimit, value); }
        public ProxyMode ProxyMode { get => _proxyMode; set => SetPropertyAndSave(ref _proxyMode, value); }
        public ProxyProtocol ProxyProtocol { get => _proxyProtocol; set => SetPropertyAndSave(ref _proxyProtocol, value); }
        public string ProxyHost { get => _proxyHost; set => SetPropertyAndSave(ref _proxyHost, value); }
        public int ProxyPort { get => _proxyPort; set => SetPropertyAndSave(ref _proxyPort, value); }
        public bool AlternativeWindowsMediaKeysHandling { get => _alternativeWindowsMediaKeysHandling; set => SetPropertyAndSave(ref _alternativeWindowsMediaKeysHandling, value); }

        [JsonConverter(typeof(SizeConverter))]
        public Rect WindowPos { get => _windowPos; set => SetPropertyAndSave(ref _windowPos, value); }

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
        public Dictionary<long, string> BlockedUsers { get; } = [];

        /// <summary>
        /// Do not modify directly; use <see cref="AddBlockedTrack(Track)"/> instead
        /// </summary>
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Dictionary<long, string> BlockedTracks { get; } = [];

        public void AddBlockedUser(User user)
        {
            if (BlockedUsers.TryAdd(user.Id, user.Username))
                SaveSettingsFile();
        }

        public bool IsBlockedUser(Track track)
        {
            if (!track.UserId.HasValue)
                return false;

            return BlockedUsers.ContainsKey(track.UserId.Value);
        }

        public void RemoveBlockedUser(long userId)
        {
            if (BlockedUsers.Remove(userId))
                SaveSettingsFile();
        }

        public void AddBlockedTrack(Track track)
        {
            if (BlockedTracks.TryAdd(track.Id, track.FullLabel))
                SaveSettingsFile();
        }

        public bool IsBlockedTrack(Track track)
        {
            return BlockedTracks.ContainsKey(track.Id);
        }

        public void RemoveBlockedTrack(long trackId)
        {
            if (BlockedTracks.Remove(trackId))
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

        public void ApplyProxySettings(ProxyMode mode, ProxyProtocol protocol, string host, int port)
        {
            _proxyMode = mode;
            _proxyProtocol = protocol;
            _proxyHost = host;
            _proxyPort = port;

            SaveSettingsFile();
        }

        public (ProxyMode mode, ProxyProtocol protocol, string host, int port) GetProxySettings()
        {
            return (ProxyMode, ProxyProtocol, ProxyHost, ProxyPort);
        }

        void SaveSettingsFile()
        {
            var json = JsonSerializer.Serialize(this);
            File.WriteAllText(Globals.SettingsFilePath, json);
        }

        internal void ResetWindowSize()
        {
            WindowPos = DefaultWindowPos;
            WindowPosReset?.Invoke(WindowPos);
        }

        private class SizeConverter : JsonConverter<Rect>
        {
            public override Rect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string[] v = (reader.GetString()?.Split(',')) ?? throw new Exception();
                return new Rect(double.Parse(v[0]), double.Parse(v[1]), double.Parse(v[2]), double.Parse(v[3]));
            }

            public override void Write(Utf8JsonWriter writer, Rect value, JsonSerializerOptions options)
            {
                writer.WriteStringValue($"{value.X},{value.Y},{value.Width},{value.Height}");
            }
        }
    }
}