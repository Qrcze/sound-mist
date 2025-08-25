using SoundMist.Helpers;
using SoundMist.Models.SoundCloud;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.Models
{
    public class CacheDatabase(SoundCloudQueries queries, ILogger logger) : IDatabase
    {
        private readonly SoundCloudQueries _queries = queries;
        private readonly ILogger _logger = logger;

        private readonly Dictionary<long, Track> _tracks = [];
        private readonly Dictionary<long, User> _users = [];
        private readonly Dictionary<long, Playlist> _playlists = [];

        public void AddTrack(Track track) => _tracks[track.Id] = track;

        public void AddUser(User user) => _users[user.Id] = user;

        public void AddPlaylist(Playlist playlist) => _playlists[playlist.Id] = playlist;

        public void Clear()
        {
            _tracks.Clear();
            _users.Clear();
            _playlists.Clear();
        }

        public async Task<Track> GetTrackById(long id, CancellationToken token) => (await GetTracksById([id], token)).Single();

        public async Task<User> GetUserById(long id, CancellationToken token) => (await GetUsersById([id], token)).Single();

        public async Task<Playlist> GetPlaylistById(long id, CancellationToken token) => (await GetPlaylistsById([id], token)).Single();

        /// <summary>
        /// Grabs the tracks data from cache, downloading the missing ones, and returns in the same order as the IDs.
        /// </summary>
        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        public async Task<IEnumerable<Track>> GetTracksById(IEnumerable<long> ids, CancellationToken token)
        {
            var missingItems = ids.Except(_tracks.Keys);

            if (missingItems.Any())
            {
                var tracks = await _queries.GetTracksById(missingItems, false, token);

                foreach (var track in tracks)
                {
                    if (token.IsCancellationRequested)
                        return [];

                    _tracks.Add(track.Id, track);
                }
            }

            //since there's an API for getting batch tracks, this requires a bit of a backwards deleted-track check
            return ids.Select(id =>
            {
                if (_tracks.TryGetValue(id, out var track))
                    return track;
                return Track.CreateRemovedTrack(id);
            });
        }

        /// <summary>
        /// Grabs the users data from cache, downloading the missing ones, and returns in the same order as the IDs.
        /// </summary>
        public async Task<IEnumerable<User>> GetUsersById(IEnumerable<long> ids, CancellationToken token)
        {
            List<User> users = new(ids.Count());
            foreach (var id in ids)
            {
                try
                {
                    if (_users.TryGetValue(id, out var existingUser) && (existingUser.IsFullInfo || existingUser.IsDeleted))
                        users.Add(existingUser);
                    else
                    {
                        var user = await FetchUserById(id, token);
                        _users[id] = user;
                        users.Add(user);
                    }
                }
                catch (TaskCanceledException)
                {
                    return [];
                }
            }
            return users;
        }

        /// <summary>
        /// </summary>
        /// <param name="id"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException" />
        async Task<User> FetchUserById(long id, CancellationToken token)
        {
            var (user, errorMessage) = await _queries.GetUserInfo(id, token);

            token.ThrowIfCancellationRequested();

            if (user == null)
            {
                _logger.Info($"Cache failed retrieving user with id <{id}>: {errorMessage}");
                return User.CreateDeletedUser(id);
            }
            return user;
        }

        /// <summary>
        /// Grabs the tracks data from cache, downloading the missing ones, and returns in the same order as the IDs.
        /// </summary>
        public async Task<IEnumerable<Playlist>> GetPlaylistsById(IEnumerable<long> ids, CancellationToken token)
        {
            var missingItems = ids.Except(_playlists.Keys);

            if (missingItems.Any())
            {
                foreach (var item in missingItems)
                {
                    var (playlist, errorMessage) = await _queries.GetPlaylistInfo(item, token);

                    if (token.IsCancellationRequested)
                        return [];

                    if (playlist == null)
                    {
                        _logger.Warn($"Cache failed retrieving playlist <{item}>: {errorMessage}");
                        _playlists.Add(item, Playlist.CreateDeletedPlaylist(item));
                    }
                    else
                        _playlists.Add(playlist.Id, playlist);
                }
            }

            return ids.Select(id => _playlists[id]);
        }
    }
}