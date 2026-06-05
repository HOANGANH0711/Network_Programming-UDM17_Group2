using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared.Enums;
using System.Threading.Tasks;

namespace Shared.Models
{
    public static class PacketHelper
    {
        // Chuyển Packet → chuỗi JSON để gửi đi
        public static string Serialize(Packet packet)
        {
            return Serializer.Serialize(packet);
        }

        // Chuyển chuỗi JSON nhận được → Packet
        public static Packet Deserialize(string json)
        {
            return Serializer.Deserialize<Packet>(json);
        }

        // Tạo nhanh một Packet để gửi object bất kỳ
        public static Packet Create<T>(CommandType cmd, T data, string senderID = null)
        {
            return new Packet
            {
                Command = cmd,
                Data = Serializer.Serialize(data),
                SenderID = senderID
            };
        }
    }
}
