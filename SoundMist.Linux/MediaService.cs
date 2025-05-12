using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SoundMist.Models;
using SoundMist.Models.Audio;
using Tmds.DBus;

namespace SoundMist.Linux;

public interface IMediaPlayerExtra : IMediaPlayer2
{
    IDictionary<string, object> MediaProperties { get; }
}

public interface IPlayerExtra : IPlayer
{
    PlayerProperties PlayerProperties { get; }
}

public class MediaService : IPlayerExtra, IMediaPlayerExtra
{
    private const string PlayerName = "org.mpris.MediaPlayer2.SoundMist";
    private const string PlayerObjectPath = "/org/mpris/MediaPlayer2";

    private readonly ILogger _logger;
    private readonly IMusicPlayer _musicPlayer;
    private Connection _connection = null!;

    public ObjectPath ObjectPath => new(PlayerObjectPath);

    public event Action<PropertyChanges>? MediaPropertiesChanged;
    public event Action<PropertyChanges>? PlayerPropertiesChanged;
    public event Action<long>? Seeked;

    public IDictionary<string, object> MediaProperties { get; }
    public PlayerProperties PlayerProperties { get; } = new();

    public MediaService(ILogger logger, IMusicPlayer musicPlayer)
    {
        _logger = logger;
        _musicPlayer = musicPlayer;

        AttachMusicPlayerEvents();

        MediaProperties = new Dictionary<string, object>
        {
            ["CanQuit"] = true,
            ["Fullscreen"] = true,
            ["CanSetFullscreen"] = true,
            ["CanRaise"] = false, //todo: find a way to make avalonia window focus/move to the top on linux
            ["HasTrackList"] = false,
            ["Identity"] = "Sound Mist",
            // ["DesktopEntry"] = "",
            // ["SupportedUriSchemes"] = ,
            // ["SupportedMimeTypes"] = ,
        };
    }

    private void AttachMusicPlayerEvents()
    {
        _musicPlayer.TrackChanging += (track) =>
        {
            this.SetMetadata(track.Id, track.Duration * 1000, track.Title, track.ArtistName, track.ArtworkUrlOriginal);
            this.SetPosition(0);
        };
        _musicPlayer.PlayStateUpdated += (playState, _) =>
        {
            switch (playState)
            {
                case PlayState.Playing:
                    this.SetPlaybackStatus(PlaybackStatus.Playing);
                    break;
                case PlayState.Paused:
                    this.SetPlaybackStatus(PlaybackStatus.Paused);
                    break;
                case PlayState.Loading:
                    this.SetPlaybackStatus(PlaybackStatus.Stopped);
                    this.SetProperty(ref PlayerProperties.CanPlay, "CanPlay", false);
                    this.SetProperty(ref PlayerProperties.CanPause, "CanPause", false);
                    this.SetProperty(ref PlayerProperties.CanSeek, "CanSeek", false);
                    break;
                case PlayState.Loaded:
                    this.SetProperty(ref PlayerProperties.CanPlay, "CanPlay", true);
                    this.SetProperty(ref PlayerProperties.CanPause, "CanPause", true);
                    this.SetProperty(ref PlayerProperties.CanSeek, "CanSeek", true);
                    break;
                case PlayState.Error:
                    this.SetPlaybackStatus(PlaybackStatus.Stopped);
                    this.SetProperty(ref PlayerProperties.CanPlay, "CanPlay", false);
                    this.SetProperty(ref PlayerProperties.CanPause, "CanPause", false);
                    this.SetProperty(ref PlayerProperties.CanSeek, "CanSeek", false);
                    break;
                default:
                    throw new UnreachableException($"Unexpected play state in Linux Media Service: {playState}");
            }
        };

        _musicPlayer.TrackTimeUpdated += d => { this.SetPosition((long)(d * 1000)); };
    }

    public async Task Register(Connection connection)
    {
        _connection = connection;
        try
        {
            await _connection.ConnectAsync();
            await _connection.RegisterObjectAsync(this);
            await _connection.RegisterServiceAsync(PlayerName);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed registering linux media service: {ex.Message}");
        }
    }

    public async Task NextAsync()
    {
        await _musicPlayer.PlayNext();
        // return Task.CompletedTask;
    }

