using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace SoundMist.Linux;

[DBusInterface("org.mpris.MediaPlayer2")]
public interface IMediaPlayer2 : IDBusObject
{
    Task RaiseAsync();
    Task QuitAsync();
    
    Task<object> GetAsync(string prop);
    Task<IDictionary<string, object>> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}