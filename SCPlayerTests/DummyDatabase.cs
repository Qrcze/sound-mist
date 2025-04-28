using SoundMist.Models;

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

        public Task<Track> GetTrackById(int id, CancellationToken token)
        {
            return Task.FromResult(new Track() { Id = id });
        }

        public Task<User> GetUserById(int id, CancellationToken token)
        {
            return Task.FromResult(new User() { Id = id });
        }

        public Task<Playlist> GetPlaylistById(int id, CancellationToken token)
        {
            return Task.FromResult(new Playlist() { Id = id });
        }

        public Task<IEnumerable<Track>> GetTracksById(IEnumerable<int> ids, CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<Track>());
        }

        public Task<IEnumerable<User>> GetUsersById(IEnumerable<int> ids, CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<User>());
        }

        public Task<IEnumerable<Playlist>> GetPlaylistsById(IEnumerable<int> ids, CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<Playlist>());
        }
    }
}