using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace SoundMist.Linux;

[DBusInterface("org.mpris.MediaPlayer2.Player")]
public interface IPlayer : IDBusObject
{
    //methods
    Task NextAsync();
    Task PreviousAsync();
    Task PauseAsync();
    Task PlayPauseAsync();
    Task StopAsync();
    Task PlayAsync();
    Task SeekAsync(long Offset);
    Task SetPositionAsync(ObjectPath TrackId, long Position);
    Task OpenUriAsync(string Uri);

    //signals
    Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null);

    //props
    Task<object> GetAsync(string prop);
    Task<PlayerProperties> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[Dictionary]
public class PlayerProperties
{
    public string PlaybackStatus = "Paused";
    public string LoopStatus = "None";
    public double Rate = 1;
    public bool Shuffle = false;
    public IDictionary<string, object> Metadata = new Dictionary<string, object>();
    public double Volume = 1;
    public long Position = 0;
    public double MinimumRate = 1;
    public double MaximumRate = 1;
    public bool CanGoNext = true;
    public bool CanGoPrevious = true;
    public bool CanPlay = true;
    public bool CanPause = true;
    public bool CanSeek = true;
    public bool CanControl = true;
}