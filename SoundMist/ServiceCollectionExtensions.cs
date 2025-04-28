using Microsoft.Extensions.DependencyInjection;
using SoundMist.Models;
using SoundMist.ViewModels;
using System;
using System.Net.Http;

namespace SoundMist;

public static class ServiceCollectionExtensions
{
    public static void AddServices(this IServiceCollection collection)
    {
        var programSettings = ProgramSettings.Load();

        collection.AddSingleton(new AuthorizedHttpClient() { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) });
        collection.AddSingleton(new HttpClient() { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) });
        collection.AddSingleton(programSettings);
        collection.AddSingleton(History.Load(programSettings));
        collection.AddSingleton<ILogger>(FileLogger.Instance);
        collection.AddSingleton<IMusicPlayer, ManagedBassPlayer>();
        collection.AddSingleton<IDatabase, CacheDatabase>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddTransient<MainViewModel>();
        collection.AddTransient<PlayerViewModel>();
        collection.AddTransient<LikedLibraryViewModel>();
        collection.AddTransient<LoginViewModel>();
        collection.AddTransient<SearchViewModel>();
        collection.AddTransient<DownloadedViewModel>();
        collection.AddTransient<TrackInfoViewModel>();
        collection.AddTransient<SettingsViewModel>();
        collection.AddTransient<UserInfoViewModel>();
        collection.AddTransient<PlaylistInfoViewModel>();
        collection.AddTransient<HistoryViewModel>();
    }
}