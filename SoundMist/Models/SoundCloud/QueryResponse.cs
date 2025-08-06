using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class QueryResponse<T>
{
    [JsonPropertyName("collection")]
    public List<T> Collection { get; set; } = [];

    [JsonPropertyName("next_href")]
    public string? NextHref { get; set; }

    [JsonPropertyName("query_urn")]
    public string? QueryUrn { get; set; }
        
    [JsonPropertyName("variant")]
    public string? Variant { get; set; }
}