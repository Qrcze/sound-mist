using SoundMist.Helpers;
using SoundMist.Models.SoundCloud;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.Models
{
    public class CacheDatabase(IHttpManager httpManager, ProgramSettings settings, SoundCloudQueries queries) : IDatabase
    {
        private readonly IHttpManager _httpManager = httpManager;
        private readonly ProgramSettings _settings = settings;
        private readonly SoundCloudQueries _queries = queries;

        private readonly Dictionary<int, Track> _tracks = [];
        private readonly Dictionary<int, User> _users = [];
        private readonly Dictionary<int, Playlist> _playlists = [];

        public void AddTrack(Track track) => _tracks[track.Id] = track;

        public void AddUser(User user) => _users[user.Id] = user;

        public void AddPlaylist(Playlist playlist) => _playlists[playlist.Id] = playlist;

        public void Clear()
        {
            _tracks.Clear();
            _users.Clear();
            _playlists.Clear();
        }

        public async Task<Track> GetTrackById(int id, CancellationToken token) => (await GetTracksById([id], token)).Single();
        public async Task<User> GetUserById(int id, CancellationToken token) => (await GetUsersById([id], token)).Single();
        public async Task<Playlist> GetPlaylistById(int id, CancellationToken token) => (await GetPlaylistsById([id], token)).Single();

        /// <summary>
        /// Grabs the tracks data from cache, downloading the missing ones, and returns in the same order as the IDs.
        /// </summary>
        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        public async Task<IEnumerable<Track>> GetTracksById(IEnumerable<int> ids, CancellationToken token)
        {
            var missingItems = ids.Except(_tracks.Keys);

            if (missingItems.Any())
            {
                var tracks = await _queries.GetTracksById(missingItems, false, token);

                foreach (var track in tracks)
                    _tracks.Add(track.Id, track);
            }

            return ids.Select(id => _tracks[id]);
        }

        /// <summary>
        /// Grabs the users data from cache, downloading the missing ones, and returns in the same order as the IDs.
        /// </summary>
        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        public async Task<IEnumerable<User>> GetUsersById(IEnumerable<int> ids, CancellationToken token)
        {
            var missingItems = ids.Except(_users.Keys);

            if (missingItems.Any())
            {
                foreach (var item in missingItems)
                {
                    var user = await _queries.GetUserInfo(item, token);

                    _users.Add(user.Id, user);
                }
            }

            return ids.Select(id => _users[id]);
        }

        /// <summary>
        /// Grabs the tracks data from cache, downloading the missing ones, and returns in the same order as the IDs.
        /// </summary>
        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        public async Task<IEnumerable<Playlist>> GetPlaylistsById(IEnumerable<int> ids, CancellationToken token)
        {
            var missingItems = ids.Except(_playlists.Keys);

            if (missingItems.Any())
            {
                foreach (var item in missingItems)
                {
                    var playlist = await _queries.GetPlaylistInfo(item, token);

                    _playlists.Add(playlist.Id, playlist);
                }
            }

            return ids.Select(id => _playlists[id]);
        }
    }
}