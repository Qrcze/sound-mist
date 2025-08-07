using SoundMist.Models;

namespace SCPlayerTests
{
    public class ProgramSettingsTests
    {
        [Fact]
        public void CheckLoading_CurrentVersion_1()
        {
            string json = """
                {
                    "AlternativeWindowsMediaKeysHandling": true,
                    "AppColorTheme": 0,
                    "AuthToken": "0-000000-00000000-000000000000000",
                    "AutoplayStationOnLastTrack": false,
                    "BlockedTracks": {
                        "100": "Test Track - One",
                        "101": "Test Track - Two"
                    },
                    "BlockedUsers": {
                        "200": "Test User 1",
                        "201": "Test User 2"
                    },
                    "HistoryLimit": 50,
                    "LastTrackId": 2128126998,
                    "ProxyHost": "127.0.0.1",
                    "ProxyMode": 1,
                    "ProxyPort": 8080,
                    "ProxyProtocol": 0,
                    "Shuffle": true,
                    "StartingTabIndex": 0,
                    "StartPlayingOnLaunch": false,
                    "Version": 1,
                    "Volume": 1
                }
                """;

            var settings = ProgramSettings.GetUpdatedSettings(json);
            Assert.NotNull(settings);
            Assert.Equal(ProgramSettings.SettingsVersion, settings.Version);
        }

        [Fact]
        public void CheckUpdatingFromVersion0()
        {
            string json = """
                {
                    "BlockedTracks": [
                        {
                            "Id": 100,
                            "Title": "Test Track - One"
                        },
                        {
                            "Id": 101,
                            "Title": "Test Track - Two"
                        }
                    ],
                    "BlockedUsers": [
                        {
                            "Id": 200,
                            "Title": "Test User 1"
                        },
                        {
                            "Id": 201,
                            "Title": "Test User 2"
                        }
                    ]
                }
                """;

            var settings = ProgramSettings.GetUpdatedSettings(json);
            Assert.NotNull(settings);
            Assert.Equal(ProgramSettings.SettingsVersion, settings.Version);
        }
    }
}