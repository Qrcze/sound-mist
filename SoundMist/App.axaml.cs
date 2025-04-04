﻿using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SoundMist.Models;
using SoundMist.ViewModels;
using SoundMist.Views;
using System.Net.Http;

namespace SoundMist;

public partial class App : Application
{
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

        KeyboardHook.Run();

        var collection = new ServiceCollection();
        collection.AddServices();

        services = collection.BuildServiceProvider();

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

        var initializer = new SoundcloudDataInitializer(
            services.GetRequiredService<ProgramSettings>(),
            services.GetRequiredService<AuthorizedHttpClient>(),
            services.GetRequiredService<HttpClient>(),
            services.GetRequiredService<ILogger>(),
            services.GetRequiredService<MainWindowViewModel>()
            );
        initializer.Run();
    }
}