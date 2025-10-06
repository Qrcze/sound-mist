using RichardSzalay.MockHttp;
using SoundMist;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using System.Text.Json;

namespace SCPlayerTests
{
    public class MockAudioController : IAudioController
    {
        public bool ChannelInitialized { get; set; }

        public double TimeInSeconds { get; set; }
        public double Volume { get; set; } = 1;

        public bool IsPlaying { get; set; }

        public event Action? OnTrackEnded;

        //debug flag
        public bool IsStreamCompleted { get; set; }

        public void Play()
        {
            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            ChannelInitialized = false;
        }

        public void AppendBytes(byte[] bytes)
        {
            Assert.False(IsStreamCompleted);
        }

        public void InitBufferedChannel(byte[] initialBytes, int trackDurationMs)
        {
            IsPlaying = false;
            ChannelInitialized = true;
        }

        public void LoadFromFile(string filePath)
        {
            IsPlaying = false;
            ChannelInitialized = true;
        }

        public void StreamCompleted()
        {
            IsStreamCompleted = true;
        }

        public bool Mute { get; set; }
    }

    public class MockHttpManager : IHttpManager
    {
        public AuthorizedHttpClient AuthorizedClient { get; }

        public HttpClient DefaultClient { get; }

        public MockHttpManager()
        {
            var mock = new MockHttpMessageHandler();

            mock.When(HttpMethod.Get, MusicPlayerTests.MockTrack1_Json_Request_Url)
                .Respond("application/json", JsonSerializer.Serialize(MusicPlayerTests.GetMockTrack()));

            mock.When(HttpMethod.Get, MusicPlayerTests.MockTrack1_M3U_Request_Url)
                .Respond("application/json", MusicPlayerTests.MockTrack1_M3U_Response);
            mock.When(HttpMethod.Get, MusicPlayerTests.MockTrack1_M3U_Data_Request)
                .Respond("application/vnd.apple.mpegurl", MusicPlayerTests.MockTrack1_M3U);

            mock.When(HttpMethod.Get, MusicPlayerTests.MockTrack1_M3U_Wildcard)
                .Respond("application/octet-stream", File.OpenRead("sample.mp3"));

            AuthorizedClient = new(mock);
            DefaultClient = new(mock);
        }

        public HttpClient GetClient() => DefaultClient;

        public HttpClient GetProxiedClient() => DefaultClient;
    }

    public class MusicPlayerTests
    {
        public static readonly string MockTrack1_Json_Request_Url = "https://api-v2.soundcloud.com/tracks?ids=1&client_id=*&app_version=*";
        public static readonly string MockTrack1_M3U_Request_Url = "https://api-v2.soundcloud.com/media/soundcloud:tracks:1/code-1/stream/hls";
        public static readonly string MockTrack1_M3U_Data_Request = "https://m3u-data.com";
        public static readonly string MockTrack1_M3U_Response = $$"""{ "url": "{{MockTrack1_M3U_Data_Request}}" }""";

        public static readonly string MockTrack1_M3U_Wildcard = "https://playback.media-streaming.soundcloud.cloud/hash/aac_160k/*";
        public static readonly string MockTrack1_M3U_0 = "https://playback.media-streaming.soundcloud.cloud/hash/aac_160k/0";
        public static readonly string MockTrack1_M3U_1 = "https://playback.media-streaming.soundcloud.cloud/hash/aac_160k/1";
        public static readonly string MockTrack1_M3U_2 = "https://playback.media-streaming.soundcloud.cloud/hash/aac_160k/2";

        public static readonly string MockTrack1_M3U = $"""
            #EXTM3U
            #EXT-X-VERSION:7
            #EXT-X-TARGETDURATION:10
            #EXT-X-MEDIA-SEQUENCE:0
            #EXT-X-PLAYLIST-TYPE:VOD
            #EXT-X-MAP:URI="https://playback.media-streaming.soundcloud.cloud/hash/aac_160k/init.mp4"
            #EXTINF:10.007800,
            {MockTrack1_M3U_0}
            #EXTINF:10.007800,
            {MockTrack1_M3U_1}
            #EXTINF:9.984580,
            {MockTrack1_M3U_2}
            #EXT-X-ENDLIST
            """;

