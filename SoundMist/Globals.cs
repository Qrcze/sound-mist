using System;
using System.Diagnostics;
using System.IO;

namespace SoundMist
{
    public static class Globals
    {
        public static string SoundCloudBaseUrl = "https://api-v2.soundcloud.com";
        public static readonly Random Random = new();

        public static readonly string InstallDirectory = AppContext.BaseDirectory;
        public static readonly string AppDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundMist");
        public static readonly string SettingsFilePath = Path.Combine(AppDirectory, "settings.json");
        public static readonly string HistoryFilePath = Path.Combine(AppDirectory, "history.json");
        public static readonly string LocalDownloadsPath = Path.Combine(AppDirectory, "downloads");
        public static readonly string LogFilePath = Path.Combine(AppDirectory, "log.txt");

        static Globals()
        {
            Directory.CreateDirectory(AppDirectory);
            MigrateLegacyData();
        }

        private static void MigrateLegacyData()
        {
            if (string.Equals(
                    Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(AppDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!File.Exists(Path.Combine(InstallDirectory, "settings.json")))
                return;

            MigrateFile("settings.json");
            MigrateFile("settings.json.old");
            MigrateFile("history.json");
            MigrateFile("history.json.old");
            MigrateFile("log.txt");
            MigrateFile("log.txt.old");
            MigrateDirectory(Path.Combine(InstallDirectory, "downloads"), LocalDownloadsPath);
        }

        private static void MigrateFile(string fileName)
        {
            MoveFileIfMissing(
                Path.Combine(InstallDirectory, fileName),
                Path.Combine(AppDirectory, fileName));
        }

        private static void MigrateDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                MoveFileIfMissing(sourceFile, Path.Combine(destinationDirectory, relativePath));
            }
        }

        private static void MoveFileIfMissing(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath) || File.Exists(destinationPath))
            {
                return;
            }

            try
            {
                string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                try
                {
                    File.Move(sourcePath, destinationPath);
                }
                catch (IOException)
                {
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                }
                catch (UnauthorizedAccessException)
                {
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                }
            }
            catch (Exception exception)
            {
                // A migration problem should not prevent the app from starting.
                Debug.WriteLine($"Failed to migrate '{sourcePath}': {exception}");
            }
        }
    }
}
