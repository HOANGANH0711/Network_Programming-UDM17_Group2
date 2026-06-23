using System.Net;
using System.Net.Sockets;
using Shared.DTO;
using Shared.Enums;
using Shared.Models;
using Server.GameLogic;
using Server.Handlers;

namespace Server.Core
{
    public class ServerManager
    {
        private readonly int _serverPort;
        private readonly object _lock = new object();
        private readonly List<ClientHandler> _connectedClients = new List<ClientHandler>();
        private readonly MatchmakingService _matchmaking;
        private readonly Dictionary<string, ActiveGame> _games = new Dictionary<string, ActiveGame>();
        private readonly List<HistoryItemDto> _history = new List<HistoryItemDto>();
        private readonly string _historyPath = Path.Combine(AppContext.BaseDirectory, "history_items.json");
        private TcpListener? _tcpListener;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isRunning;

        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

        public ServerManager(int port = 5000)
        {
            _serverPort = port;
            _matchmaking = new MatchmakingService(FindClient, GetConnectedClients, StartOnlineGameAsync);
            LoadHistory();
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
            lock (_lock)
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
                        await _matchmaking.LoginAsync(client, packet.Data);
                        break;
                    case CommandType.GET_PLAYER_LIST:
                    case CommandType.RETURN_TO_LOBBY:
                        await _matchmaking.SendLobbyAsync(client);
                        break;
                    case CommandType.INVITE:
                    case CommandType.CHALLENGE:
                        await _matchmaking.SendInviteAsync(client, packet.Data);
                        break;
                    case CommandType.INVITE_RESPONSE:
                        await _matchmaking.HandleInviteResponseAsync(client, packet.Data);
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

        private async Task StartOnlineGameAsync(InviteDto invite)
        {
            var xId = invite.InviterSymbol == "X" ? invite.FromPlayerId : invite.ToPlayerId;
            var oId = invite.InviterSymbol == "X" ? invite.ToPlayerId : invite.FromPlayerId;
            var game = ActiveGame.CreateOnline(xId, _matchmaking.GetName(xId), oId, _matchmaking.GetName(oId), invite.TurnSeconds);
            AddGame(game);
            await BroadcastGameStateAsync(game, CommandType.GAME_START);
            game.StartTimer(() => _ = TickGameAsync(game.GameID));
            await _matchmaking.BroadcastLobbyAsync();
        }

        private async Task StartBotGameAsync(ClientHandler client, string data)
        {
            var request = Serializer.Deserialize<BotGameRequestDto>(data) ?? new BotGameRequestDto();
            var playerIsX = request.PlayerSymbol != "O";
            var xId = playerIsX ? client.ClientId : ActiveGame.BotId;
            var oId = playerIsX ? ActiveGame.BotId : client.ClientId;
            var game = ActiveGame.CreateBot(xId, xId == client.ClientId ? _matchmaking.GetName(client.ClientId) : "Bot", oId, oId == client.ClientId ? _matchmaking.GetName(client.ClientId) : "Bot", request.TurnSeconds, request.Difficulty);
            AddGame(game);
            await BroadcastGameStateAsync(game, CommandType.GAME_START);
            game.StartTimer(() => _ = TickGameAsync(game.GameID));
            await MaybeBotMoveAsync(game);
            await _matchmaking.BroadcastLobbyAsync();
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

            lock (_lock)
            {
                _matchmaking.MarkInGame(game.PlayerXID, false);
                _matchmaking.MarkInGame(game.PlayerOID, false);
                SaveHistory(game);
            }

            await BroadcastGameStateAsync(game, CommandType.GAME_END);
            await _matchmaking.BroadcastLobbyAsync();
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

        private async Task SendHistoryAsync(ClientHandler client)
        {
            List<HistoryItemDto> items;
            lock (_lock)
                items = _history.Where(h => h.GameID.StartsWith(client.ClientId + "-", StringComparison.Ordinal)).ToList();
            await client.SendPacketAsync(PacketHelper.Create(CommandType.HISTORY_DATA, items));
        }

        private async Task RemoveClientAsync(ClientHandler client)
        {
            ActiveGame? game;
            lock (_lock)
            {
                _connectedClients.RemoveAll(c => c.ClientId == client.ClientId);
                _matchmaking.RemovePlayer(client.ClientId);
                game = GetGameByPlayer(client.ClientId);
            }

            if (game != null && !game.IsGameOver)
                await EndGameAsync(game, game.OpponentOf(client.ClientId), $"{_matchmaking.GetName(client.ClientId)} mat ket noi");

            await _matchmaking.BroadcastLobbyAsync();
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { ClientId = client.ClientId });
        }

        private void AddGame(ActiveGame game)
        {
            lock (_lock)
            {
                _games[game.GameID] = game;
                _matchmaking.MarkInGame(game.PlayerXID, game.PlayerXID != ActiveGame.BotId);
                _matchmaking.MarkInGame(game.PlayerOID, game.PlayerOID != ActiveGame.BotId);
            }
        }

        private void SaveHistory(ActiveGame game)
        {
            if (game.PlayerXID != ActiveGame.BotId)
                _history.Add(game.ToHistory(game.PlayerXID, _matchmaking.GetName(game.PlayerOID)));
            if (game.PlayerOID != ActiveGame.BotId)
                _history.Add(game.ToHistory(game.PlayerOID, _matchmaking.GetName(game.PlayerXID)));
            File.WriteAllText(_historyPath, Serializer.Serialize(_history));
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var items = Serializer.Deserialize<List<HistoryItemDto>>(File.ReadAllText(_historyPath));
                    if (items != null)
                        _history.AddRange(items);
                }
            }
            catch (Exception ex)
            {
                LogError($"Could not load history: {ex.Message}");
            }
        }

