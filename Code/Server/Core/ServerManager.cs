using System.Net;
using System.Net.Sockets;
using Shared.DTO;
using Shared.Enums;
using Shared.Models;

namespace Server.Core
{
    public class ServerManager
    {
        private readonly int _serverPort;
        private readonly object _lock = new object();
        private readonly List<ClientHandler> _connectedClients = new List<ClientHandler>();
        private readonly Dictionary<string, UserDTO> _users = new Dictionary<string, UserDTO>();
        private readonly Dictionary<string, InviteDto> _pendingInvites = new Dictionary<string, InviteDto>();
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
            username = string.IsNullOrWhiteSpace(username) ? $"Player {client.ClientId}" : username.Trim();
            lock (_lock)
            {
                _users[client.ClientId] = new UserDTO
                {
                    UserID = client.ClientId,
                    UserName = username,
                    IsOnline = true,
                    IsInGame = false
                };
            }

            await client.SendPacketAsync(PacketHelper.Create(CommandType.SUCCESS, "Logged in", client.ClientId));
            await BroadcastLobbyAsync();
        }

        private async Task SendInviteAsync(ClientHandler client, string data)
        {
            var invite = Serializer.Deserialize<InviteDto>(data);
            if (invite == null)
                return;

            lock (_lock)
            {
                invite.FromPlayerId = client.ClientId;
                invite.FromPlayerName = GetName(client.ClientId);
                invite.ToPlayerName = GetName(invite.ToPlayerId);
                _pendingInvites[$"{invite.FromPlayerId}:{invite.ToPlayerId}"] = invite;
            }

            var target = FindClient(invite.ToPlayerId);
            if (target != null)
                await target.SendPacketAsync(PacketHelper.Create(CommandType.INVITE, invite, client.ClientId));
        }

        private async Task HandleInviteResponseAsync(ClientHandler client, string data)
        {
            var response = Serializer.Deserialize<InviteResponseDto>(data);
            if (response == null)
                return;

            InviteDto? invite = null;
            lock (_lock)
            {
                _pendingInvites.TryGetValue($"{response.FromPlayerId}:{client.ClientId}", out invite);
                _pendingInvites.Remove($"{response.FromPlayerId}:{client.ClientId}");
            }

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
            var game = ActiveGame.CreateOnline(xId, GetName(xId), oId, GetName(oId), invite.TurnSeconds);
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
            var game = ActiveGame.CreateBot(xId, xId == client.ClientId ? GetName(client.ClientId) : "Bot", oId, oId == client.ClientId ? GetName(client.ClientId) : "Bot", request.TurnSeconds, request.Difficulty);
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
                await EndGameAsync(game, client.ClientId, $"{GetName(client.ClientId)} thang do du 5 quan lien tiep");
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
                await EndGameAsync(game, winner, $"{GetName(loser)} het thoi gian va bi xu thua");
                return;
            }

