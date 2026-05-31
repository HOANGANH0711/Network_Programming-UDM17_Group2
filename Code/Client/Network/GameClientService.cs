using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Models;
using Shared.DTO;
using System.Text.Json;

namespace Client.Network
{
    public class GameClientService
    {
        // EVENTS - Cac form/UI se lang nghe cac event nay
        // Khi GameClientService nhan duoc Packet bang event thi UI se tu cap nhat

        public event Action<List<UserDTO>>? OnPlayerListReceived; //nhan danh sach nguoi choi
        public event Action<List<RoomDTO>>? OnRoomListReceived; //nhan danh sach phong
        public event Action<GameDTO>? OnGameStarted; //Game bat dau
        public event Action<MoveDTO>? OnMoveMade; // co nuoc di moi
        public event Action<string>? OnGameEnded; //Game ket thuc, thong bao nguoi thang
        public event Action<string>? OnError; //Thong bao loi
        public event Action<UserDTO>? OnInviteReveived; // nhan loi moi

        //Goi ham moi khi nhan duoc packet tu server

        public void HandlePacket(Packet packet)
        {
            switch (packet.Command)
            {
                // nhan danh sach nguoi choi trong lobby
                case CommandType.PLAYER_LIST:
                    var players = JsonSerializer.Deserialize<List<UserDTO>>(packet.Data);
                    if (players != null) OnPlayerListReceived?.Invoke(players);
                    break;
                //nhanh danh sach phong
                case CommandType.ROOM_LIST:
                    var rooms = JsonSerializer.Deserialize<List<RoomDTO>>(packet.Data);
                    if (rooms != null) OnRoomListReceived?.Invoke(rooms);
                    break;
                // nhan loi moi
                case CommandType.INVITE:
                    var inviter = JsonSerializer.Deserialize<UserDTO>(packet.Data);
                    if (inviter != null) OnInviteReveived?.Invoke(inviter);
                    break;
                // Gamr bat dau
                case CommandType.GAME_START:
                    var game = JsonSerializer.Deserialize<GameDTO>(packet.Data);
                    if (game != null) OnGameStarted?.Invoke(game);
                    break;
                // co nuoc do moi 
                case CommandType.GAME_MOVE:
                    var move = JsonSerializer.Deserialize<MoveDTO>(packet.Data);
                    if (move != null) OnMoveMade?.Invoke(move);
                    break;

                case CommandType.GAME_END:
                    OnGameEnded?.Invoke(packet.Data); // packet.Data chua ten nguoi thang
                    break;
                case CommandType.ERROR:
                    OnError?.Invoke(packet.Data); // packet.Data chua thong bao loi
                    break;
            }
        }

    }
}
