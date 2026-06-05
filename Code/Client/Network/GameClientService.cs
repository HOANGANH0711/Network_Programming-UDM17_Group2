using Shared.DTO;
using Shared.Enums;
using Shared.Models;

namespace Client.Network
{
    public class GameClientService
    {
        public static GameClientService Instance { get; } = new GameClientService();

        private readonly ClientSocket _socket = new ClientSocket();

        public string PlayerId { get; private set; } = "";
        public string PlayerName { get; private set; } = "";
        public GameStateDto? CurrentGame { get; private set; }
        public List<UserDTO> LastPlayers { get; private set; } = new List<UserDTO>();
        public List<HistoryItemDto> LastHistory { get; private set; } = new List<HistoryItemDto>();

        public event Action? OnLoginSuccess;
        public event Action<List<UserDTO>>? OnPlayerListReceived;
        public event Action<List<RoomDTO>>? OnRoomListReceived;
        public event Action<GameStateDto>? OnGameStarted;
        public event Action<GameStateDto>? OnGameState;
        public event Action<MoveDTO>? OnMoveMade;
        public event Action<GameStateDto>? OnGameEnded;
        public event Action<ChatDto>? OnChatReceived;
        public event Action<InviteDto>? OnInviteReceived;
        public event Action<CommandType, string>? OnDrawMessage;
        public event Action<List<HistoryItemDto>>? OnHistoryReceived;
        public event Action<string>? OnError;
        public event Action<string>? OnDisconnected;

        private GameClientService()
        {
            _socket.PacketReceived += HandlePacket;
            _socket.Disconnected += message => OnDisconnected?.Invoke(message);
        }

        public async Task ConnectAndLoginAsync(string ip, int port, string playerName)
        {
            PlayerName = playerName;
            await _socket.ConnectAsync(ip, port);
            await SendAsync(CommandType.LOGIN, playerName);
        }

        public Task SendAsync<T>(CommandType command, T data)
        {
            return _socket.SendAsync(PacketHelper.Create(command, data, PlayerId));
        }

        public void HandlePacket(Packet packet)
        {
            switch (packet.Command)
            {
                case CommandType.SUCCESS:
                    if (!string.IsNullOrWhiteSpace(packet.SenderID))
                        PlayerId = packet.SenderID;
                    OnLoginSuccess?.Invoke();
                    _ = SendAsync(CommandType.GET_PLAYER_LIST, "");
                    break;

                case CommandType.PLAYER_LIST:
                case CommandType.LOBBY_UPDATE:
                    var players = Serializer.Deserialize<List<UserDTO>>(packet.Data);
                    if (players is not null)
                    {
                        LastPlayers = players;
                        OnPlayerListReceived?.Invoke(players);
                    }
                    break;

                case CommandType.ROOM_LIST:
                    var rooms = Serializer.Deserialize<List<RoomDTO>>(packet.Data);
                    if (rooms is not null)
                        OnRoomListReceived?.Invoke(rooms);
                    break;

                case CommandType.INVITE:
                    var invite = Serializer.Deserialize<InviteDto>(packet.Data);
                    if (invite is not null)
                        OnInviteReceived?.Invoke(invite);
                    break;

                case CommandType.GAME_START:
                    var started = Serializer.Deserialize<GameStateDto>(packet.Data);
                    if (started is not null)
                    {
                        CurrentGame = started;
                        OnGameStarted?.Invoke(started);
                    }
                    break;

                case CommandType.GAME_MOVE:
                    var move = Serializer.Deserialize<MoveDTO>(packet.Data);
                    if (move is not null)
                        OnMoveMade?.Invoke(move);
                    break;

                case CommandType.GAME_STATE:
                case CommandType.TIMER_UPDATE:
                    var state = Serializer.Deserialize<GameStateDto>(packet.Data);
                    if (state is not null)
                    {
                        CurrentGame = state;
                        OnGameState?.Invoke(state);
                    }
                    break;

                case CommandType.GAME_CHAT:
                    var chat = Serializer.Deserialize<ChatDto>(packet.Data);
                    if (chat is not null)
                        OnChatReceived?.Invoke(chat);
                    break;

                case CommandType.DRAW_REQUEST:
                case CommandType.DRAW_ACCEPT:
                case CommandType.DRAW_DECLINE:
                    OnDrawMessage?.Invoke(packet.Command, packet.SenderID);
                    break;

                case CommandType.GAME_END:
                case CommandType.GAME_RESULT:
                    var ended = Serializer.Deserialize<GameStateDto>(packet.Data);
                    if (ended is not null)
                    {
                        CurrentGame = ended;
                        OnGameEnded?.Invoke(ended);
                    }
                    break;

                case CommandType.HISTORY_DATA:
                    var history = Serializer.Deserialize<List<HistoryItemDto>>(packet.Data);
                    if (history is not null)
                    {
                        LastHistory = history;
                        OnHistoryReceived?.Invoke(history);
                    }
                    break;

                case CommandType.ERROR:
                    OnError?.Invoke(packet.Data);
                    break;
            }
        }
    }
}
