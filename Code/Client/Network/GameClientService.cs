using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.DTO;
using Shared.Enums;
using Shared.Models;
using PacketModel = Shared.Models.Packet;

namespace Client.Network
{
    public class GameClientService : IDisposable
    {
        private readonly ClientSocket socket = new ClientSocket();

        public string CurrentUserId { get; private set; } = string.Empty;
        public string CurrentUserName { get; private set; } = string.Empty;

        public event Action<List<UserDTO>>? OnPlayerListReceived;
        public event Action<List<RoomDTO>>? OnRoomListReceived;
        public event Action<GameDTO>? OnGameStarted;
        public event Action<MoveDTO>? OnMoveMade;
        public event Action<ChatMessageDTO>? OnChatReceived;
        public event Action<DrawOfferDTO>? OnDrawRequested;
        public event Action<List<GameHistoryDTO>>? OnHistoryReceived;
        public event Action<TimerUpdateDTO>? OnTimerUpdated;
        public event Action<string>? OnGameEnded;
        public event Action<string>? OnError;
        public event Action<InviteDTO>? OnInviteReceived;
        public event Action<string>? OnSuccess;
        public event Action? OnDisconnected;

        public GameClientService()
        {
            socket.OnPacketReceived += HandlePacket;
            socket.OnError += message => OnError?.Invoke(message);
            socket.OnDisconnected += () => OnDisconnected?.Invoke();
        }

        public bool IsConnected => socket.IsConnected;

        public async Task ConnectAsync(string ip, int port)
        {
            await socket.ConnectAsync(ip, port);
        }

        public async Task LoginAsync(string userName)
        {
            CurrentUserName = userName.Trim();
            CurrentUserId = CurrentUserName;

            await SendAsync(CommandType.LOGIN, CurrentUserName);
        }

        public Task RequestPlayerListAsync()
        {
            return SendAsync(CommandType.GET_PLAYER_LIST, string.Empty);
        }

        public Task RequestRoomListAsync()
        {
            return SendAsync(CommandType.GET_ROOM_LIST, string.Empty);
        }

        public Task InviteAsync(UserDTO player, int turnSeconds, bool inviterPlaysX)
        {
            InviteDTO invite = new InviteDTO
            {
                Inviter = new UserDTO
                {
                    UserID = CurrentUserId,
                    UserName = CurrentUserName,
                    IsOnline = true
                },
                Target = player,
                TurnSeconds = turnSeconds,
                InviterPlaysX = inviterPlaysX
            };

            return SendAsync(CommandType.INVITE, invite);
        }

        public Task SendInviteResponseAsync(InviteDTO invite, bool accepted)
        {
            return SendAsync(CommandType.INVITE_RESPONSE, new InviteResponseDTO
            {
                Invite = invite,
                Accepted = accepted
            });
        }

        public Task CreateRoomAsync()
        {
            return SendAsync(CommandType.CREATE_ROOM, CurrentUserName);
        }

        public Task JoinRoomAsync(RoomDTO room)
        {
            return SendAsync(CommandType.JOIN_ROOM, room);
        }

        public Task SendMoveAsync(int row, int col, string gameId = "")
        {
            MoveDTO move = new MoveDTO
            {
                GameID = gameId,
                PlayerID = CurrentUserId,
                Row = row,
                Col = col
            };

            return SendAsync(CommandType.MAKE_MOVE, move);
        }

        public Task SendChatAsync(string gameId, string message)
        {
            ChatMessageDTO chat = new ChatMessageDTO
            {
                GameID = gameId,
                SenderID = CurrentUserId,
                SenderName = CurrentUserName,
                Message = message
            };

            return SendAsync(CommandType.GAME_CHAT, chat);
        }

        public Task RequestDrawAsync(string gameId)
        {
            DrawOfferDTO offer = new DrawOfferDTO
            {
                GameID = gameId,
                FromPlayerID = CurrentUserId
            };

            return SendAsync(CommandType.DRAW_REQUEST, offer);
        }

        public Task SurrenderAsync(string gameId)
        {
            return SendAsync(CommandType.SURRENDER, gameId);
        }

