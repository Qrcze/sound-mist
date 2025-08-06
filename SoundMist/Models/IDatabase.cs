using SoundMist.Models.SoundCloud;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.Models;

public interface IDatabase
{
    void AddPlaylist(Playlist playlist);

    void AddTrack(Track track);

    void AddUser(User user);

    void Clear();

    Task<Track> GetTrackById(long id, CancellationToken token);

    Task<User> GetUserById(long id, CancellationToken token);

    Task<Playlist> GetPlaylistById(long id, CancellationToken token);

    Task<IEnumerable<Track>> GetTracksById(IEnumerable<long> ids, CancellationToken token);

    Task<IEnumerable<User>> GetUsersById(IEnumerable<long> ids, CancellationToken token);

    Task<IEnumerable<Playlist>> GetPlaylistsById(IEnumerable<long> ids, CancellationToken token);
}