        private ClientHandler? FindClient(string id) => GetConnectedClients().FirstOrDefault(c => c.ClientId == id);

        private ActiveGame? GetGame(string gameId)
        {
            lock (_lock)
                return _games.TryGetValue(gameId, out var game) ? game : null;
        }

        private ActiveGame? GetGameByPlayer(string playerId)
        {
            lock (_lock)
                return _games.Values.FirstOrDefault(g => !g.IsGameOver && (g.PlayerXID == playerId || g.PlayerOID == playerId));
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            _tcpListener?.Stop();
            foreach (var game in _games.Values)
                game.StopTimer();
            foreach (var client in GetConnectedClients())
                await client.DisconnectAsync();
            LogInfo("Server stopped");
        }

        public List<ClientHandler> GetConnectedClients()
        {
            lock (_lock)
                return new List<ClientHandler>(_connectedClients);
        }

        public int GetClientCount()
        {
            lock (_lock)
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

    internal sealed class ActiveGame
    {
        public const string BotId = "BOT";
        public string GameID { get; } = Guid.NewGuid().ToString("N");
        public string PlayerXID { get; private set; } = "";
        public string PlayerXName { get; private set; } = "";
        public string PlayerOID { get; private set; } = "";
        public string PlayerOName { get; private set; } = "";
        public string CurrentTurnID { get; private set; } = "";
        public string CurrentSymbol { get; private set; } = "X";
        public GameLogic.Board Board { get; } = new GameLogic.Board();
        public int TimeRemaining { get; set; }
        public int TurnSeconds { get; private set; }
        public bool IsGameOver { get; set; }
        public string WinnerID { get; set; } = "";
        public string ResultText { get; set; } = "";
        public bool IsBotGame { get; private set; }
        public string BotDifficulty { get; private set; } = "Easy";
        public List<GameLogic.Move> Moves { get; } = new List<GameLogic.Move>();
        private Timer? _timer;
        private readonly GameLogic.GameService _gameService = new GameLogic.GameService();

        public static ActiveGame CreateOnline(string xId, string xName, string oId, string oName, int turnSeconds)
        {
            return new ActiveGame
            {
                PlayerXID = xId,
                PlayerXName = xName,
                PlayerOID = oId,
                PlayerOName = oName,
                CurrentTurnID = xId,
                TurnSeconds = Math.Max(15, turnSeconds),
                TimeRemaining = Math.Max(15, turnSeconds)
            };
        }

        public static ActiveGame CreateBot(string xId, string xName, string oId, string oName, int turnSeconds, string difficulty)
        {
            var game = CreateOnline(xId, xName, oId, oName, turnSeconds);
            game.IsBotGame = true;
            game.BotDifficulty = difficulty;
            return game;
        }

        public void StartTimer(Action tick)
        {
            _timer = new Timer(_ => tick(), null, 1000, 1000);
        }

        public void StopTimer()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public string SymbolOf(string playerId)
        {
            if (playerId == PlayerXID)
                return "X";
            if (playerId == PlayerOID)
                return "O";
            return "";
        }

        public string OpponentOf(string playerId) => playerId == PlayerXID ? PlayerOID : PlayerXID;

        public bool PlaceMove(string playerId, string symbol, int row, int col)
        {
            var value = GameLogic.CellValueExtensions.FromSymbol(symbol);
            if (!Board.Place(row, col, value))
                return false;

            Moves.Add(new GameLogic.Move { Row = row, Col = col, PlayerID = playerId, Symbol = symbol });
            TimeRemaining = TurnSeconds;
            return true;
        }

        public void SwitchTurn()
        {
            CurrentTurnID = CurrentTurnID == PlayerXID ? PlayerOID : PlayerXID;
            CurrentSymbol = CurrentTurnID == PlayerXID ? "X" : "O";
            TimeRemaining = TurnSeconds;
        }

        public bool HasWinner(int row, int col, string symbol) =>
        GameLogic.WinChecker.HasWinner(Board, row, col, symbol);

        public bool IsBoardFull() => Board.IsFull();


        public (int row, int col) ChooseBotMove()
        {
            var botSymbol = SymbolOf(BotId);
            return _gameService.ChooseBotMove(Board, Moves, botSymbol, BotDifficulty);
        }


        public GameStateDto ToDto(string viewerId)
        {
            return new GameStateDto
            {
                GameID = GameID,
                PlayerXID = PlayerXID,
                PlayerXName = PlayerXName,
                PlayerOID = PlayerOID,
                PlayerOName = PlayerOName,
                CurrentTurnID = CurrentTurnID,
                CurrentSymbol = CurrentSymbol,
                YourSymbol = SymbolOf(viewerId),
                Board = Board.GetSnapshot(),
                TimeRemaining = TimeRemaining,
                TurnSeconds = TurnSeconds,
                IsGameOver = IsGameOver,
                WinnerID = WinnerID,
                ResultText = ResultText,
                Moves = Moves.Select(m => new MoveRecordDto
                {
                    Row = m.Row,
                    Col = m.Col,
                    PlayerID = m.PlayerID,
                    Symbol = m.Symbol
                }).ToList(),
                IsBotGame = IsBotGame
            };
        }

        public HistoryItemDto ToHistory(string playerId, string opponentName)
        {
            var result = WinnerID == "" ? "Hoa" : WinnerID == playerId ? "Thang" : "Thua";
            return new HistoryItemDto
            {
                GameID = $"{playerId}-{GameID}",
                PlayedAt = DateTime.Now,
                OpponentName = opponentName,
                Result = result,
                Mode = IsBotGame ? $"Bot {BotDifficulty}" : "Online",
                Moves = Moves.Select(m => new MoveRecordDto
                {
                    Row = m.Row,
                    Col = m.Col,
                    PlayerID = m.PlayerID,
                    Symbol = m.Symbol
                }).ToList(),
            };
        }
    }
}