        public Task LeaveGameTemporarilyAsync(string gameId)
        {
            return SendAsync(CommandType.LEAVE_ROOM, gameId);
        }

        public Task ReturnGameAsync(string gameId)
        {
            return SendAsync(CommandType.JOIN_ROOM, gameId);
        }

        public Task StartBotGameAsync(string difficulty)
        {
            return SendAsync(CommandType.START_BOT_GAME, new BotGameRequestDTO
            {
                Difficulty = difficulty
            });
        }

        public Task RespondDrawAsync(string gameId, string fromPlayerId, bool accepted)
        {
            DrawOfferDTO response = new DrawOfferDTO
            {
                GameID = gameId,
                FromPlayerID = fromPlayerId,
                ToPlayerID = CurrentUserId,
                Accepted = accepted
            };

            return SendAsync(CommandType.DRAW_RESPONSE, response);
        }

        public Task RequestHistoryAsync()
        {
            return SendAsync(CommandType.GET_HISTORY, CurrentUserId);
        }

        private Task SendAsync<T>(CommandType command, T data)
        {
            PacketModel packet = Serializer.Create(command, data, CurrentUserId);
            return socket.SendAsync(packet);
        }

        private void HandlePacket(PacketModel packet)
        {
            switch (packet.Command)
            {
                case CommandType.PLAYER_LIST:
                case CommandType.LOBBY_UPDATE:
                    List<UserDTO>? players = Serializer.DeserializeData<List<UserDTO>>(packet.Data);
                    if (players != null)
                        OnPlayerListReceived?.Invoke(players);
                    break;

                case CommandType.ROOM_LIST:
                    List<RoomDTO>? rooms = Serializer.DeserializeData<List<RoomDTO>>(packet.Data);
                    if (rooms != null)
                        OnRoomListReceived?.Invoke(rooms);
                    break;

                case CommandType.INVITE:
                    InviteDTO? invite = Serializer.DeserializeData<InviteDTO>(packet.Data);
                    if (invite != null)
                        OnInviteReceived?.Invoke(invite);
                    break;

                case CommandType.GAME_START:
                    GameDTO? game = Serializer.DeserializeData<GameDTO>(packet.Data);
                    if (game != null)
                        OnGameStarted?.Invoke(game);
                    break;

                case CommandType.GAME_MOVE:
                case CommandType.MOVE_RESULT:
                    MoveDTO? move = Serializer.DeserializeData<MoveDTO>(packet.Data);
                    if (move != null)
                        OnMoveMade?.Invoke(move);
                    break;

                case CommandType.GAME_CHAT:
                    ChatMessageDTO? chat = Serializer.DeserializeData<ChatMessageDTO>(packet.Data);
                    if (chat != null)
                        OnChatReceived?.Invoke(chat);
                    break;

                case CommandType.DRAW_REQUEST:
                    DrawOfferDTO? offer = Serializer.DeserializeData<DrawOfferDTO>(packet.Data);
                    if (offer != null)
                        OnDrawRequested?.Invoke(offer);
                    break;

                case CommandType.HISTORY_DATA:
                    List<GameHistoryDTO>? history = Serializer.DeserializeData<List<GameHistoryDTO>>(packet.Data);
                    if (history != null)
                        OnHistoryReceived?.Invoke(history);
                    break;

                case CommandType.TIMER_UPDATE:
                    TimerUpdateDTO? timer = Serializer.DeserializeData<TimerUpdateDTO>(packet.Data);
                    if (timer != null)
                        OnTimerUpdated?.Invoke(timer);
                    break;

                case CommandType.GAME_END:
                case CommandType.GAME_WIN:
                case CommandType.GAME_LOSE:
                case CommandType.GAME_DRAW:
                case CommandType.GAME_RESULT:
                    OnGameEnded?.Invoke(Serializer.DeserializeData<string>(packet.Data) ?? packet.Data);
                    break;

                case CommandType.SUCCESS:
                    OnSuccess?.Invoke(Serializer.DeserializeData<string>(packet.Data) ?? packet.Data);
                    break;

                case CommandType.ERROR:
                    OnError?.Invoke(Serializer.DeserializeData<string>(packet.Data) ?? packet.Data);
                    break;
            }
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}
