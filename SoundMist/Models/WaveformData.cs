using System.Text.Json.Serialization;

namespace SoundMist.Models
{
    public class WaveformData
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("samples")]
        public int[] Samples { get; set; } = [];
    }
}