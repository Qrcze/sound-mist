using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class Visuals
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("tracking")]
    public object? Tracking { get; set; }

    [JsonPropertyName("urn")]
    public string Urn { get; set; } = string.Empty;

    [JsonPropertyName("visuals")]
    public List<VisualItem> Items { get; set; } = [];
}