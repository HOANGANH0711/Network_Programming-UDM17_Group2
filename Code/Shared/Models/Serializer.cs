using System.Text.Json;
using Shared.Enums;

namespace Shared.Models
{
    public static class Serializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static string Serialize(Packet packet)
        {
            return JsonSerializer.Serialize(packet, Options);
        }

        public static Packet Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<Packet>(json, Options);
        }

        public static string SerializeData<T>(T data)
        {
            return JsonSerializer.Serialize(data, Options);
        }

        public static T DeserializeData<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, Options);
        }

        public static Packet Create<T>(CommandType command, T data, string senderId = null)
        {
            return new Packet
            {
                Command = command,
                Data = SerializeData(data),
                SenderID = senderId ?? string.Empty
            };
        }
    }
}
