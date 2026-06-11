using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Core
{
    /// <summary>
    /// ServerManager: Manages TCP server operations and client connections
    /// - Accepts incoming client connections
    /// - Maintains list of connected clients
    /// - Logs connection/disconnection events
    /// </summary>
    using Shared.Models;
    using Shared.Enums;
    using Shared.DTO;
    using Server.Rooms;

    public class ServerManager
    {
        private TcpListener? _tcpListener;
        private List<ClientHandler> _connectedClients;
        private bool _isRunning;
        private int _serverPort;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _clientLock = new object();
        private readonly GameRoomManager _roomManager = new GameRoomManager();
        private readonly Dictionary<string, string> _usernames = new Dictionary<string, string>();
        private readonly TimeSpan _clientTimeout = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _watchdogInterval = TimeSpan.FromSeconds(15);
        private Task? _watchdogTask;
        private CancellationTokenSource? _watchdogCts;

        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

        public ServerManager(int port = 5000)
        {
            _serverPort = port;
            _connectedClients = new List<ClientHandler>();
            _isRunning = false;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private ClientHandler? GetClientById(string clientId)
        {
            lock (_clientLock)
            {
                return _connectedClients.FirstOrDefault(c => c.ClientId == clientId);
            }
        }

        private async Task WatchdogLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(_watchdogInterval, cancellationToken);

                    var now = DateTime.UtcNow;
                    var timedOut = new List<ClientHandler>();

                    lock (_clientLock)
                    {
                        foreach (var c in _connectedClients)
                        {
                            try
                            {
                                if (!c.IsConnected) continue;
                                var last = c.LastActivity;
                                if (now - last > _clientTimeout)
                                {
                                    timedOut.Add(c);
                                }
                            }
                            catch
                            {
                                // ignore per-client errors
                            }
                        }
                    }

                    foreach (var c in timedOut)
                    {
                        LogWarning($"Client {c.ClientId} timed out, disconnecting.");
                        try
                        {
                            await c.DisconnectAsync();
                        }
                        catch (Exception ex)
                        {
                            LogError($"Error disconnecting timed out client {c.ClientId}: {ex.Message}");
                        }

                        // cleanup username mapping and rooms
                        lock (_clientLock)
                        {
                            var mapping = _usernames.FirstOrDefault(kv => kv.Key == c.ClientId);
                            if (!string.IsNullOrEmpty(mapping.Key) && _usernames.ContainsKey(mapping.Key))
                                _usernames.Remove(mapping.Key);
                        }
                        _roomManager.PlayerLeft(c.ClientId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            catch (Exception ex)
            {
                LogError($"Watchdog error: {ex.Message}");
            }
        }

        /// <summary>
        /// Start the TCP server and begin accepting client connections
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, _serverPort);
                _tcpListener.Start();
                _isRunning = true;

                LogInfo($"Server started on port {_serverPort}");
                LogInfo("Waiting for client connections...");

                // Accept client connections in a background task
                // start accepting clients
                var acceptTask = AcceptClientsAsync();

                // start watchdog loop
                _watchdogCts = new CancellationTokenSource();
                _watchdogTask = Task.Run(() => WatchdogLoopAsync(_watchdogCts.Token));

                await acceptTask;
            }
            catch (Exception ex)
            {
                LogError($"Error starting server: {ex.Message}");
                _isRunning = false;
            }
        }

        /// <summary>
        /// Accept incoming client connections
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_tcpListener == null) break;
                    
                    TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    
                    // Handle client connection in a separate task
                    _ = HandleClientConnectionAsync(tcpClient);
                }
                catch (ObjectDisposedException)
                {
                    // Server has been stopped
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"Error accepting client connection: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handle a new client connection
        /// </summary>
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

                // Subscribe to incoming messages from this client
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

                // When client disconnects, cleanup rooms and username map
                clientHandler.Disconnected += (s, e) =>
                {
                    try
                    {
                        lock (_clientLock)
                        {
                            if (_usernames.ContainsKey(clientHandler.ClientId))
                                _usernames.Remove(clientHandler.ClientId);
                        }

                        // Remove player from any rooms
                        _roomManager.PlayerLeft(clientHandler.ClientId);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error during disconnect cleanup for {clientId}: {ex.Message}");
                    }
                };

                IPEndPoint? remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                LogInfo($"[{clientId}] Client connected from {remoteEndPoint?.Address}:{remoteEndPoint?.Port}");

                // Raise ClientConnected event
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs 
                { 
                    ClientId = clientId,
                    RemoteAddress = remoteEndPoint?.Address.ToString(),
                    RemotePort = remoteEndPoint?.Port ?? 0
                });

                // Keep the connection alive and monitor for disconnection
                await clientHandler.HandleAsync();
            }
            catch (Exception ex)
            {
                LogError($"Error handling client connection: {ex.Message}");
            }
            finally
            {
                // Remove client from list when disconnected
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
                    // packet.Data expected to be username
                    var username = packet.Data ?? string.Empty;
                    lock (_clientLock)
                    {
                        _usernames[client.ClientId] = username;
                    }

                    // Acknowledge
                    var ack = PacketHelper.Create(CommandType.SUCCESS, "Logged in", client.ClientId);
                    await client.SendPacketAsync(ack);
                    break;
                }

                case CommandType.CREATE_ROOM:
                {
                    var roomName = packet.Data ?? "Room";
                    var room = _roomManager.CreateRoom(roomName, client.ClientId);

                    var resp = PacketHelper.Create(CommandType.ROOM_CREATED, room.Info, client.ClientId);
                    await client.SendPacketAsync(resp);

                    // Broadcast updated room list
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
                    // packet.Data expected to be roomId
                    var roomId = packet.Data ?? string.Empty;
                    bool ok = _roomManager.TryJoinRoom(roomId, client.ClientId);
                    if (ok)
                    {
                        var room = _roomManager.GetRoomList().FirstOrDefault(r => r.RoomID == roomId);
                        var resp = PacketHelper.Create(CommandType.ROOM_JOINED, room, client.ClientId);
                        await client.SendPacketAsync(resp);

                        // Notify other players in room
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
                        // Broadcast updated room list
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

                case CommandType.GAME_END:
                {
                    // client signals game end; remove from room and cleanup
                    _roomManager.PlayerLeft(client.ClientId);
                    var list = _roomManager.GetRoomList().ToList();
                    var listPkt = PacketHelper.Create(CommandType.ROOM_LIST, list);
                    await BroadcastPacketAsync(listPkt);
                    break;
                }

                case CommandType.OPPONENT_DISCONNECTED:
                {
                    // opponent disconnected unexpectedly: remove player from room and notify
                    _roomManager.PlayerLeft(client.ClientId);
                    var notify = PacketHelper.Create(CommandType.OPPONENT_DISCONNECTED, client.ClientId, client.ClientId);
                    await client.SendPacketAsync(notify);

                    var list = _roomManager.GetRoomList().ToList();
                    var listPkt = PacketHelper.Create(CommandType.ROOM_LIST, list);
                    await BroadcastPacketAsync(listPkt);
                    break;
                }

                // Other commands (game-related) can be forwarded or handled here
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

        /// <summary>
        /// Stop the server and close all client connections
        /// </summary>
        public async Task StopAsync()
        {
            _isRunning = false;
            _cancellationTokenSource.Cancel();

            LogInfo("Stopping server...");

            try
            {
                _tcpListener?.Stop();

                // stop watchdog
                try
                {
                    _watchdogCts?.Cancel();
                    if (_watchdogTask != null)
                        await _watchdogTask;
                }
                catch { }

                // Close all client connections
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

        /// <summary>
        /// Get the list of connected clients
        /// </summary>
        public List<ClientHandler> GetConnectedClients()
        {
            lock (_clientLock)
            {
                return new List<ClientHandler>(_connectedClients);
            }
        }

        /// <summary>
        /// Get the count of connected clients
        /// </summary>
        public int GetClientCount()
        {
            lock (_clientLock)
            {
                return _connectedClients.Count;
            }
        }

        /// <summary>
        /// Broadcast message to all connected clients
        /// </summary>
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

        /// <summary>
        /// Send message to a specific client
        /// </summary>
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

        // Logging methods
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

    // Event args classes
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
