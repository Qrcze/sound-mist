using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud
{
    public class ScObjectConverter(string typePropertyName = "kind") : JsonConverter<object>
    {
        private readonly string _typePropertyName = typePropertyName;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            string raw = root.GetRawText();
            if (root.TryGetProperty(_typePropertyName, out JsonElement typeProperty))
            {
                string type = typeProperty.GetString()!;
                return type switch
                {
                    "track" => JsonSerializer.Deserialize<Track>(root.GetRawText(), options),
                    "track-repost" => JsonSerializer.Deserialize<Track>(root.GetRawText(), options),
                    "user" => JsonSerializer.Deserialize<User>(root.GetRawText(), options),
                    "playlist" => JsonSerializer.Deserialize<Playlist>(root.GetRawText(), options),
                    "playlist-repost" => JsonSerializer.Deserialize<Playlist>(root.GetRawText(), options),
                    _ => $"unhandled type: {type}...",
                };
            }
            else
            {
                throw new JsonException($"Missing '{_typePropertyName}' property while reading json object: {raw}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(value, value.GetType(), options);
        }
    }
}