        [Fact]
        public async Task TrackLoadsAndPlaysAfter()
        {
            var httpManager = new MockHttpManager();
            var settings = new ProgramSettings();
            var queries = new SoundCloudQueries(httpManager, settings);
            var downloader = new SoundCloudDownloader(httpManager, settings, queries);
            var audioController = new MockAudioController() { ChannelInitialized = true };
            var logger = new DummyLogger();
            var track = GetMockTrack();

            var player = new MusicPlayer(queries, downloader, settings, audioController, logger);
            player.PlayStateUpdated += (state, message) =>
            {
                Assert.False(state == PlayState.Error, $"Play state threw an error: {message}");
            };

            bool trackChangingFired = false;
            player.TrackChanging += (t) =>
            {
                Assert.True(t.Title == track.Title);
                Assert.False(trackChangingFired);
                trackChangingFired = true;
            };
            player.TrackChanged += (t) =>
            {
                Assert.True(t.Title == track.Title);
                Assert.True(trackChangingFired);
            };

            Assert.False(player.IsPlaying);
            await player.LoadNewQueue([track], null, false);
            Assert.False(player.IsPlaying);

            player.Play(); //play runs a separate task, so this thread needs to wait for a second to hopefully catch up

            int tries = 100;
            while (!player.IsPlaying)
            {
                await Task.Delay(100);
                tries--;
                if (tries <= 0)
                    break;
            }
            Assert.True(player.IsPlaying);
        }

        [Fact]
        public async Task TrackAutoplaysAtTheEndOfQueue()
        {
            Assert.Fail("todo");
        }

        /* tests to do:
         *  queue - adding, removing(?), continuing with autoplay at the end
         *  play pause - if works at all
         * */


        public static Track GetMockTrack()
        {
            return new()
            {
                Id = 1,
                Title = "Test Track",
                Policy = "ALLOW",
                Duration = 300000,
                FullDuration = 300000,
                Media = new()
                {
                    Transcodings =
                    [
                        new()
                        {
                            Duration = 300000,
                            Format = new()
                            {
                                MimeType = "audio/mpeg",
                                Protocol = "hls",
                            },
                            IsLegacyTranscoding = true,
                            Preset = "mp3_0_1",
                            Quality = "sq",
                            Snipped = false,
                            Url = MockTrack1_M3U_Request_Url
                        }
                    ]
                },
                User = new()
                {
                    Username = "Test User",
                    Id = 2,
                }
            };
        }

        /*
      "duration": 218645,
      "full_duration": 218645,
      "id": 764804143,
      "label_name": "Rhekboi",
      "permalink": "floating-in-a-silk-kimono",
      "permalink_url": "https://soundcloud.com/rhekluse/floating-in-a-silk-kimono",
      "policy": "ALLOW",
      "secret_token": null,
      "station_permalink": "track-stations:764804143",
      "station_urn": "soundcloud:system-playlists:track-stations:764804143",
      "streamable": true,
      "title": "Floating In A Silk Kimono",
      "track_authorization": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJnZW8iOiJQTCIsInN1YiI6IjU0NTQ3MjYwIiwicmlkIjoiZWJmZjU1OTMtNjAyMy00NDNhLTk0MDItMTI4OWIxODViMzYzIiwiaWF0IjoxNzM5NDg1ODkyfQ.mLagME8SVytDr63gLp_fd_fCS8Wk70gdwJx7LJOm008",
      "uri": "https://api.soundcloud.com/tracks/764804143",
      "urn": "soundcloud:tracks:764804143",
      "media": {
        "transcodings": [
          {
            "duration": 218645,
            "format": {
              "mime_type": "audio/mpeg",
              "protocol": "hls"
            },
            "is_legacy_transcoding": true,
            "preset": "mp3_0_1",
            "quality": "sq",
            "snipped": false,
            "url": "https://api-v2.soundcloud.com/media/soundcloud:tracks:764804143/ad016e0f-a7bf-4f6c-844d-bbceb8de005b/stream/hls"
          },
          {
            "duration": 218645,
            "format": {
              "mime_type": "audio/mpeg",
              "protocol": "progressive"
            },
            "is_legacy_transcoding": true,
            "preset": "mp3_0_1",
            "quality": "sq",
            "snipped": false,
            "url": "https://api-v2.soundcloud.com/media/soundcloud:tracks:764804143/ad016e0f-a7bf-4f6c-844d-bbceb8de005b/stream/progressive"
          },
          {
            "duration": 218625,
            "format": {
              "mime_type": "audio/ogg; codecs=\"opus\"",
              "protocol": "hls"
            },
            "is_legacy_transcoding": true,
            "preset": "opus_0_0",
            "quality": "sq",
            "snipped": false,
            "url": "https://api-v2.soundcloud.com/media/soundcloud:tracks:764804143/f7ac9441-f76d-491d-a0fe-c822b03d87f1/stream/hls"
          }
        ]
      },
        */
    }
}