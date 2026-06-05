using Shared.Enums;

namespace Shared.Models
{
    public static class PacketHelper
    {
        public static string Serialize(Packet packet)
        {
            return Serializer.Serialize(packet);
        }

        public static Packet Deserialize(string json)
        {
            return Serializer.Deserialize(json);
        }

        public static Packet Create<T>(CommandType cmd, T data, string senderID = null)
        {
            return Serializer.Create(cmd, data, senderID);
        }
    }
}
