using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Views;
using System;

namespace SoundMist;

public partial class App : Application
{
    public static event Action<ServiceProvider>? ServiceConfigured;

    private static ServiceProvider services = null!;

    public static T GetService<T>() where T : notnull
    {
        return services.GetRequiredService<T>();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        var collection = new ServiceCollection();
        collection.AddServices();

        services = collection.BuildServiceProvider();

        ServiceConfigured?.Invoke(services);

        var musicPlayer = services.GetRequiredService<IMusicPlayer>();

#if !OS_LINUX
        var settings = services.GetRequiredService<ProgramSettings>();
        if (settings.AlternativeWindowsMediaKeysHandling)
        {
            KeyboardHook.Run();
            KeyboardHook.PlayPausedTriggered += musicPlayer.PlayPause;
            KeyboardHook.PlayTriggered += musicPlayer.Play;
            KeyboardHook.PauseTriggered += musicPlayer.Pause;
            KeyboardHook.PrevTrackTriggered += () => System.Threading.Tasks.Task.Run(async () => await musicPlayer.PlayPrev());
            KeyboardHook.NextTrackTriggered += () => System.Threading.Tasks.Task.Run(async () => await musicPlayer.PlayNext());
        }
#endif

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            NotificationManager.Toplevel = desktop.MainWindow;

            musicPlayer.ErrorCallback += error =>
            {
                NotificationManager.Show(new Notification("Player error", error, NotificationType.Error, TimeSpan.Zero));
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();

        var initializer = services.GetRequiredService<SoundcloudDataInitializer>();
        initializer.Run();
    }
}