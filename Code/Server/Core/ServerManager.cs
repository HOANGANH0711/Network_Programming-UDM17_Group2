using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Core
{
    using Shared.Models;
    using Shared.Enums;
    using Shared.DTO;
    using Server.Rooms;
    using Server.Handlers;

    public class ServerManager
    {
        private TcpListener? _tcpListener;
        private List<ClientHandler> _connectedClients;
        private bool _isRunning;
        private int _serverPort;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _clientLock = new object();
        private readonly GameRoomManager _roomManager = new GameRoomManager();
        private readonly MatchmakingService _matchmaking = new MatchmakingService();
        private readonly Dictionary<string, string> _usernames = new Dictionary<string, string>();

        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

        public ServerManager(int port = 5000)
        {
            _serverPort = port;
            _connectedClients = new List<ClientHandler>();
            _isRunning = false;
            _cancellationTokenSource = new CancellationTokenSource();

            _matchmaking.BroadcastToLobby += async payload =>
            {
                var pkt = PacketHelper.Create(CommandType.PLAYER_LIST, payload);
                await BroadcastPacketAsync(pkt);
            };

            _matchmaking.SendToClient += async (playerId, payload) =>
            {
                var pkt = PacketHelper.Create(CommandType.INVITE, payload);
                await SendToClientAsync(playerId, System.Text.Json.JsonSerializer.Serialize(pkt));
            };

            _matchmaking.CreateGameRoom += (p1Id, p2Id) =>
            {
                string p1Name = _usernames.TryGetValue(p1Id, out var n1) ? n1 : p1Id;
                var room = _roomManager.CreateRoom($"{p1Name}'s Room", p1Id);
                _roomManager.TryJoinRoom(room.RoomID, p2Id);
                return room.RoomID;
            };
        }

        public async Task StartAsync()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, _serverPort);
                _tcpListener.Start();
                _isRunning = true;

                LogInfo($"Server started on port {_serverPort}");
                LogInfo("Waiting for client connections...");

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
                    if (_tcpListener == null) break;

                    TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
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
            string clientId = Guid.NewGuid().ToString().Substring(0, 8);
            try
            {
                var clientHandler = new ClientHandler(tcpClient, clientId);

                lock (_clientLock)
                {
                    _connectedClients.Add(clientHandler);
                }

                clientHandler.MessageReceived += async (s, e) =>
                {
                    try
                    {
                        if (e?.Packet != null)
                        {
                            await HandlePacketAsync(clientHandler, e.Packet);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error handling packet from {clientId}: {ex.Message}");
                    }
                };

                clientHandler.Disconnected += (s, e) =>
                {
                    try
                    {
                        lock (_clientLock)
                        {
                            if (_usernames.ContainsKey(clientHandler.ClientId))
                                _usernames.Remove(clientHandler.ClientId);
                        }

                        _roomManager.PlayerLeft(clientHandler.ClientId);
                        _matchmaking.PlayerLeaveLobby(clientHandler.ClientId);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error during disconnect cleanup for {clientId}: {ex.Message}");
                    }
                };

                IPEndPoint? remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                LogInfo($"[{clientId}] Client connected from {remoteEndPoint?.Address}:{remoteEndPoint?.Port}");

                ClientConnected?.Invoke(this, new ClientConnectedEventArgs
                {
                    ClientId = clientId,
                    RemoteAddress = remoteEndPoint?.Address.ToString(),
                    RemotePort = remoteEndPoint?.Port ?? 0
                });

                await clientHandler.HandleAsync();
            }
            catch (Exception ex)
            {
                LogError($"Error handling client connection: {ex.Message}");
            }
            finally
            {
                lock (_clientLock)
                {
                    var clientHandler = _connectedClients.FirstOrDefault(c => c.ClientId == clientId);
                    if (clientHandler != null)
                    {
                        _connectedClients.Remove(clientHandler);
                    }
                }

                LogInfo($"[{clientId}] Client disconnected");
                ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { ClientId = clientId });
            }
        }

        private async Task HandlePacketAsync(ClientHandler client, Packet packet)
        {
            switch (packet.Command)
            {
                case CommandType.LOGIN:
                    {
                        var username = packet.Data ?? string.Empty;
                        lock (_clientLock)
                        {
                            _usernames[client.ClientId] = username;
                        }
                        _matchmaking.PlayerJoinLobby(client.ClientId, username);

                        var ack = PacketHelper.Create(CommandType.SUCCESS, "Logged in", client.ClientId);
                        await client.SendPacketAsync(ack);
                        break;
                    }

                case CommandType.INVITE:
                    {
                        _matchmaking.SendInvite(client.ClientId, packet.Data ?? "");
                        break;
                    }

                case CommandType.INVITE_RESPONSE:
                    {
                        _matchmaking.RespondToInvite(client.ClientId, packet.Data == "true");
                        break;
                    }

                case CommandType.CREATE_ROOM:
                    {
                        var roomName = packet.Data ?? "Room";
                        var room = _roomManager.CreateRoom(roomName, client.ClientId);

                        var resp = PacketHelper.Create(CommandType.ROOM_CREATED, room.Info, client.ClientId);
                        await client.SendPacketAsync(resp);

                        var list = _roomManager.GetRoomList().ToList();
                        var listPkt = PacketHelper.Create(CommandType.ROOM_LIST, list);
                        await BroadcastPacketAsync(listPkt);
                        break;
                    }

                case CommandType.GET_ROOM_LIST:
                    {
                        var list = _roomManager.GetRoomList().ToList();
                        var listPkt = PacketHelper.Create(CommandType.ROOM_LIST, list);
                        await client.SendPacketAsync(listPkt);
                        break;
                    }

                case CommandType.JOIN_ROOM:
                    {
                        var roomId = packet.Data ?? string.Empty;
                        bool ok = _roomManager.TryJoinRoom(roomId, client.ClientId);
                        if (ok)
                        {
                            var room = _roomManager.GetRoomList().FirstOrDefault(r => r.RoomID == roomId);
                            var resp = PacketHelper.Create(CommandType.ROOM_JOINED, room, client.ClientId);
                            await client.SendPacketAsync(resp);

                            if (room != null)
                            {
                                var otherId = room.Player1ID == client.ClientId ? room.Player2ID : room.Player1ID;
                                if (!string.IsNullOrEmpty(otherId))
                                {
                                    var otherClient = GetConnectedClients().FirstOrDefault(c => c.ClientId == otherId);
                                    if (otherClient != null)
                                    {
                                        var notify = PacketHelper.Create(CommandType.PLAYER_JOINED, room, client.ClientId);
                                        await otherClient.SendPacketAsync(notify);
                                    }
                                }
                            }
                            var list = _roomManager.GetRoomList().ToList();
                            var listPkt = PacketHelper.Create(CommandType.ROOM_LIST, list);
                            await BroadcastPacketAsync(listPkt);
                        }
                        else
                        {
                            var err = PacketHelper.Create(CommandType.ROOM_FULL, "Room is full", client.ClientId);
                            await client.SendPacketAsync(err);
                        }
                        break;
                    }

                case CommandType.LEAVE_ROOM:
                    {
                        _roomManager.PlayerLeft(client.ClientId);
                        var ok = PacketHelper.Create(CommandType.SUCCESS, "Left room", client.ClientId);
                        await client.SendPacketAsync(ok);

                        var list = _roomManager.GetRoomList().ToList();
                        var listPkt = PacketHelper.Create(CommandType.ROOM_LIST, list);
                        await BroadcastPacketAsync(listPkt);
                        break;
                    }

                case CommandType.PING:
                    {
                        var pong = PacketHelper.Create(CommandType.SUCCESS, "PONG", client.ClientId);
                        await client.SendPacketAsync(pong);
                        break;
                    }

                default:
                    {
                        LogInfo($"Received command {packet.Command} from {client.ClientId}");
                        break;
                    }
            }
        }

        private async Task BroadcastPacketAsync(Packet packet)
        {
            var clients = GetConnectedClients();
            var tasks = new List<Task>();
            foreach (var c in clients)
            {
                tasks.Add(c.SendPacketAsync(packet));
            }
            await Task.WhenAll(tasks);
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();

            LogInfo("Stopping server...");

            try
            {
                _tcpListener?.Stop();

                List<ClientHandler> clientsToDisconnect;
                lock (_clientLock)
                {
                    clientsToDisconnect = new List<ClientHandler>(_connectedClients);
                    _connectedClients.Clear();
                }

                foreach (var client in clientsToDisconnect)
                {
                    await client.DisconnectAsync();
                }

                LogInfo("Server stopped");
            }
            catch (Exception ex)
            {
                LogError($"Error stopping server: {ex.Message}");
            }
        }

        public List<ClientHandler> GetConnectedClients()
        {
            lock (_clientLock)
            {
                return new List<ClientHandler>(_connectedClients);
            }
        }

        public int GetClientCount()
        {
            lock (_clientLock)
            {
                return _connectedClients.Count;
            }
        }

        public async Task BroadcastAsync(string message)
        {
            try
            {
                var clients = GetConnectedClients();
                var tasks = new List<Task>();

                foreach (var client in clients)
                {
                    tasks.Add(client.SendMessageAsync(message));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                LogError($"Error broadcasting message: {ex.Message}");
            }
        }

        public async Task SendToClientAsync(string clientId, string message)
        {
            try
            {
                var client = GetConnectedClients().FirstOrDefault(c => c.ClientId == clientId);
                if (client != null)
                {
                    await client.SendMessageAsync(message);
                }
                else
                {
                    LogWarning($"Client {clientId} not found");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error sending message to client: {ex.Message}");
            }
        }

        private void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}");
            Console.ResetColor();
        }

        private void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}");
            Console.ResetColor();
        }

        private void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARN] {message}");
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