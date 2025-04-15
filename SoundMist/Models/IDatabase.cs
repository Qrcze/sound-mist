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

    Task<Track> GetTrackById(int id, CancellationToken token);

    Task<User> GetUserById(int id, CancellationToken token);

    Task<Playlist> GetPlaylistById(int id, CancellationToken token);

    Task<IEnumerable<Track>> GetTracksById(IEnumerable<int> ids, CancellationToken token);

    Task<IEnumerable<User>> GetUsersById(IEnumerable<int> ids, CancellationToken token);

    Task<IEnumerable<Playlist>> GetPlaylistsById(IEnumerable<int> ids, CancellationToken token);
}