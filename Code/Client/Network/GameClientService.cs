using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Models;
using Shared.DTO;
using System.Text.Json;
using System.Net.Sockets;
using System.IO;
using System.Windows.Forms;

namespace Client.Network
{
    public class GameClientService
    {
        private TcpClient? client;
        private StreamReader? reader;
        private StreamWriter? writer;

        // EVENTS - Cac form/UI se lang nghe cac event nay
        // Khi GameClientService nhan duoc Packet bang event thi UI se tu cap nhat

        public event Action<List<UserDTO>>? OnPlayerListReceived;
        public event Action<List<RoomDTO>>? OnRoomListReceived;
        public event Action<GameDTO>? OnGameStarted;
        public event Action<MoveDTO>? OnMoveMade;
        public event Action<string>? OnGameEnded;
        public event Action<string>? OnError;
        public event Action<UserDTO>? OnInviteReveived;

        // Ket noi server
        public void Connect(string ip, int port)
        {
            client = new TcpClient(ip, port);

            reader = new StreamReader(client.GetStream());

            writer = new StreamWriter(client.GetStream());

            writer.AutoFlush = true;
        }

        // Test gui packet len server
        public void TestSend()
        {
            if (writer == null)
            {
                MessageBox.Show("Chua ket noi server");
                return;
            }

            Packet packet = new Packet()
            {
                Command = CommandType.LOGIN,
                Data = "Khoa"
            };

            string json = JsonSerializer.Serialize(packet);

            writer.WriteLine(json);

            MessageBox.Show("Da gui packet len server");
        }

        // Goi ham moi khi nhan duoc packet tu server
        public void HandlePacket(Packet packet)
        {
            switch (packet.Command)
            {
                // nhan danh sach nguoi choi trong lobby
                case CommandType.PLAYER_LIST:
                    var players = JsonSerializer.Deserialize<List<UserDTO>>(packet.Data);

                    if (players is not null)
                        OnPlayerListReceived?.Invoke(players);

                    break;

                // nhan danh sach phong
                case CommandType.ROOM_LIST:
                    var rooms = JsonSerializer.Deserialize<List<RoomDTO>>(packet.Data);

                    if (rooms is not null)
                        OnRoomListReceived?.Invoke(rooms);

                    break;

                // nhan loi moi
                case CommandType.INVITE:
                    var inviter = JsonSerializer.Deserialize<UserDTO>(packet.Data);

                    if (inviter is not null)
                        OnInviteReveived?.Invoke(inviter);

                    break;

                // game bat dau
                case CommandType.GAME_START:
                    var game = JsonSerializer.Deserialize<GameDTO>(packet.Data);

                    if (game is not null)
                        OnGameStarted?.Invoke(game);

                    break;

                // co nuoc di moi
                case CommandType.GAME_MOVE:
                    var move = JsonSerializer.Deserialize<MoveDTO>(packet.Data);

                    if (move is not null)
                        OnMoveMade?.Invoke(move);

                    break;

                // game ket thuc
                case CommandType.GAME_END:
                    OnGameEnded?.Invoke(packet.Data);

                    break;

                // bao loi
                case CommandType.ERROR:
                    OnError?.Invoke(packet.Data);

                    break;
            }
        }

        // Gui loi moi choi co tuong toi nguoi choi khac
        public void SendInvite(string targetName)
        {
            if (writer == null) return;

            var packet = new Packet()
            {
                Command = CommandType.INVITE,
                Data = targetName
            };

            writer.WriteLine(JsonSerializer.Serialize(packet));
        }

        // Tra loi loi moi: true = chap nhan, false = tu choi
        public void SendInviteResponse(bool accepted)
        {
            if (writer == null) return;

            var packet = new Packet()
            {
                Command = CommandType.INVITE_RESPONSE,
                Data = accepted ? "true" : "false"
            };

            writer.WriteLine(JsonSerializer.Serialize(packet));
        }
    }
}