using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class Media
{
    [JsonPropertyName("transcodings")]
    public List<Transcoding> Transcodings { get; set; } = null!;
}