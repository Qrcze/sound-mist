using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class Facet
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("facets")]
    public List<Facet> Facets { get; set; } = [];

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("filter")]
    public string Filter { get; set; } = string.Empty;
}