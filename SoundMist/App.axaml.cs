using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using SoundMist.Models;
using SoundMist.ViewModels;
using SoundMist.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
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

        var collection = new ServiceCollection();
        collection.AddServices();

        services = collection.BuildServiceProvider();
        //var vm = services.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView();
            //throw new PlatformNotSupportedException("desktop only lol");
        }

        base.OnFrameworkInitializationCompleted();

        var initializer = new SoundcloudDataInitializer(
            services.GetRequiredService<ProgramSettings>(),
            services.GetRequiredService<HttpClient>(),
            services.GetRequiredService<ILogger>(),
            services.GetRequiredService<MainWindowViewModel>()
            );
        initializer.Run();
    }
}