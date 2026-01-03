using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TagLib;

namespace SoundMist.Helpers
{
    public class SoundCloudDownloader(IHttpManager httpManager, ProgramSettings settings, SoundCloudQueries scQueries)
    {
        private readonly IHttpManager _httpManager = httpManager;
        private readonly ProgramSettings _settings = settings;
        private readonly SoundCloudQueries _scQueries = scQueries;

        internal async Task<bool> CheckConnection(bool forceProxy, CancellationToken token)
        {
            var client = forceProxy ? _httpManager.GetProxiedClient() : _httpManager.DefaultClient;
            try
            {
                using var resonse = await client.GetAsync(string.Empty, token);
                resonse.EnsureSuccessStatusCode();
            }
            catch
            {
                return false;
            }
            return true;
        }

        public async Task<(List<string>? links, string errorMessage)> GetTrackLinks(Track track, bool forceProxy, CancellationToken token)
        {
            string? url = track.Media.Transcodings.FirstOrDefault(x => x.Format.MimeType == "audio/mpeg" && x.Format.Protocol == "hls")?.Url;
            if (string.IsNullOrEmpty(url))
                return (null, "Track didn't have the hls stream");

            url = url.Replace("/preview/", "/stream/");

            M3ULinkHolder? playlistLink = null;
            var client = forceProxy ? _httpManager.GetProxiedClient() : _httpManager.DefaultClient;
            try
            {
                using var playbackStreamRequest = await client.GetAsync($"{url}?client_id={_settings.ClientId}&track_authorization={track.TrackAuthorization}", token);
                playbackStreamRequest.EnsureSuccessStatusCode();

                playlistLink = await playbackStreamRequest.Content.ReadFromJsonAsync<M3ULinkHolder>(token);
            }
            catch (HttpRequestException e)
            {
                return (null, $"Failed getting playback stream url for track: {track.Title} by {track.ArtistName} (ID: {track.Id}), return code: {e.StatusCode}: {e.Message}");
            }

            //get the list of streams
            string chunks;
            try
            {
                using var chunksRequest = await client.GetAsync(playlistLink!.Url, token);
                chunks = await chunksRequest.Content.ReadAsStringAsync(token);
            }
            catch (HttpRequestException e)
            {
                return (null, $"exception while getting chunks list for {track.Title} (ID: {track.Id}): {e.Message}");
            }

            return (chunks.Split('\n').Where(x => !x.StartsWith('#')).ToList(), string.Empty);
        }

        internal async Task<(byte[]? data, string errorMessage)> DownloadTrackData(List<string> links, Action<string>? statusCallback, bool forceProxy, CancellationToken token)
        {
            List<byte[]> chunks = new(links.Count);

            try
            {
                foreach (var link in links)
                {
                    token.ThrowIfCancellationRequested();

                    statusCallback?.Invoke($"Downloading: {chunks.Count}/{links.Count}");

                    chunks.Add(await DownloadTrackChunk(link, forceProxy, token));
                }
            }
            catch (HttpRequestException e)
            {
                return (null, $"Failed retrieving chunks, return code: {e.StatusCode}: {e.Message}");
            }
            catch (TaskCanceledException)
            {
                return (null, "Download task cancelled");
            }
            return (chunks.SelectMany(x => x).ToArray(), string.Empty);
        }

        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        internal async Task<byte[]> DownloadTrackChunk(string link, bool forceProxy, CancellationToken token)
        {
            var client = forceProxy ? _httpManager.GetProxiedClient() : _httpManager.DefaultClient;

            using var response = await client.GetAsync(link, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(token);
        }

        /// <summary>
        /// The returned byte array is rented by <see cref="ArrayPool{T}.Shared" />, therefore it's expected to be returned later (ArrayPool<byte>.Shared.Return()).
        /// </summary>
        /// <param name="link"></param>
        /// <param name="forceProxy"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        internal async Task<(byte[] data, int length)> DownloadTrackChunkPooled(string link, bool forceProxy, CancellationToken token)
        {
            var client = forceProxy ? _httpManager.GetProxiedClient() : _httpManager.DefaultClient;

            using var response = await client.GetAsync(link, token);
            response.EnsureSuccessStatusCode();
            if (!response.Content.Headers.ContentLength.HasValue)
                throw new NotSupportedException("Track chunk didn't return its length");

            int length = (int)response.Content.Headers.ContentLength;

            var buffer = ArrayPool<byte>.Shared.Rent(length);
            using var stream = await response.Content.ReadAsStreamAsync(token);
            await stream.ReadAsync(buffer, token);

            return (buffer, length);
        }

        public async Task<(bool success, string error)> SaveTrackLocally(Track track, Action<string> statusCallback)
        {
            Directory.CreateDirectory(Globals.LocalDownloadsPath);

            var httpClient = _httpManager.DefaultClient;
            bool forceProxy = false;
            if (_settings.ProxyMode == ProxyMode.BypassOnly)
            {
                httpClient = _httpManager.GetProxiedClient();
                forceProxy = true;
                if (track.Policy == "BLOCK")
                    track = (await _scQueries.GetTracksById([track.Id])).Single();
            }

            (var links, string error) = await GetTrackLinks(track, forceProxy, CancellationToken.None);
            if (links is null)
            {
                return (false, error);
            }

            (var data, error) = await DownloadTrackData(links, statusCallback, forceProxy, CancellationToken.None);

            if (data is null)
                return (false, error);

            await System.IO.File.WriteAllBytesAsync(track.LocalFilePath, data);

            using var tfile = TagLib.File.Create(track.LocalFilePath);
            tfile.Tag.Title = track.Title;
            tfile.Tag.AlbumArtists = [track.ArtistName];
            if (!string.IsNullOrEmpty(track.Genre))
                tfile.Tag.Genres = [track.Genre];
            if (track.ReleaseDate.HasValue)
                tfile.Tag.Year = (uint)track.ReleaseDate.Value.Year;
            if (track.PublisherMetadata is not null)
            {
                tfile.Tag.Album = track.PublisherMetadata.AlbumTitle;
                tfile.Tag.Publisher = track.PublisherMetadata.Publisher;
                tfile.Tag.Composers = [track.PublisherMetadata.WriterComposer];
            }

            using var pic = await httpClient.GetAsync(track.ArtworkUrlOriginal);
            if (pic.IsSuccessStatusCode)
            {
                var vect = ByteVector.FromStream(await pic.Content.ReadAsStreamAsync());
                tfile.Tag.Pictures = [new Picture(vect)];
            }

            tfile.Save();

            System.IO.File.WriteAllText($"{Globals.LocalDownloadsPath}/{track.FullLabel}.id", track.Id.ToString());

            return (true, string.Empty);
        }

        public async Task<(QueryResponse<Track>? tracks, string error)> GetRelatedTracks(Track track)
        {
            QueryResponse<Track>? tracks;
            try
            {
                using var response = await _httpManager.DefaultClient.GetAsync($"tracks/{track.Id}/related?user_id={_settings.UserId}&client_id={_settings.ClientId}&limit=50&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en");
                response.EnsureSuccessStatusCode();
                tracks = await response.Content.ReadFromJsonAsync<QueryResponse<Track>>();
            }
            catch (Exception ex)
            {
                return (null, $"Failed retrieving related tracks for {track.Title}: {ex.Message}");
            }

            if (tracks == null)
                return (null, "Couldn't read the related track collection json.");

            return (tracks, string.Empty);
        }

        private class M3ULinkHolder
        {
            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;
        }
    }
}