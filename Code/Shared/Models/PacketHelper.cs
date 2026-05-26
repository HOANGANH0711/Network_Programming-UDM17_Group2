using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Shared.Enums;
using System.Threading.Tasks;

namespace Shared.Models
{
    public static class PacketHelper
    {
        // Chuyển Packet → chuỗi JSON để gửi đi
        public static string Serialize(Packet packet)
        {
            return JsonSerializer.Serialize(packet);
        }

        // Chuyển chuỗi JSON nhận được → Packet
        public static Packet Deserialize(string json)
        {
            return JsonSerializer.Deserialize<Packet>(json);
        }

        // Tạo nhanh một Packet để gửi object bất kỳ
        public static Packet Create<T>(CommandType cmd, T data, string senderID = null)
        {
            return new Packet
            {
                Command = cmd,
                Data = JsonSerializer.Serialize(data),
                SenderID = senderID
            };
        }
    }
}
