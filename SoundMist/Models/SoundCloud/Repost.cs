using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class Repost : UserEntry, IJsonOnDeserialized
{
    public void OnDeserialized()
    {
        if (Track is not null)
            Track.RepostingUser = User;
        if (Playlist is not null)
            Playlist.RepostedByUser = User;
    }
}