            await BroadcastGameStateAsync(game, CommandType.TIMER_UPDATE);
        }

        private async Task EndByResignAsync(ClientHandler client)
        {
            var game = GetGameByPlayer(client.ClientId);
            if (game != null)
                await EndGameAsync(game, game.OpponentOf(client.ClientId), $"{GetName(client.ClientId)} dau hang");
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
                MarkInGame(game.PlayerXID, false);
                MarkInGame(game.PlayerOID, false);
                SaveHistory(game);
            }

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
            await client.SendPacketAsync(PacketHelper.Create(CommandType.PLAYER_LIST, GetUsers()));
        }

        private async Task BroadcastLobbyAsync()
        {
            var packet = PacketHelper.Create(CommandType.LOBBY_UPDATE, GetUsers());
            var clients = GetConnectedClients();
            await Task.WhenAll(clients.Select(c => c.SendPacketAsync(packet)));
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
                _users.Remove(client.ClientId);
                game = GetGameByPlayer(client.ClientId);
            }

            if (game != null && !game.IsGameOver)
                await EndGameAsync(game, game.OpponentOf(client.ClientId), $"{GetName(client.ClientId)} mat ket noi");

            await BroadcastLobbyAsync();
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { ClientId = client.ClientId });
        }

        private void AddGame(ActiveGame game)
        {
            lock (_lock)
            {
                _games[game.GameID] = game;
                MarkInGame(game.PlayerXID, game.PlayerXID != ActiveGame.BotId);
                MarkInGame(game.PlayerOID, game.PlayerOID != ActiveGame.BotId);
            }
        }

        private void SaveHistory(ActiveGame game)
        {
            if (game.PlayerXID != ActiveGame.BotId)
                _history.Add(game.ToHistory(game.PlayerXID, GetName(game.PlayerOID)));
            if (game.PlayerOID != ActiveGame.BotId)
                _history.Add(game.ToHistory(game.PlayerOID, GetName(game.PlayerXID)));
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

        private void MarkInGame(string playerId, bool isInGame)
        {
            if (_users.TryGetValue(playerId, out var user))
                user.IsInGame = isInGame;
        }

        private List<UserDTO> GetUsers()
        {
            lock (_lock)
                return _users.Values.Select(u => new UserDTO { UserID = u.UserID, UserName = u.UserName, IsOnline = u.IsOnline, IsInGame = u.IsInGame }).ToList();
        }

        private string GetName(string playerId)
        {
            if (playerId == ActiveGame.BotId)
                return "Bot";
            lock (_lock)
                return _users.TryGetValue(playerId, out var user) ? user.UserName : playerId;
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
        public int[][] Board { get; } = Enumerable.Range(0, 15).Select(_ => new int[15]).ToArray();
        public int TimeRemaining { get; set; }
        public int TurnSeconds { get; private set; }
        public bool IsGameOver { get; set; }
        public string WinnerID { get; set; } = "";
        public string ResultText { get; set; } = "";
        public bool IsBotGame { get; private set; }
        public string BotDifficulty { get; private set; } = "Easy";
        public List<MoveRecordDto> Moves { get; } = new List<MoveRecordDto>();
        private Timer? _timer;
        private readonly Random _random = new Random();

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
            if (row < 0 || row >= 15 || col < 0 || col >= 15 || Board[row][col] != 0)
                return false;

            Board[row][col] = symbol == "X" ? 1 : 2;
            Moves.Add(new MoveRecordDto { Row = row, Col = col, PlayerID = playerId, Symbol = symbol });
            TimeRemaining = TurnSeconds;
            return true;
        }

        public void SwitchTurn()
        {
            CurrentTurnID = CurrentTurnID == PlayerXID ? PlayerOID : PlayerXID;
            CurrentSymbol = CurrentTurnID == PlayerXID ? "X" : "O";
            TimeRemaining = TurnSeconds;
        }

        public bool HasWinner(int row, int col, string symbol)
        {
            var value = symbol == "X" ? 1 : 2;
            var directions = new[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var (dr, dc) in directions)
            {
                var count = 1 + Count(row, col, dr, dc, value) + Count(row, col, -dr, -dc, value);
                if (count >= 5)
                    return true;
            }
            return false;
        }

        private int Count(int row, int col, int dr, int dc, int value)
        {
            var count = 0;
            var r = row + dr;
            var c = col + dc;
            while (r >= 0 && r < 15 && c >= 0 && c < 15 && Board[r][c] == value)
            {
                count++;
                r += dr;
                c += dc;
            }
            return count;
        }

        public bool IsBoardFull() => Board.All(row => row.All(cell => cell != 0));

        public (int row, int col) ChooseBotMove()
        {
            var botSymbol = SymbolOf(BotId);
            var humanSymbol = botSymbol == "X" ? "O" : "X";
            if (Moves.Count == 0)
                return (7, 7);

            if (BotDifficulty != "Easy")
            {
                var win = FindFinishingMove(botSymbol);
                if (win.row >= 0)
                    return win;
            }

            if (BotDifficulty != "Easy")
            {
                var block = FindFinishingMove(humanSymbol);
                if (block.row >= 0)
                    return block;
            }

            if (BotDifficulty == "Hard")
                return ChooseHardMove(botSymbol, humanSymbol);

            if (BotDifficulty == "Medium")
                return ChooseBestHeuristicMove(botSymbol, humanSymbol);

            var candidates = EmptyCellsNearMoves().ToList();
            if (candidates.Count == 0)
                candidates.Add((7, 7));
            return candidates[_random.Next(candidates.Count)];
        }

        private (int row, int col) FindFinishingMove(string symbol)
        {
            foreach (var (row, col) in CandidateMoves(2, 32))
            {
                Board[row][col] = symbol == "X" ? 1 : 2;
                var ok = HasWinner(row, col, symbol);
                Board[row][col] = 0;
                if (ok)
                    return (row, col);
            }
            return (-1, -1);
        }

        private IEnumerable<(int row, int col)> EmptyCellsNearMoves()
        {
            if (Moves.Count == 0)
            {
                yield return (7, 7);
                yield break;
            }

            var seen = new HashSet<string>();
            foreach (var move in Moves)
            {
                for (var dr = -1; dr <= 1; dr++)
                    for (var dc = -1; dc <= 1; dc++)
                    {
                        var r = move.Row + dr;
                        var c = move.Col + dc;
                        var key = $"{r}:{c}";
                        if (r >= 0 && r < 15 && c >= 0 && c < 15 && Board[r][c] == 0 && seen.Add(key))
                            yield return (r, c);
                    }
            }
        }

        private (int row, int col) ChooseBestHeuristicMove(string botSymbol, string humanSymbol)
        {
            return CandidateMoves(2, 18)
                .OrderByDescending(move => ScoreCandidate(move.row, move.col, botSymbol, humanSymbol))
                .FirstOrDefault((-1, -1));
        }

        private (int row, int col) ChooseHardMove(string botSymbol, string humanSymbol)
        {
            var candidates = CandidateMoves(2, 14).ToList();
            if (candidates.Count == 0)
                return (7, 7);

            var botValue = SymbolValue(botSymbol);
            var bestMove = candidates[0];
            var bestScore = int.MinValue;

            foreach (var move in candidates)
            {
                Board[move.row][move.col] = botValue;
                var score = HasWinner(move.row, move.col, botSymbol)
                    ? 50_000_000
                    : Minimax(3, false, botSymbol, humanSymbol, int.MinValue / 2, int.MaxValue / 2);
                Board[move.row][move.col] = 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            return bestMove;
        }

        private int Minimax(int depth, bool maximizing, string botSymbol, string humanSymbol, int alpha, int beta)
        {
            if (depth == 0 || IsBoardFull())
                return EvaluateBoard(botSymbol, humanSymbol);

            var symbol = maximizing ? botSymbol : humanSymbol;
            var value = SymbolValue(symbol);
            var moves = CandidateMoves(2, depth >= 3 ? 12 : 10).ToList();
            if (moves.Count == 0)
                return EvaluateBoard(botSymbol, humanSymbol);

            if (maximizing)
            {
                var best = int.MinValue / 2;
                foreach (var move in moves)
                {
                    Board[move.row][move.col] = value;
                    var score = HasWinner(move.row, move.col, symbol)
                        ? 40_000_000 + depth
                        : Minimax(depth - 1, false, botSymbol, humanSymbol, alpha, beta);
                    Board[move.row][move.col] = 0;

                    best = Math.Max(best, score);
                    alpha = Math.Max(alpha, best);
                    if (beta <= alpha)
                        break;
                }
                return best;
            }

            var worst = int.MaxValue / 2;
            foreach (var move in moves)
            {
                Board[move.row][move.col] = value;
                var score = HasWinner(move.row, move.col, symbol)
                    ? -40_000_000 - depth
                    : Minimax(depth - 1, true, botSymbol, humanSymbol, alpha, beta);
                Board[move.row][move.col] = 0;

                worst = Math.Min(worst, score);
                beta = Math.Min(beta, worst);
                if (beta <= alpha)
                    break;
            }
            return worst;
        }

        private IEnumerable<(int row, int col)> CandidateMoves(int radius, int limit)
        {
            if (Moves.Count == 0)
                return new[] { (7, 7) };

            var seen = new HashSet<string>();
            var moves = new List<(int row, int col)>();
            foreach (var move in Moves)
            {
                for (var dr = -radius; dr <= radius; dr++)
                    for (var dc = -radius; dc <= radius; dc++)
                    {
                        var r = move.Row + dr;
                        var c = move.Col + dc;
                        var key = $"{r}:{c}";
                        if (r >= 0 && r < 15 && c >= 0 && c < 15 && Board[r][c] == 0 && seen.Add(key))
                            moves.Add((r, c));
                    }
            }

            return moves
                .OrderByDescending(move => ScoreCandidate(move.row, move.col, SymbolOf(BotId), SymbolOf(BotId) == "X" ? "O" : "X"))
                .ThenBy(move => Math.Abs(move.row - 7) + Math.Abs(move.col - 7))
                .Take(limit)
                .ToList();
        }

        private int ScoreCandidate(int row, int col, string botSymbol, string humanSymbol)
        {
            var botValue = SymbolValue(botSymbol);
            var humanValue = SymbolValue(humanSymbol);

            Board[row][col] = botValue;
            var attack = EvaluatePoint(row, col, botValue);
            Board[row][col] = humanValue;
            var defense = EvaluatePoint(row, col, humanValue);
            Board[row][col] = 0;

            return attack + defense * 2;
        }

        private int EvaluateBoard(string botSymbol, string humanSymbol)
        {
            return EvaluatePlayer(SymbolValue(botSymbol)) - EvaluatePlayer(SymbolValue(humanSymbol)) * 2;
        }

        private int EvaluatePlayer(int value)
        {
            var score = 0;
            for (var r = 0; r < 15; r++)
                for (var c = 0; c < 15; c++)
                    if (Board[r][c] == value)
                        score += EvaluatePoint(r, c, value);
            return score;
        }

        private int EvaluatePoint(int row, int col, int value)
        {
            var score = 0;
            var directions = new[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var (dr, dc) in directions)
            {
                var forward = CountLine(row, col, dr, dc, value, out var openForward);
                var backward = CountLine(row, col, -dr, -dc, value, out var openBackward);
                var length = 1 + forward + backward;
                var openEnds = (openForward ? 1 : 0) + (openBackward ? 1 : 0);
                score += ScorePattern(length, openEnds);
            }
            return score;
        }

        private int CountLine(int row, int col, int dr, int dc, int value, out bool openEnd)
        {
            var count = 0;
            var r = row + dr;
            var c = col + dc;
            while (r >= 0 && r < 15 && c >= 0 && c < 15 && Board[r][c] == value)
            {
                count++;
                r += dr;
                c += dc;
            }

            openEnd = r >= 0 && r < 15 && c >= 0 && c < 15 && Board[r][c] == 0;
            return count;
        }

        private static int ScorePattern(int length, int openEnds)
        {
            if (length >= 5)
                return 10_000_000;
            if (length == 4 && openEnds == 2)
                return 1_000_000;
            if (length == 4 && openEnds == 1)
                return 120_000;
            if (length == 3 && openEnds == 2)
                return 45_000;
            if (length == 3 && openEnds == 1)
                return 5_000;
            if (length == 2 && openEnds == 2)
                return 1_200;
            if (length == 2 && openEnds == 1)
                return 150;
            if (length == 1 && openEnds == 2)
                return 20;
            return 1;
        }

        private static int SymbolValue(string symbol) => symbol == "X" ? 1 : 2;

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
                Board = Board.Select(row => row.ToArray()).ToArray(),
                TimeRemaining = TimeRemaining,
                TurnSeconds = TurnSeconds,
                IsGameOver = IsGameOver,
                WinnerID = WinnerID,
                ResultText = ResultText,
                Moves = Moves.ToList(),
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
                Moves = Moves.ToList()
            };
        }
    }
}
