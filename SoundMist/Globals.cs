using System;

namespace SoundMist
{
    public static class Globals
    {
        public static string SoundCloudBaseUrl = "https://api-v2.soundcloud.com";
        public static string LocalDownloadsPath = "downloads";
        public static readonly Random Random = new();
    }
}