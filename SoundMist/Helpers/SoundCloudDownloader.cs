using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;
using System;
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
    internal static class SoundCloudDownloader
    {
        public static async Task<(List<string>? links, string errorMessage)> GetTrackLinks(HttpClient httpClient, Track track, string clientId, CancellationToken token)
        {
            string? url = track.Media.Transcodings.FirstOrDefault(x => x.Format.MimeType == "audio/mpeg" && x.Format.Protocol == "hls")?.Url;
            if (string.IsNullOrEmpty(url))
                return (null, "Track didn't have the hls stream");

            url = url.Replace("/preview/", "/stream/");

            M3ULinkHolder? playlistLink = null;
            try
            {
                using var playbackStreamRequest = await httpClient.GetAsync($"{url}?client_id={clientId}&track_authorization={track.TrackAuthorization}", token);
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
                using var chunksRequest = await httpClient.GetAsync(playlistLink!.Url, token);
                chunks = await chunksRequest.Content.ReadAsStringAsync(token);
            }
            catch (HttpRequestException e)
            {
                return (null, $"exception while getting chunks list for {track.Title} (ID: {track.Id}): {e.Message}");
            }

            return (chunks.Split('\n').Where(x => !x.StartsWith('#')).ToList(), string.Empty);
        }

        internal static async Task<(byte[]? data, string errorMessage)> DownloadTrackData(HttpClient httpClient, List<string> links, Action<string>? statusCallback, CancellationToken token)
        {
            List<byte[]> chunks = new(links.Count);

            try
            {
                foreach (var link in links)
                {
                    token.ThrowIfCancellationRequested();

                    statusCallback?.Invoke($"Downloading: {chunks.Count}/{links.Count}");

                    chunks.Add(await DownloadTrackChunk(httpClient, link, token));
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
        internal static async Task<byte[]> DownloadTrackChunk(HttpClient httpClient, string link, CancellationToken token)
        {
            using var response = await httpClient.GetAsync(link, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(token);
        }

        public static async Task<(bool success, string error)> SaveTrackLocally(HttpManager httpManager, ProxyMode proxyMode, Track track, string clientId, int appVersion, Action<string> statusCallback)
        {
            Directory.CreateDirectory(Globals.LocalDownloadsPath);

            var httpClient = httpManager.DefaultClient;
            if (proxyMode == ProxyMode.BypassOnly)
            {
                httpClient = httpManager.GetProxiedClient();
                if (track.Policy == "BLOCK")
                    track = (await SoundCloudQueries.GetTracksById(httpClient, clientId, appVersion, [track.Id])).Single();
            }

            (var links, string error) = await GetTrackLinks(httpClient, track, clientId, CancellationToken.None);
            if (links is null)
            {
                return (false, error);
            }

            (var data, error) = await DownloadTrackData(httpClient, links, statusCallback, CancellationToken.None);

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

        public static async Task<(QueryResponse<Track>? tracks, string error)> GetRelatedTracks(HttpClient httpClient, Track track, int userId, string clientId, int appVersion)
        {
            QueryResponse<Track>? tracks;
            try
            {
                using var response = await httpClient.GetAsync($"tracks/{track.Id}/related?user_id={userId}&client_id={clientId}&limit=50&offset=0&linked_partitioning=1&app_version={appVersion}&app_locale=en");
                response.EnsureSuccessStatusCode();
                tracks = await response.Content.ReadFromJsonAsync<QueryResponse<Track>>();
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed retrieving related tracks for {track.Title}: {ex.Message}");
                throw;
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