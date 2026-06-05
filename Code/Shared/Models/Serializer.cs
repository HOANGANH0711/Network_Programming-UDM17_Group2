#nullable enable
using System.Text.Json;

namespace Shared.Models
{
    public static class Serializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        public static T? Deserialize<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }
}
