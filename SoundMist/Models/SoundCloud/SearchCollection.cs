using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud
{
    public class SearchCollection
    {
        [JsonPropertyName("collection")]
        public List<object> Collection { get; set; } = [];

        [JsonPropertyName("next_href")]
        public string? NextHref { get; set; } = null!;

        [JsonPropertyName("query_urn")]
        public string QueryUrn { get; set; } = null!;

        [JsonPropertyName("total_results")]
        public int TotalResults { get; set; }

        [JsonPropertyName("facets")]
        public List<Facet> Facets { get; set; } = [];
    }
}
