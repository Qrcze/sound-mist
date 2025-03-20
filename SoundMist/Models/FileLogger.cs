using System;
using System.Diagnostics;
using System.IO;

namespace SoundMist.Models
{
    public interface ILogger
    {
        void Info(string message);

        void Warn(string message);

        void Error(string message);

        void Fatal(string message);
    }

    public class FileLogger : ILogger
    {
        public static FileLogger Instance { get; set; } = new FileLogger();
        private static readonly object _lock = new();

        private static void WriteToFile(string message)
        {
            lock (_lock)
            {
                var file = File.Open("log.txt", FileMode.Append);
                if (file.Length > 1_000_000) //if > ~1mb
                {
                    file.Close();
                    var logFiles = Directory.GetFiles(".", "log.*.txt");
                    for (int i = logFiles.Length - 1; i >= 0; i--)
                    {
                        if (i > 3)
                            File.Delete($"log.{i}.txt");
                        else
                            File.Move($"log.{i}.txt", $"log.{i + 1}.txt");
                    }
                    File.Move("log.txt", $"log.0.txt");
                    file = File.Create("log.txt");
                }
                var writer = new StreamWriter(file);
                writer.WriteLine(message);
                writer.Close();
                file.Close();
            }
        }

        public void Info(string message)
        {
            WriteToFile($"{DateTime.Now}\t[INFO]: {message}");
            Debug.Print($"Info: {message}");
        }

        public void Warn(string message)
        {
            WriteToFile($"{DateTime.Now}\t[WARN]: {message}");
            Debug.Print($"Warn: {message}");
        }

        public void Error(string message)
        {
            WriteToFile($"{DateTime.Now}\t[ERROR]: {message} Stack Trace: {new StackTrace().ToString().Trim()}");
            Debug.Print($"Error: {message}");
        }

        public void Fatal(string message)
        {
            WriteToFile($"{DateTime.Now}\t[FATAL]: {message} Stack Trace: {new StackTrace().ToString().Trim()}");
            Debug.Print($"Fatal: {message}");
        }
    }
}