    public async Task PreviousAsync()
    {
        await _musicPlayer.PlayPrev();
        // return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        _musicPlayer.Pause();
        return Task.CompletedTask;
    }

    public Task PlayPauseAsync()
    {
        _musicPlayer.PlayPause();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _musicPlayer.Stop();
        return Task.CompletedTask;
    }

    public Task PlayAsync()
    {
        _musicPlayer.Play();
        return Task.CompletedTask;
    }

    public Task SeekAsync(long Offset)
    {
        return Task.CompletedTask;
    }

    public Task SetPositionAsync(ObjectPath TrackId, long Position)
    {
        // this.SetPosition(Position);
        _musicPlayer.SetPosition(Position / 1000);
        return Task.CompletedTask;
    }

    public Task OpenUriAsync(string Uri)
    {
        throw new NotImplementedException();
    }

    public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
    {
        return SignalWatcher.AddAsync(this, nameof(Seeked), handler);
    }

    public Task RaiseAsync()
    {
        throw new NotImplementedException();
    }

    public Task QuitAsync()
    {
        Environment.Exit(0);
        return Task.CompletedTask;
    }

    Task<object> IMediaPlayer2.GetAsync(string prop)
    {
        MediaProperties.TryGetValue(prop, out var value);
        return Task.FromResult<object>(value!);
    }

    Task<IDictionary<string, object>> IMediaPlayer2.GetAllAsync()
    {
        return Task.FromResult(MediaProperties);
    }

    Task IMediaPlayer2.SetAsync(string prop, object val)
    {
        MediaProperties[prop] = val;
        MediaPropertiesChanged?.Invoke(PropertyChanges.ForProperty(prop, val));
        return Task.CompletedTask;
    }

    Task<IDisposable> IMediaPlayer2.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        return SignalWatcher.AddAsync(this, nameof(MediaPropertiesChanged), handler);
    }

    Task<object> IPlayer.GetAsync(string prop)
    {
        return Task.FromResult<object>(typeof(PlayerProperties).GetField(prop).GetValue(PlayerProperties));
    }

    Task<PlayerProperties> IPlayer.GetAllAsync()
    {
        return Task.FromResult(PlayerProperties);
    }

    Task IPlayer.SetAsync(string prop, object val)
    {
        MediaProperties[prop] = val;
        PlayerPropertiesChanged?.Invoke(PropertyChanges.ForProperty(prop, val));
        return Task.CompletedTask;
    }

    Task<IDisposable> IPlayer.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        return SignalWatcher.AddAsync(this, nameof(PlayerPropertiesChanged), handler);
    }
}

public enum PlaybackStatus
{
    Playing,
    Paused,
    Stopped
}

public static class MediaServiceExtensions
{
    public static void SetMetadata(this IPlayerExtra player, int trackId, long lengthMicroseconds, string title,
        string artist, string albumImageUrl)
    {
        player.PlayerProperties.Metadata = new Dictionary<string, object>
        {
            ["mpris:trackid"] = new ObjectPath($"/org/mpris/MediaPlayer2/Track/{trackId}"),
            ["mpris:length"] = lengthMicroseconds,
            ["xesam:title"] = title,
            ["xesam:artist"] = new[] { artist },
            ["xesam:album"] = "Album Name",
            ["mpris:artUrl"] = albumImageUrl,
            ["title"] = title,
            ["artist"] = artist,
        };
        player.SetAsync("Metadata", player.PlayerProperties.Metadata);
    }

    public static void SetPlaybackStatus(this IPlayerExtra player, PlaybackStatus state)
    {
        SetProperty(player, ref player.PlayerProperties.PlaybackStatus, "PlaybackStatus", state.ToString());
    }

    /// <summary>
    /// </summary>
    /// <param name="player"></param>
    /// <param name="position">in microseconds</param>
    public static void SetPosition(this IPlayerExtra player, long position)
    {
        player.PlayerProperties.Position = position;
        // player.SetAsync("Position", position);
    }

    public static void SetProperty<T>(this IPlayerExtra player, ref T prop, string propertyName, T value)
    {
        prop = value!;
        player.SetAsync(propertyName, value!);
    }
}