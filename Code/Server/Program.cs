using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Shared.DTO;
using Shared.Enums;
using Shared.Models;
using PacketModel = Shared.Models.Packet;

namespace Server
{
    internal class Program
    {
        private const int Port = 5000;
        private const int BoardSize = 15;
        private static readonly List<ClientSession> Clients = new List<ClientSession>();
        private static readonly Dictionary<string, GameSession> Games = new Dictionary<string, GameSession>();
        private static readonly List<GameHistoryDTO> History = new List<GameHistoryDTO>();
        private static readonly object SyncRoot = new object();

        private static async Task Main()
        {
            TcpListener server = new TcpListener(IPAddress.Any, Port);
            server.Start();

            Console.WriteLine("Server dang chay tai port " + Port);

            while (true)
            {
                TcpClient tcpClient = await server.AcceptTcpClientAsync();
                ClientSession session = new ClientSession(tcpClient);

                lock (SyncRoot)
                    Clients.Add(session);

                Console.WriteLine("Client da ket noi.");
                _ = Task.Run(() => HandleClientAsync(session));
            }
        }

        private static async Task HandleClientAsync(ClientSession session)
        {
            try
            {
                while (true)
                {
                    string? json = await session.Reader.ReadLineAsync();

                    if (json == null)
                        break;

                    PacketModel? packet = Serializer.Deserialize(json);

                    if (packet == null)
                        continue;

                    await HandlePacketAsync(session, packet);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Client mat ket noi.");
            }
            catch (SocketException)
            {
                Console.WriteLine("Client mat ket noi.");
            }
            finally
            {
                lock (SyncRoot)
                {
                    Clients.Remove(session);
                    RemoveGamesOf(session);
                }

                session.Dispose();
                await BroadcastPlayerListAsync();
            }
        }

        private static async Task HandlePacketAsync(ClientSession session, PacketModel packet)
        {
            Console.WriteLine("Command: " + packet.Command + " - Sender: " + packet.SenderID);

            switch (packet.Command)
            {
                case CommandType.LOGIN:
                    string? userName = Serializer.DeserializeData<string>(packet.Data);
                    session.UserName = string.IsNullOrWhiteSpace(userName) ? "Player" : userName.Trim();
                    session.UserId = session.UserName;
                    await session.SendAsync(Serializer.Create(CommandType.SUCCESS, "Login success", "server"));
                    await BroadcastPlayerListAsync();
                    break;

                case CommandType.GET_PLAYER_LIST:
                    await session.SendAsync(Serializer.Create(CommandType.PLAYER_LIST, GetOnlinePlayers(), "server"));
                    break;

                case CommandType.INVITE:
                    await ForwardInviteAsync(session, packet);
                    break;

                case CommandType.INVITE_RESPONSE:
                    await HandleInviteResponseAsync(session, packet);
                    break;

                case CommandType.CREATE_ROOM:
                    await session.SendAsync(Serializer.Create(CommandType.ERROR, "Hay chon mot nguoi trong lobby va bam Moi dau de bat dau.", "server"));
                    break;

                case CommandType.MAKE_MOVE:
                    await HandleMoveAsync(session, packet);
                    break;

                case CommandType.GAME_CHAT:
                    await HandleChatAsync(session, packet);
                    break;

                case CommandType.DRAW_REQUEST:
                    await HandleDrawRequestAsync(session, packet);
                    break;

                case CommandType.DRAW_RESPONSE:
                    await HandleDrawResponseAsync(session, packet);
                    break;

                case CommandType.GET_HISTORY:
                    await session.SendAsync(Serializer.Create(CommandType.HISTORY_DATA, GetHistoryFor(session.UserId), "server"));
                    break;

                case CommandType.LEAVE_ROOM:
                    await HandleTemporaryLeaveAsync(session, packet);
                    break;

                case CommandType.JOIN_ROOM:
                    await HandleReturnGameAsync(session, packet);
                    break;

                case CommandType.SURRENDER:
                    await HandleSurrenderAsync(session, packet);
                    break;

                case CommandType.START_BOT_GAME:
                    await HandleStartBotGameAsync(session, packet);
                    break;

                case CommandType.PING:
                    await session.SendAsync(Serializer.Create(CommandType.SUCCESS, packet.Data, "server"));
                    break;
            }
        }

        private static async Task ForwardInviteAsync(ClientSession inviterSession, PacketModel packet)
        {
            if (inviterSession.IsInGame)
            {
                await inviterSession.SendAsync(Serializer.Create(CommandType.ERROR, "Ban dang co van dau chua ket thuc.", "server"));
                return;
            }

            InviteDTO? invite = Serializer.DeserializeData<InviteDTO>(packet.Data);

            if (invite == null)
                return;

            ClientSession? targetSession = FindClient(invite.Target.UserID, invite.Target.UserName);

            if (targetSession == null)
            {
                await inviterSession.SendAsync(Serializer.Create(CommandType.ERROR, "Nguoi choi khong con online.", "server"));
                await BroadcastPlayerListAsync();
                return;
            }

            if (targetSession.IsInGame)
            {
                await inviterSession.SendAsync(Serializer.Create(CommandType.ERROR, "Nguoi choi dang trong van dau khac.", "server"));
                await BroadcastPlayerListAsync();
                return;
            }

            invite.Inviter = inviterSession.ToUserDTO();
            invite.Target = targetSession.ToUserDTO();
            invite.TurnSeconds = NormalizeTurnSeconds(invite.TurnSeconds);
            await targetSession.SendAsync(Serializer.Create(CommandType.INVITE, invite, inviterSession.UserId));
        }

        private static async Task HandleInviteResponseAsync(ClientSession responderSession, PacketModel packet)
        {
            if (responderSession.IsInGame)
            {
                await responderSession.SendAsync(Serializer.Create(CommandType.ERROR, "Ban dang co van dau chua ket thuc.", "server"));
                return;
            }

            Shared.DTO.InviteResponseDTO? response = Serializer.DeserializeData<Shared.DTO.InviteResponseDTO>(packet.Data);

            if (response == null)
                return;

            ClientSession? inviterSession = FindClient(response.Invite.Inviter.UserID, response.Invite.Inviter.UserName);

            if (inviterSession == null)
                return;

            if (response.Accepted)
            {
                ClientSession playerX = response.Invite.InviterPlaysX ? inviterSession : responderSession;
                ClientSession playerO = response.Invite.InviterPlaysX ? responderSession : inviterSession;
                await StartGameAsync(playerX, playerO, NormalizeTurnSeconds(response.Invite.TurnSeconds));
            }
            else
            {
                await inviterSession.SendAsync(Serializer.Create(
                    CommandType.ERROR,
                    responderSession.UserName + " da tu choi loi moi.",
                    "server"));
            }
        }

        private static async Task StartGameAsync(ClientSession player1, ClientSession player2, int turnSeconds)
        {
            GameSession gameSession = new GameSession(player1, player2, turnSeconds);

            lock (SyncRoot)
            {
                Games[gameSession.GameId] = gameSession;
                player1.IsInGame = true;
                player2.IsInGame = true;
            }

            GameDTO game = gameSession.ToGameDTO();
            PacketModel packet = Serializer.Create(CommandType.GAME_START, game, "server");
            await player1.SendAsync(packet);
            await player2.SendAsync(packet);
            StartTurnTimer(gameSession);
            await BroadcastPlayerListAsync();
        }

        private static async Task HandleStartBotGameAsync(ClientSession player, PacketModel packet)
        {
            if (player.IsInGame)
            {
                await player.SendAsync(Serializer.Create(CommandType.ERROR, "Ban dang co van dau chua ket thuc.", "server"));
                return;
            }

            BotGameRequestDTO? request = Serializer.DeserializeData<BotGameRequestDTO>(packet.Data);
            string difficulty = request?.Difficulty ?? "Easy";
            ClientSession bot = ClientSession.CreateBot("BOT-" + difficulty.ToUpperInvariant(), difficulty);
            GameSession gameSession = new GameSession(player, bot, 30);

            lock (SyncRoot)
            {
                Games[gameSession.GameId] = gameSession;
                player.IsInGame = true;
            }

            await player.SendAsync(Serializer.Create(CommandType.GAME_START, gameSession.ToGameDTO(), "server"));
            StartTurnTimer(gameSession);
            await BroadcastPlayerListAsync();
        }

        private static async Task HandleMoveAsync(ClientSession session, PacketModel packet)
        {
            MoveDTO? move = Serializer.DeserializeData<MoveDTO>(packet.Data);

            if (move == null)
                return;

            GameSession? game;
            lock (SyncRoot)
                Games.TryGetValue(move.GameID, out game);

            if (game == null)
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Khong tim thay van dau.", "server"));
                return;
            }

            string? error = game.TryApplyMove(session, move);

            if (error != null)
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, error, "server"));
                return;
            }

            PacketModel movePacket = Serializer.Create(CommandType.GAME_MOVE, move, "server");
            await game.Player1.SendAsync(movePacket);
            await game.Player2.SendAsync(movePacket);

            if (game.WinnerId != null)
            {
                game.IsGameOver = true;
                string message = game.WinnerId + " thang!";
                PacketModel endPacket = Serializer.Create(CommandType.GAME_END, message, "server");
                await game.Player1.SendAsync(endPacket);
                if (!game.Player2.IsBot)
                    await game.Player2.SendAsync(endPacket);
                FinishGame(game, message, game.WinnerId);
                await BroadcastPlayerListAsync();
            }
            else if (game.IsDraw())
            {
                game.IsGameOver = true;
                string message = "Hoa co!";
                PacketModel endPacket = Serializer.Create(CommandType.GAME_END, message, "server");
                await game.Player1.SendAsync(endPacket);
                if (!game.Player2.IsBot)
                    await game.Player2.SendAsync(endPacket);
                FinishGame(game, message, string.Empty);
                await BroadcastPlayerListAsync();
            }
            else if (game.Player2.IsBot && game.CurrentTurnId == game.Player2.UserId)
            {
                await MakeBotMoveAsync(game);
            }
            else
            {
                StartTurnTimer(game);
            }
        }

        private static async Task MakeBotMoveAsync(GameSession game)
        {
            MoveDTO botMove = GomokuBot.ChooseMove(game.Board, game.Player2.BotDifficulty, 2, 1);
            string? error = game.TryApplyMove(game.Player2, botMove);

            if (error != null)
                return;

            PacketModel movePacket = Serializer.Create(CommandType.GAME_MOVE, botMove, "server");
            await game.Player1.SendAsync(movePacket);

            if (game.WinnerId != null)
            {
                game.IsGameOver = true;
                string message = game.WinnerId + " thang!";
                await game.Player1.SendAsync(Serializer.Create(CommandType.GAME_END, message, "server"));
                FinishGame(game, message, game.WinnerId);
                await BroadcastPlayerListAsync();
            }
            else if (game.IsDraw())
            {
                game.IsGameOver = true;
                string message = "Hoa co!";
                await game.Player1.SendAsync(Serializer.Create(CommandType.GAME_END, message, "server"));
                FinishGame(game, message, string.Empty);
                await BroadcastPlayerListAsync();
            }
            else
            {
                StartTurnTimer(game);
            }
        }

        private static async Task HandleChatAsync(ClientSession session, PacketModel packet)
        {
            ChatMessageDTO? chat = Serializer.DeserializeData<ChatMessageDTO>(packet.Data);

            if (chat == null || string.IsNullOrWhiteSpace(chat.Message))
                return;

            GameSession? game = FindGame(chat.GameID);

            if (game == null)
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Khong tim thay phong chat cua van dau.", "server"));
                return;
            }

            if (!game.Contains(session))
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Ban khong o trong van dau nay.", "server"));
                return;
            }

            chat.GameID = game.GameId;
            chat.SenderID = session.UserId;
            chat.SenderName = session.UserName;
            chat.SentAt = DateTime.Now;

            PacketModel chatPacket = Serializer.Create(CommandType.GAME_CHAT, chat, session.UserId);
            await game.Player1.SendAsync(chatPacket);
            if (!game.Player2.IsBot)
                await game.Player2.SendAsync(chatPacket);
        }

        private static async Task HandleDrawRequestAsync(ClientSession session, PacketModel packet)
        {
            DrawOfferDTO? offer = Serializer.DeserializeData<DrawOfferDTO>(packet.Data);

            if (offer == null)
                return;

            GameSession? game = FindGame(offer.GameID);

            if (game == null || !game.Contains(session))
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Khong tim thay van dau de cau hoa.", "server"));
                return;
            }

            if (game.Player2.IsBot)
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Bot khong chap nhan cau hoa dau, danh tiep di.", "server"));
                return;
            }

            ClientSession opponent = game.GetOpponent(session);
            offer.GameID = game.GameId;
            offer.FromPlayerID = session.UserId;
            offer.ToPlayerID = opponent.UserId;
            offer.Accepted = false;

            await opponent.SendAsync(Serializer.Create(CommandType.DRAW_REQUEST, offer, session.UserId));
            await session.SendAsync(Serializer.Create(CommandType.SUCCESS, "Da gui loi cau hoa den " + opponent.UserName + ".", "server"));
        }

        private static async Task HandleDrawResponseAsync(ClientSession session, PacketModel packet)
        {
            DrawOfferDTO? response = Serializer.DeserializeData<DrawOfferDTO>(packet.Data);

            if (response == null)
                return;

            GameSession? game = FindGame(response.GameID);

            if (game == null || !game.Contains(session))
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Khong tim thay van dau de phan hoi cau hoa.", "server"));
                return;
            }

            ClientSession requester = game.GetOpponent(session);

            if (!response.Accepted)
            {
                await requester.SendAsync(Serializer.Create(CommandType.ERROR, session.UserName + " da tu choi cau hoa.", "server"));
                return;
            }

            game.IsGameOver = true;
            string message = "Hai nguoi choi dong y hoa!";
            PacketModel endPacket = Serializer.Create(CommandType.GAME_END, message, "server");
            await game.Player1.SendAsync(endPacket);
            if (!game.Player2.IsBot)
                await game.Player2.SendAsync(endPacket);
            FinishGame(game, message, string.Empty);
            await BroadcastPlayerListAsync();
        }

        private static async Task HandleSurrenderAsync(ClientSession session, PacketModel packet)
        {
            string? gameId = Serializer.DeserializeData<string>(packet.Data);
            GameSession? game = FindGame(gameId ?? string.Empty);

            if (game == null || !game.Contains(session))
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Khong tim thay van dau de dau hang.", "server"));
                return;
            }

            ClientSession winner = game.GetOpponent(session);
            game.IsGameOver = true;
            game.WinnerId = winner.UserId;
            string message = session.UserId + " dau hang. " + winner.UserId + " thang!";
            PacketModel endPacket = Serializer.Create(CommandType.GAME_END, message, "server");

            await game.Player1.SendAsync(endPacket);
            if (!game.Player2.IsBot)
                await game.Player2.SendAsync(endPacket);

            FinishGame(game, message, winner.UserId);
            await BroadcastPlayerListAsync();
        }

        private static async Task HandleTemporaryLeaveAsync(ClientSession session, PacketModel packet)
        {
            string? gameId = Serializer.DeserializeData<string>(packet.Data);
            GameSession? game = FindGame(gameId ?? string.Empty);

            if (game == null || !game.Contains(session))
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Khong tim thay van dau de roi tam.", "server"));
                return;
            }

            if (game.Player2.IsBot)
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Dang dau voi bot thi hay dau hang de ket thuc van.", "server"));
                return;
            }

            lock (SyncRoot)
            {
                game.MarkAway(session);
            }

            await session.SendAsync(Serializer.Create(CommandType.SUCCESS, "Ban da ve lobby tam thoi. Dong ho luot van tiep tuc chay.", "server"));
        }

        private static async Task HandleReturnGameAsync(ClientSession session, PacketModel packet)
        {
            string? gameId = Serializer.DeserializeData<string>(packet.Data);
            GameSession? game = FindGame(gameId ?? string.Empty);

            if (game == null || !game.Contains(session))
            {
                await session.SendAsync(Serializer.Create(CommandType.ERROR, "Van dau khong con ton tai.", "server"));
                return;
            }

            lock (SyncRoot)
                game.MarkReturned(session);

            await session.SendAsync(Serializer.Create(CommandType.GAME_START, game.ToGameDTO(), "server"));
        }

        private static GameSession? FindGame(string gameId)
        {
            lock (SyncRoot)
            {
                Games.TryGetValue(gameId, out GameSession? game);
                return game;
            }
        }

        private static void FinishGame(GameSession game, string result, string winnerId)
        {
            lock (SyncRoot)
            {
                History.Add(new GameHistoryDTO
                {
                    GameID = game.GameId,
                    Player1ID = game.Player1.UserId,
                    Player2ID = game.Player2.UserId,
                    WinnerID = winnerId,
                    Result = result,
                    MoveLog = new List<string>(game.MoveLog),
                    EndedAt = DateTime.Now
                });

                game.Player1.IsInGame = false;
                game.Player2.IsInGame = false;
                Games.Remove(game.GameId);
            }
        }

        private static void StartTurnTimer(GameSession game)
        {
            string gameId = game.GameId;
            string turnPlayerId = game.CurrentTurnId;
            int turnVersion = game.StartNewTurnTimer();

            _ = Task.Run(async () =>
            {
                for (int remaining = game.TurnSeconds; remaining >= 0; remaining--)
                {
                    GameSession? currentGame = FindGame(gameId);

                    if (currentGame == null || currentGame.IsGameOver || !currentGame.IsSameTurn(turnPlayerId, turnVersion))
                        return;

                    await SendTimerUpdateAsync(currentGame, remaining);

                    if (remaining == 0)
                        break;

                    await Task.Delay(1000);
                }

                await ResolveTurnTimeoutAsync(gameId, turnPlayerId, turnVersion);
            });
        }

        private static async Task SendTimerUpdateAsync(GameSession game, int remainingSeconds)
        {
            TimerUpdateDTO update = new TimerUpdateDTO
            {
                GameID = game.GameId,
                CurrentTurnID = game.CurrentTurnId,
                RemainingSeconds = remainingSeconds
            };

            PacketModel packet = Serializer.Create(CommandType.TIMER_UPDATE, update, "server");
            await game.Player1.SendAsync(packet);
            if (!game.Player2.IsBot)
                await game.Player2.SendAsync(packet);
        }

        private static int NormalizeTurnSeconds(int seconds)
        {
            return seconds switch
            {
                15 => 15,
                30 => 30,
                60 => 60,
                120 => 120,
                300 => 300,
                _ => 30
            };
        }

        private static string FormatTurnSeconds(int seconds)
        {
            if (seconds < 60)
                return seconds + " giay";

            return (seconds / 60) + " phut";
        }

        private static async Task ResolveTurnTimeoutAsync(string gameId, string turnPlayerId, int turnVersion)
        {
            GameSession? game = FindGame(gameId);

            if (game == null || game.IsGameOver || !game.IsSameTurn(turnPlayerId, turnVersion))
                return;

            ClientSession timeoutPlayer = game.Player1.UserId == turnPlayerId ? game.Player1 : game.Player2;
            ClientSession winner = game.GetOpponent(timeoutPlayer);
            game.IsGameOver = true;
            game.WinnerId = winner.UserId;

            string message = timeoutPlayer.UserId + " het " + FormatTurnSeconds(game.TurnSeconds) + " suy nghi. " + winner.UserId + " thang!";
            PacketModel endPacket = Serializer.Create(CommandType.GAME_END, message, "server");
            await game.Player1.SendAsync(endPacket);
            if (!game.Player2.IsBot)
                await game.Player2.SendAsync(endPacket);

            FinishGame(game, message, winner.UserId);
            await BroadcastPlayerListAsync();
        }

        private static void RemoveGamesOf(ClientSession session)
        {
            List<string> removeIds = new List<string>();

            foreach (GameSession game in Games.Values)
            {
                if (game.Player1 == session || game.Player2 == session)
                {
                    game.Player1.IsInGame = false;
                    game.Player2.IsInGame = false;
                    removeIds.Add(game.GameId);
                }
            }

            foreach (string gameId in removeIds)
                Games.Remove(gameId);
        }

        private static async Task BroadcastPlayerListAsync()
        {
            await BroadcastAsync(Serializer.Create(CommandType.PLAYER_LIST, GetOnlinePlayers(), "server"));
        }

        private static List<UserDTO> GetOnlinePlayers()
        {
            lock (SyncRoot)
            {
                List<UserDTO> players = new List<UserDTO>();

                foreach (ClientSession client in Clients)
                {
                    if (!string.IsNullOrWhiteSpace(client.UserName))
                        players.Add(client.ToUserDTO());
                }

                return players;
            }
        }

        private static List<GameHistoryDTO> GetHistoryFor(string userId)
        {
            lock (SyncRoot)
            {
                List<GameHistoryDTO> result = new List<GameHistoryDTO>();

                foreach (GameHistoryDTO item in History)
                {
                    if (item.Player1ID == userId || item.Player2ID == userId)
                        result.Add(item);
                }

                result.Reverse();
                return result;
            }
        }

        private static ClientSession? FindClient(string? userId, string? userName)
        {
            lock (SyncRoot)
            {
                foreach (ClientSession client in Clients)
                {
                    if (!string.IsNullOrWhiteSpace(userId) && client.UserId == userId)
                        return client;

                    if (!string.IsNullOrWhiteSpace(userName) && client.UserName == userName)
                        return client;
                }
            }

            return null;
        }

        private static async Task BroadcastAsync(PacketModel packet)
        {
            List<ClientSession> snapshot;

            lock (SyncRoot)
                snapshot = new List<ClientSession>(Clients);

            foreach (ClientSession client in snapshot)
                await client.SendAsync(packet);
        }

        private sealed class GameSession
        {
            public GameSession(ClientSession player1, ClientSession player2, int turnSeconds)
            {
                Player1 = player1;
                Player2 = player2;
                TurnSeconds = turnSeconds;
                GameId = Guid.NewGuid().ToString("N");
                CurrentTurnId = player1.UserId;
            }

            public string GameId { get; }
            public ClientSession Player1 { get; }
            public ClientSession Player2 { get; }
            public int TurnSeconds { get; }
            public int[,] Board { get; } = new int[BoardSize, BoardSize];
            public List<string> MoveLog { get; } = new List<string>();
            public string CurrentTurnId { get; private set; }
            public string? WinnerId { get; set; }
            public bool IsGameOver { get; set; }
            private readonly HashSet<string> awayPlayers = new HashSet<string>();
            private int turnVersion;

            public bool Contains(ClientSession session)
            {
                return Player1 == session || Player2 == session;
            }

            public ClientSession GetOpponent(ClientSession session)
            {
                return Player1 == session ? Player2 : Player1;
            }

            public void MarkAway(ClientSession session)
            {
                awayPlayers.Add(session.UserId);
            }

            public void MarkReturned(ClientSession session)
            {
                awayPlayers.Remove(session.UserId);
            }

            public bool IsAway(string userId)
            {
                return awayPlayers.Contains(userId);
            }

            public bool AreBothAway()
            {
                return awayPlayers.Contains(Player1.UserId) && awayPlayers.Contains(Player2.UserId);
            }

            public int StartNewTurnTimer()
            {
                turnVersion++;
                return turnVersion;
            }

            public bool IsSameTurn(string userId, int version)
            {
                return CurrentTurnId == userId && turnVersion == version;
            }

            public string? TryApplyMove(ClientSession session, MoveDTO move)
            {
                if (IsGameOver)
                    return "Van dau da ket thuc.";

                if (session.UserId != Player1.UserId && session.UserId != Player2.UserId)
                    return "Ban khong phai nguoi choi trong van nay.";

                if (session.UserId != CurrentTurnId)
                    return "Chua toi luot cua ban.";

                if (move.Row < 0 || move.Row >= BoardSize || move.Col < 0 || move.Col >= BoardSize)
                    return "Nuoc di nam ngoai ban co.";

                if (Board[move.Row, move.Col] != 0)
                    return "O nay da co quan.";

                int mark = session.UserId == Player1.UserId ? 1 : 2;
                Board[move.Row, move.Col] = mark;
                move.PlayerID = session.UserId;
                move.GameID = GameId;
                MoveLog.Add((MoveLog.Count + 1).ToString("00") + ". " +
                            session.UserId + " " +
                            (mark == 1 ? "X" : "O") +
                            ": (" + (move.Row + 1) + ", " + (move.Col + 1) + ")");

                if (HasFiveInRow(move.Row, move.Col, mark))
                    WinnerId = session.UserId;
                else
                    CurrentTurnId = session.UserId == Player1.UserId ? Player2.UserId : Player1.UserId;

                return null;
            }

            public bool IsDraw()
            {
                for (int row = 0; row < BoardSize; row++)
                {
                    for (int col = 0; col < BoardSize; col++)
                    {
                        if (Board[row, col] == 0)
                            return false;
                    }
                }

                return true;
            }

            public GameDTO ToGameDTO()
            {
                int[][] dtoBoard = new int[BoardSize][];

                for (int row = 0; row < BoardSize; row++)
                {
                    dtoBoard[row] = new int[BoardSize];
                    for (int col = 0; col < BoardSize; col++)
                        dtoBoard[row][col] = Board[row, col];
                }

                return new GameDTO
                {
                    GameID = GameId,
                    Player1ID = Player1.UserId,
                    Player2ID = Player2.UserId,
                    CurrentTurnID = CurrentTurnId,
                    Board = dtoBoard,
                    IsGameOver = IsGameOver,
                    WinnerID = WinnerId ?? string.Empty,
                    TimeRemaining = TurnSeconds
                };
            }

            private bool HasFiveInRow(int row, int col, int mark)
            {
                return CountLine(row, col, 0, 1, mark) >= 5 ||
                       CountLine(row, col, 1, 0, mark) >= 5 ||
                       CountLine(row, col, 1, 1, mark) >= 5 ||
                       CountLine(row, col, 1, -1, mark) >= 5;
            }

            private int CountLine(int row, int col, int dRow, int dCol, int mark)
            {
                return 1 +
                       CountDirection(row, col, dRow, dCol, mark) +
                       CountDirection(row, col, -dRow, -dCol, mark);
            }

            private int CountDirection(int row, int col, int dRow, int dCol, int mark)
            {
                int count = 0;
                int nextRow = row + dRow;
                int nextCol = col + dCol;

                while (nextRow >= 0 &&
                       nextRow < BoardSize &&
                       nextCol >= 0 &&
                       nextCol < BoardSize &&
                       Board[nextRow, nextCol] == mark)
                {
                    count++;
                    nextRow += dRow;
                    nextCol += dCol;
                }

                return count;
            }
        }
    }

    internal sealed class ClientSession : IDisposable
    {
        private readonly TcpClient? tcpClient;

        public ClientSession(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            NetworkStream stream = tcpClient.GetStream();
            Reader = new StreamReader(stream);
            Writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };
        }

        private ClientSession(string botName, string difficulty)
        {
            UserId = botName;
            UserName = botName;
            IsBot = true;
            BotDifficulty = difficulty;
            Reader = StreamReader.Null;
            Writer = StreamWriter.Null;
        }

        public static ClientSession CreateBot(string botName, string difficulty)
        {
            return new ClientSession(botName, difficulty);
        }

        public StreamReader Reader { get; }
        public StreamWriter Writer { get; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool IsInGame { get; set; }
        public bool IsBot { get; private set; }
        public string BotDifficulty { get; private set; } = "Easy";

        public Task SendAsync(PacketModel packet)
        {
            if (IsBot)
                return Task.CompletedTask;

            return Writer.WriteLineAsync(Serializer.Serialize(packet));
        }

        public UserDTO ToUserDTO()
        {
            return new UserDTO
            {
                UserID = UserId,
                UserName = UserName,
                IsOnline = true,
                IsInGame = IsInGame
            };
        }

        public void Dispose()
        {
            Reader.Dispose();
            Writer.Dispose();
            tcpClient?.Close();
        }
    }

    internal static class GomokuBot
    {
        private const int BoardSize = 15;
        private static readonly Random Random = new Random();

        public static MoveDTO ChooseMove(int[,] board, string difficulty, int botMark, int humanMark)
        {
            List<(int Row, int Col)> candidates = GetCandidateMoves(board);

            if (candidates.Count == 0)
                return new MoveDTO { Row = BoardSize / 2, Col = BoardSize / 2 };

            if (difficulty.Equals("Easy", StringComparison.OrdinalIgnoreCase))
            {
                (int row, int col) = candidates[Random.Next(candidates.Count)];
                return new MoveDTO { Row = row, Col = col };
            }

            (int winRow, int winCol, bool hasWin) = FindImmediateMove(board, candidates, botMark);
            if (hasWin)
                return new MoveDTO { Row = winRow, Col = winCol };

            (int blockRow, int blockCol, bool hasBlock) = FindImmediateMove(board, candidates, humanMark);
            if (hasBlock)
                return new MoveDTO { Row = blockRow, Col = blockCol };

            int depth = difficulty.Equals("Hard", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            (int bestRow, int bestCol) = FindBestMove(board, candidates, botMark, humanMark, depth);
            return new MoveDTO { Row = bestRow, Col = bestCol };
        }

        private static (int Row, int Col) FindBestMove(int[,] board, List<(int Row, int Col)> candidates, int botMark, int humanMark, int depth)
        {
            int bestScore = int.MinValue;
            (int Row, int Col) best = candidates[0];

            foreach ((int row, int col) in candidates)
            {
                board[row, col] = botMark;
                int score = Minimax(board, depth - 1, false, botMark, humanMark, int.MinValue + 1, int.MaxValue - 1);
                board[row, col] = 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = (row, col);
                }
            }

            return best;
        }

        private static int Minimax(int[,] board, int depth, bool maximizing, int botMark, int humanMark, int alpha, int beta)
        {
            if (depth == 0)
                return EvaluateBoard(board, botMark) - EvaluateBoard(board, humanMark);

            List<(int Row, int Col)> moves = GetCandidateMoves(board);

            if (moves.Count == 0)
                return 0;

            if (maximizing)
            {
                int best = int.MinValue;
                foreach ((int row, int col) in moves)
                {
                    board[row, col] = botMark;
                    best = Math.Max(best, Minimax(board, depth - 1, false, botMark, humanMark, alpha, beta));
                    board[row, col] = 0;
                    alpha = Math.Max(alpha, best);
                    if (beta <= alpha)
                        break;
                }
                return best;
            }

            int worst = int.MaxValue;
            foreach ((int row, int col) in moves)
            {
                board[row, col] = humanMark;
                worst = Math.Min(worst, Minimax(board, depth - 1, true, botMark, humanMark, alpha, beta));
                board[row, col] = 0;
                beta = Math.Min(beta, worst);
                if (beta <= alpha)
                    break;
            }
            return worst;
        }

        private static (int Row, int Col, bool Found) FindImmediateMove(int[,] board, List<(int Row, int Col)> candidates, int mark)
        {
            foreach ((int row, int col) in candidates)
            {
                board[row, col] = mark;
                bool wins = HasFiveInRow(board, row, col, mark);
                board[row, col] = 0;

                if (wins)
                    return (row, col, true);
            }

            return (0, 0, false);
        }

        private static int EvaluateBoard(int[,] board, int mark)
        {
            int score = 0;

            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if (board[row, col] == mark)
                    {
                        score += EvaluateDirection(board, row, col, 0, 1, mark);
                        score += EvaluateDirection(board, row, col, 1, 0, mark);
                        score += EvaluateDirection(board, row, col, 1, 1, mark);
                        score += EvaluateDirection(board, row, col, 1, -1, mark);
                    }
                }
            }

            return score;
        }

        private static int EvaluateDirection(int[,] board, int row, int col, int dRow, int dCol, int mark)
        {
            int prevRow = row - dRow;
            int prevCol = col - dCol;
            if (IsInside(prevRow, prevCol) && board[prevRow, prevCol] == mark)
                return 0;

            int count = 0;
            int r = row;
            int c = col;

            while (IsInside(r, c) && board[r, c] == mark)
            {
                count++;
                r += dRow;
                c += dCol;
            }

            int openEnds = 0;
            if (IsInside(prevRow, prevCol) && board[prevRow, prevCol] == 0)
                openEnds++;
            if (IsInside(r, c) && board[r, c] == 0)
                openEnds++;

            if (count >= 5)
                return 1_000_000;
            if (count == 4 && openEnds == 2)
                return 100_000;
            if (count == 4)
                return 12_000;
            if (count == 3 && openEnds == 2)
                return 3_000;
            if (count == 3)
                return 500;
            if (count == 2 && openEnds == 2)
                return 120;
            return count * 10;
        }

        private static List<(int Row, int Col)> GetCandidateMoves(int[,] board)
        {
            List<(int Row, int Col)> moves = new List<(int Row, int Col)>();
            bool hasStone = false;

            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if (board[row, col] != 0)
                    {
                        hasStone = true;
                        break;
                    }
                }
            }

            if (!hasStone)
            {
                moves.Add((BoardSize / 2, BoardSize / 2));
                return moves;
            }

            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if (board[row, col] == 0 && HasNeighbor(board, row, col))
                        moves.Add((row, col));
                }
            }

            return moves;
        }

        private static bool HasNeighbor(int[,] board, int row, int col)
        {
            for (int dRow = -2; dRow <= 2; dRow++)
            {
                for (int dCol = -2; dCol <= 2; dCol++)
                {
                    if (dRow == 0 && dCol == 0)
                        continue;

                    int r = row + dRow;
                    int c = col + dCol;
                    if (IsInside(r, c) && board[r, c] != 0)
                        return true;
                }
            }

            return false;
        }

        private static bool HasFiveInRow(int[,] board, int row, int col, int mark)
        {
            return CountLine(board, row, col, 0, 1, mark) >= 5 ||
                   CountLine(board, row, col, 1, 0, mark) >= 5 ||
                   CountLine(board, row, col, 1, 1, mark) >= 5 ||
                   CountLine(board, row, col, 1, -1, mark) >= 5;
        }

        private static int CountLine(int[,] board, int row, int col, int dRow, int dCol, int mark)
        {
            return 1 +
                   CountDirection(board, row, col, dRow, dCol, mark) +
                   CountDirection(board, row, col, -dRow, -dCol, mark);
        }

        private static int CountDirection(int[,] board, int row, int col, int dRow, int dCol, int mark)
        {
            int count = 0;
            int nextRow = row + dRow;
            int nextCol = col + dCol;

            while (IsInside(nextRow, nextCol) && board[nextRow, nextCol] == mark)
            {
                count++;
                nextRow += dRow;
                nextCol += dCol;
            }

            return count;
        }

        private static bool IsInside(int row, int col)
        {
            return row >= 0 && row < BoardSize && col >= 0 && col < BoardSize;
        }
    }

}
