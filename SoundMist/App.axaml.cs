using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SoundMist.Models;
using SoundMist.ViewModels;
using SoundMist.Views;
using System.Net.Http;
using SoundMist.Models.Audio;

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

#if !OS_LINUX
        KeyboardHook.Run();

        var musicPlayer = services.GetRequiredService<IMusicPlayer>();

        KeyboardHook.PlayPausedTriggered += musicPlayer.PlayPause;
        KeyboardHook.PlayTriggered += musicPlayer.Play;
        KeyboardHook.PauseTriggered += musicPlayer.Pause;
        KeyboardHook.PrevTrackTriggered += () => System.Threading.Tasks.Task.Run(async () => await musicPlayer.PlayPrev());
        KeyboardHook.NextTrackTriggered += () => System.Threading.Tasks.Task.Run(async () => await musicPlayer.PlayNext());
#endif

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            NotificationManager.Toplevel = desktop.MainWindow;
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