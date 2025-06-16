using System;
using System.IO;

namespace SoundMist
{
    public static class Globals
    {
        public static string SoundCloudBaseUrl = "https://api-v2.soundcloud.com";
        public static readonly Random Random = new();

        public static string AppDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string SettingsFilePath = AppDirectory + "settings.json";
        public static readonly string HistoryFilePath = AppDirectory + "history.json";
        public static readonly string LocalDownloadsPath = AppDirectory + "downloads";
    }
}