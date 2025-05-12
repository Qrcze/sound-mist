using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
        private static HttpClient? _proxyClientInstance;
        private static Task<(HttpClient? proxyClient, string errorMessage)>? _proxySearchTask;

        public static async Task<(HttpClient? proxyClient, string errorMessage)> GetProxyHttpClient(HttpClient defaultClient, string clientId, int appVersion)
        {
            //TODO: have settings if the user wants to customize the proxy themselves

            if (_proxyClientInstance is not null)
                return (_proxyClientInstance, string.Empty);

            _proxySearchTask ??= ProxySearch(defaultClient, clientId, appVersion);

            return await _proxySearchTask;
        }

        static async Task<(HttpClient? proxyClient, string errorMessage)> ProxySearch(HttpClient defaultClient, string clientId, int appVersion)
        {
            string addressesString;
            try
            {
                using var proxyRequest = await defaultClient.GetAsync("https://api.proxyscrape.com/v4/free-proxy-list/get?request=display_proxies&country=us&protocol=http&proxy_format=protocolipport&format=text&timeout=5000");
                proxyRequest.EnsureSuccessStatusCode();
                addressesString = await proxyRequest.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Request to get proxies failed: {ex.Message}");
            }

            string[] addresses = addressesString.Split("\r\n");
            if (addresses.Length == 0)
                return (null, "No valid proxies available");

            string? address = await FindWorkingProxy(addresses, clientId, appVersion);

            if (address == null)
                return (null, "No valid proxy found.");

            var proxy = new WebProxy() { Address = new Uri(address) };
            var handler = new HttpClientHandler() { Proxy = proxy, AutomaticDecompression = DecompressionMethods.All };
            _proxyClientInstance = new HttpClient(handler) { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) };
            return (_proxyClientInstance, string.Empty);
        }

        static async Task<string?> FindWorkingProxy(string[] addresses, string clientId, int appVersion)
        {
            var proxyCheckTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string? workingAddress = null;

            var options = new ParallelOptions() { CancellationToken = proxyCheckTokenSource.Token };

            try
            {
                await Parallel.ForEachAsync(addresses, options, async (address, token) =>
                {
                    try
                    {
                        var proxy = new WebProxy(address);
                        var handler = new HttpClientHandler() { Proxy = proxy };
                        using var client = new HttpClient(handler);

                        var track = (await SoundCloudQueries.GetTracksById(client, clientId, appVersion, [2], token)).Single();
                        if (track is null)
                            return;

                        (var links, _) = await GetTrackLinks(client, track, clientId, token);
                        if (links is null)
                            return;

                        using var request = await client.GetAsync(links[0], token);
                        request.EnsureSuccessStatusCode();

                        proxyCheckTokenSource.Cancel();

                        Debug.Print($"found working proxy: {address}");
                        workingAddress = address;
                    }
                    catch (HttpRequestException e)
                    {
                        Debug.Print($"pinging {address} threw exception: {e.Message}");
                    }
                });
            }
            catch (OperationCanceledException)
            { }

            return workingAddress;
        }

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
                return (null, $"Failed getting playback stream url {track.Title}, return code: {e.StatusCode}: {e.Message}");
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
                return (null, $"exception while getting chunks list for {track.Title}: {e.Message}");
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
            return (chunks.SelectMany(x => x).ToArray(), string.Empty);
        }

        internal static async Task<byte[]> DownloadTrackChunk(HttpClient httpClient, string link, CancellationToken token)
        {
            using var response = await httpClient.GetAsync(link, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(token);
        }

        public static async Task<(bool success, string error)> SaveTrackLocally(HttpClient httpClient, Track track, string clientId, Action<string> statusCallback)
        {
            Directory.CreateDirectory(Globals.LocalDownloadsPath);

            (var links, string error) = await GetTrackLinks(httpClient, track, clientId, CancellationToken.None);
            if (links is null)
                return (false, error);

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