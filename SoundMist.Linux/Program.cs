using System;
using System.Diagnostics;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using SoundMist.Models;
using Tmds.DBus;

namespace SoundMist.Linux;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        App.ServiceConfigured += (services) =>
        {
            Debug.Print("connecting media player");
            var logger = services.GetRequiredService<ILogger>();
            var musicPlayer = services.GetRequiredService<IMusicPlayer>();

            var media = new MediaService(logger, musicPlayer);
            media.Register(new Connection(Address.Session));

            Debug.Print("Media player connected");
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            FileLogger.Instance.Fatal($"Program crashed unexpectedly: {ex.Message}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}