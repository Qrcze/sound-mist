using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud
{
    public class SearchQuery
    {
        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;
    }
}
