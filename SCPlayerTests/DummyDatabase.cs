using SoundMist.Models;
using SoundMist.Models.SoundCloud;

namespace SCPlayerTests
{
    internal class DummyDatabase : IDatabase
    {
        public void AddPlaylist(Playlist playlist)
        {
        }

        public void AddTrack(Track track)
        {
        }

        public void AddUser(User user)
        {
        }

        public void Clear()
        {
        }

        public Task<Track> GetTrackById(long id, CancellationToken token)
        {
            return Task.FromResult(new Track() { Id = id });
        }

        public Task<User> GetUserById(long id, CancellationToken token)
        {
            return Task.FromResult(new User() { Id = id });
        }

        public Task<Playlist> GetPlaylistById(long id, CancellationToken token)
        {
            return Task.FromResult(new Playlist() { Id = id });
        }

        public Task<IEnumerable<Track>> GetTracksById(IEnumerable<long> ids, CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<Track>());
        }

        public Task<IEnumerable<User>> GetUsersById(IEnumerable<long> ids, CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<User>());
        }

        public Task<IEnumerable<Playlist>> GetPlaylistsById(IEnumerable<long> ids, CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<Playlist>());
        }
    }
}