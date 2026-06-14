using System.Net;
using System.Net.Sockets;
using Server.GameLogic;
using Server.Handlers;
using Shared.DTO;
using Shared.Enums;
using Shared.Models;

namespace Server.Core
{
    public class ServerManager
    {
        private readonly int _serverPort;
        private readonly object _clientLock = new object();
        private readonly object _gameLock = new object();
        private readonly List<ClientHandler> _connectedClients = new List<ClientHandler>();
        private readonly Dictionary<string, ActiveGame> _games = new Dictionary<string, ActiveGame>();
        private readonly MatchmakingService _matchmaking = new MatchmakingService();
        private readonly MatchHistoryRepository _historyRepository = new MatchHistoryRepository();
        private TcpListener? _tcpListener;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isRunning;

        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

        public ServerManager(int port = 5000)
        {
            _serverPort = port;
        }

        public async Task StartAsync()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, _serverPort);
                _tcpListener.Start();
                _isRunning = true;
                LogInfo($"Server started on port {_serverPort}");
                await AcceptClientsAsync();
            }
            catch (Exception ex)
            {
                LogError($"Error starting server: {ex.Message}");
                _isRunning = false;
            }
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_tcpListener == null)
                        break;

                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    _ = HandleClientConnectionAsync(tcpClient);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"Error accepting client connection: {ex.Message}");
                }
            }
        }

        private async Task HandleClientConnectionAsync(TcpClient tcpClient)
        {
            var clientId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var clientHandler = new ClientHandler(tcpClient, clientId);
            lock (_clientLock)
                _connectedClients.Add(clientHandler);

            clientHandler.MessageReceived += async (_, e) =>
            {
                if (e.Packet != null)
                    await HandlePacketAsync(clientHandler, e.Packet);
            };

            clientHandler.Disconnected += async (_, _) => await RemoveClientAsync(clientHandler);

            var remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
            LogInfo($"[{clientId}] Client connected from {remoteEndPoint?.Address}:{remoteEndPoint?.Port}");
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs
            {
                ClientId = clientId,
                RemoteAddress = remoteEndPoint?.Address.ToString(),
                RemotePort = remoteEndPoint?.Port ?? 0
            });

            await clientHandler.HandleAsync();
        }

        private async Task HandlePacketAsync(ClientHandler client, Packet packet)
        {
            try
            {
                switch (packet.Command)
                {
                    case CommandType.LOGIN:
                        await LoginAsync(client, packet.Data);
                        break;
                    case CommandType.GET_PLAYER_LIST:
                    case CommandType.RETURN_TO_LOBBY:
                        await SendLobbyAsync(client);
                        break;
                    case CommandType.INVITE:
                    case CommandType.CHALLENGE:
                        await SendInviteAsync(client, packet.Data);
                        break;
                    case CommandType.INVITE_RESPONSE:
                        await HandleInviteResponseAsync(client, packet.Data);
                        break;
                    case CommandType.MAKE_MOVE:
                        await HandleMoveAsync(client, packet.Data);
                        break;
                    case CommandType.GAME_CHAT:
                        await BroadcastToGameAsync(client, packet.Data, CommandType.GAME_CHAT);
                        break;
                    case CommandType.RESIGN:
                        await EndByResignAsync(client);
                        break;
                    case CommandType.DRAW_REQUEST:
                    case CommandType.DRAW_ACCEPT:
                    case CommandType.DRAW_DECLINE:
                        await BroadcastToGameAsync(client, packet.Data, packet.Command);
                        if (packet.Command == CommandType.DRAW_ACCEPT)
                            await EndGameAsync(GetGameByPlayer(client.ClientId), "", "Hoa do chap nhan cau hoa");
                        break;
                    case CommandType.GET_HISTORY:
                        await SendHistoryAsync(client);
                        break;
                    case CommandType.START_BOT_GAME:
                        await StartBotGameAsync(client, packet.Data);
                        break;
                    case CommandType.LOGOUT:
                        await RemoveClientAsync(client);
                        break;
                }
            }
            catch (Exception ex)
            {
                await client.SendPacketAsync(PacketHelper.Create(CommandType.ERROR, ex.Message));
            }
        }

        private async Task LoginAsync(ClientHandler client, string username)
        {
            _matchmaking.Login(client.ClientId, username);
            await client.SendPacketAsync(PacketHelper.Create(CommandType.SUCCESS, "Logged in", client.ClientId));
            await BroadcastLobbyAsync();
        }

        private async Task SendInviteAsync(ClientHandler client, string data)
        {
            var request = Serializer.Deserialize<InviteDto>(data);
            if (request == null)
                return;

            var invite = _matchmaking.CreateInvite(client.ClientId, request);
            var target = FindClient(invite.ToPlayerId);
            if (target != null)
                await target.SendPacketAsync(PacketHelper.Create(CommandType.INVITE, invite, client.ClientId));
        }

        private async Task HandleInviteResponseAsync(ClientHandler client, string data)
        {
            var response = Serializer.Deserialize<InviteResponseDto>(data);
            if (response == null)
                return;

            var invite = _matchmaking.TakeInvite(response.FromPlayerId, client.ClientId);
            var inviter = FindClient(response.FromPlayerId);
            if (!response.Accepted)
            {
                if (inviter != null)
                    await inviter.SendPacketAsync(PacketHelper.Create(CommandType.INVITE_RESPONSE, response));
                return;
            }

            if (invite != null)
                await StartOnlineGameAsync(invite);
        }

        private async Task StartOnlineGameAsync(InviteDto invite)
        {
            var xId = invite.InviterSymbol == "X" ? invite.FromPlayerId : invite.ToPlayerId;
            var oId = invite.InviterSymbol == "X" ? invite.ToPlayerId : invite.FromPlayerId;
            var game = ActiveGame.CreateOnline(xId, _matchmaking.GetName(xId), oId, _matchmaking.GetName(oId), invite.TurnSeconds);
            AddGame(game);
            await BroadcastGameStateAsync(game, CommandType.GAME_START);
            game.StartTimer(() => _ = TickGameAsync(game.GameID));
            await BroadcastLobbyAsync();
        }

        private async Task StartBotGameAsync(ClientHandler client, string data)
        {
            var request = Serializer.Deserialize<BotGameRequestDto>(data) ?? new BotGameRequestDto();
            var playerIsX = request.PlayerSymbol != "O";
            var xId = playerIsX ? client.ClientId : ActiveGame.BotId;
            var oId = playerIsX ? ActiveGame.BotId : client.ClientId;
            var game = ActiveGame.CreateBot(
                xId,
                xId == client.ClientId ? _matchmaking.GetName(client.ClientId) : "Bot",
                oId,
                oId == client.ClientId ? _matchmaking.GetName(client.ClientId) : "Bot",
                request.TurnSeconds,
                request.Difficulty);

            AddGame(game);
            await BroadcastGameStateAsync(game, CommandType.GAME_START);
            game.StartTimer(() => _ = TickGameAsync(game.GameID));
            await MaybeBotMoveAsync(game);
            await BroadcastLobbyAsync();
        }

        private async Task HandleMoveAsync(ClientHandler client, string data)
        {
            var move = Serializer.Deserialize<MoveDTO>(data);
            var game = GetGameByPlayer(client.ClientId);
            if (move == null || game == null || game.IsGameOver)
                return;

            var symbol = game.SymbolOf(client.ClientId);
            if (game.CurrentTurnID != client.ClientId || string.IsNullOrEmpty(symbol))
            {
                await client.SendPacketAsync(PacketHelper.Create(CommandType.ERROR, "Chua toi luot hoac ban khong dung quan cua minh."));
                return;
            }

            if (!game.PlaceMove(client.ClientId, symbol, move.Row, move.Col))
            {
                await client.SendPacketAsync(PacketHelper.Create(CommandType.ERROR, "Nuoc di khong hop le."));
                return;
            }

            await BroadcastGameStateAsync(game, CommandType.GAME_STATE);

            if (game.HasWinner(move.Row, move.Col, symbol))
                await EndGameAsync(game, client.ClientId, $"{_matchmaking.GetName(client.ClientId)} thang do du 5 quan lien tiep");
            else if (game.IsBoardFull())
                await EndGameAsync(game, "", "Hoa do ban co day");
            else
            {
                game.SwitchTurn();
                await BroadcastGameStateAsync(game, CommandType.GAME_STATE);
                await MaybeBotMoveAsync(game);
            }
        }

        private async Task MaybeBotMoveAsync(ActiveGame game)
        {
            if (!game.IsBotGame || game.CurrentTurnID != ActiveGame.BotId || game.IsGameOver)
                return;

            await Task.Delay(450);
            var (row, col) = game.ChooseBotMove();
            if (row >= 0 && game.PlaceMove(ActiveGame.BotId, game.SymbolOf(ActiveGame.BotId), row, col))
            {
                await BroadcastGameStateAsync(game, CommandType.GAME_STATE);
                if (game.HasWinner(row, col, game.SymbolOf(ActiveGame.BotId)))
                    await EndGameAsync(game, ActiveGame.BotId, "Bot thang");
                else if (game.IsBoardFull())
                    await EndGameAsync(game, "", "Hoa do ban co day");
                else
                {
                    game.SwitchTurn();
                    await BroadcastGameStateAsync(game, CommandType.GAME_STATE);
                }
            }
        }

        private async Task TickGameAsync(string gameId)
        {
            var game = GetGame(gameId);
            if (game == null || game.IsGameOver)
                return;

            game.TimeRemaining--;
            if (game.TimeRemaining <= 0)
            {
                var loser = game.CurrentTurnID;
                var winner = game.OpponentOf(loser);
                await EndGameAsync(game, winner, $"{_matchmaking.GetName(loser)} het thoi gian va bi xu thua");
                return;
            }

            await BroadcastGameStateAsync(game, CommandType.TIMER_UPDATE);
        }

        private async Task EndByResignAsync(ClientHandler client)
        {
            var game = GetGameByPlayer(client.ClientId);
            if (game != null)
                await EndGameAsync(game, game.OpponentOf(client.ClientId), $"{_matchmaking.GetName(client.ClientId)} dau hang");
        }

        private async Task EndGameAsync(ActiveGame? game, string winnerId, string result)
        {
            if (game == null || game.IsGameOver)
                return;

            game.IsGameOver = true;
            game.WinnerID = winnerId;
            game.ResultText = result;
            game.StopTimer();

            _matchmaking.MarkInGame(game.PlayerXID, false);
            _matchmaking.MarkInGame(game.PlayerOID, false);
            _historyRepository.SaveGame(game, _matchmaking.GetName);

            await BroadcastGameStateAsync(game, CommandType.GAME_END);
            await BroadcastLobbyAsync();
        }

        private async Task BroadcastToGameAsync(ClientHandler client, string data, CommandType command)
        {
            var game = GetGameByPlayer(client.ClientId);
            if (game == null)
                return;

            var packet = new Packet { Command = command, Data = data, SenderID = client.ClientId };
            await SendToPlayerAsync(game.PlayerXID, packet);
            await SendToPlayerAsync(game.PlayerOID, packet);
        }

        private async Task BroadcastGameStateAsync(ActiveGame game, CommandType command)
        {
            await SendGameStateTo(game, game.PlayerXID, command);
            await SendGameStateTo(game, game.PlayerOID, command);
        }

        private async Task SendGameStateTo(ActiveGame game, string playerId, CommandType command)
        {
            if (playerId == ActiveGame.BotId)
                return;

            var client = FindClient(playerId);
            if (client != null)
                await client.SendPacketAsync(PacketHelper.Create(command, game.ToDto(playerId)));
        }

        private async Task SendToPlayerAsync(string playerId, Packet packet)
        {
            if (playerId == ActiveGame.BotId)
                return;

            var client = FindClient(playerId);
            if (client != null)
                await client.SendPacketAsync(packet);
        }

        private async Task SendLobbyAsync(ClientHandler client)
        {
            await client.SendPacketAsync(PacketHelper.Create(CommandType.PLAYER_LIST, _matchmaking.GetPlayers()));
        }

        private async Task BroadcastLobbyAsync()
        {
            var packet = PacketHelper.Create(CommandType.LOBBY_UPDATE, _matchmaking.GetPlayers());
            var clients = GetConnectedClients();
            await Task.WhenAll(clients.Select(c => c.SendPacketAsync(packet)));
        }

        private async Task SendHistoryAsync(ClientHandler client)
        {
            await client.SendPacketAsync(PacketHelper.Create(CommandType.HISTORY_DATA, _historyRepository.GetByPlayer(client.ClientId)));
        }

        private async Task RemoveClientAsync(ClientHandler client)
        {
            ActiveGame? game;
            lock (_clientLock)
                _connectedClients.RemoveAll(c => c.ClientId == client.ClientId);

            game = GetGameByPlayer(client.ClientId);
            _matchmaking.Logout(client.ClientId);

            if (game != null && !game.IsGameOver)
                await EndGameAsync(game, game.OpponentOf(client.ClientId), $"{_matchmaking.GetName(client.ClientId)} mat ket noi");

            await BroadcastLobbyAsync();
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { ClientId = client.ClientId });
        }

        private void AddGame(ActiveGame game)
        {
            lock (_gameLock)
                _games[game.GameID] = game;

            _matchmaking.MarkInGame(game.PlayerXID, game.PlayerXID != ActiveGame.BotId);
            _matchmaking.MarkInGame(game.PlayerOID, game.PlayerOID != ActiveGame.BotId);
        }

        private ClientHandler? FindClient(string id) => GetConnectedClients().FirstOrDefault(c => c.ClientId == id);

        private ActiveGame? GetGame(string gameId)
        {
            lock (_gameLock)
                return _games.TryGetValue(gameId, out var game) ? game : null;
        }

        private ActiveGame? GetGameByPlayer(string playerId)
        {
            lock (_gameLock)
                return _games.Values.FirstOrDefault(g => !g.IsGameOver && (g.PlayerXID == playerId || g.PlayerOID == playerId));
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            _tcpListener?.Stop();

            lock (_gameLock)
            {
                foreach (var game in _games.Values)
                    game.StopTimer();
            }

            foreach (var client in GetConnectedClients())
                await client.DisconnectAsync();

            LogInfo("Server stopped");
        }

        public List<ClientHandler> GetConnectedClients()
        {
            lock (_clientLock)
                return new List<ClientHandler>(_connectedClients);
        }

        public int GetClientCount()
        {
            lock (_clientLock)
                return _connectedClients.Count;
        }

        public Task BroadcastAsync(string message) => Task.WhenAll(GetConnectedClients().Select(c => c.SendMessageAsync(message)));

        public async Task SendToClientAsync(string clientId, string message)
        {
            var client = FindClient(clientId);
            if (client != null)
                await client.SendMessageAsync(message);
        }

        private static void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}");
            Console.ResetColor();
        }

        private static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}");
            Console.ResetColor();
        }

        public bool IsRunning => _isRunning;
        public int ServerPort => _serverPort;
    }

    public class ClientConnectedEventArgs : EventArgs
    {
        public string? ClientId { get; set; }
        public string? RemoteAddress { get; set; }
        public int RemotePort { get; set; }
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        public string? ClientId { get; set; }
    }
}
