using Microsoft.Extensions.DependencyInjection;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.ViewModels;

namespace SoundMist;

public static class ServiceCollectionExtensions
{
    public static void AddServices(this IServiceCollection collection)
    {
        var programSettings = ProgramSettings.Load();
        var httpManager = new HttpManager(programSettings);

        collection.AddSingleton(programSettings);
        collection.AddSingleton(programSettings);
        collection.AddSingleton<IHttpManager>(httpManager);
        collection.AddSingleton<SoundCloudQueries>();
        collection.AddSingleton<SoundCloudCommands>();
        collection.AddSingleton<SoundCloudDownloader>();
        collection.AddSingleton(History.Load(programSettings));
        collection.AddSingleton<ILogger>(FileLogger.Instance);
        collection.AddSingleton<IAudioController, ManagedBassController>();
        collection.AddSingleton<IMusicPlayer, MusicPlayer>();
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
        collection.AddTransient<ProxyFailViewModel>();
        collection.AddTransient<SoundcloudDataInitializer>();
    }
}