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
    public class ServerManager
    {
        private TcpListener _tcpListener;
        private List<ClientHandler> _connectedClients;
        private bool _isRunning;
        private int _serverPort;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _clientLock = new object();

        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        public ServerManager(int port = 5000)
        {
            _serverPort = port;
            _connectedClients = new List<ClientHandler>();
            _isRunning = false;
            _cancellationTokenSource = new CancellationTokenSource();
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
                await AcceptClientsAsync();
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
            try
            {
                string clientId = Guid.NewGuid().ToString().Substring(0, 8);
                var clientHandler = new ClientHandler(tcpClient, clientId);

                lock (_clientLock)
                {
                    _connectedClients.Add(clientHandler);
                }

                IPEndPoint remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
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
                string disconnectedClientId = null;
                lock (_clientLock)
                {
                    var clientHandler = _connectedClients.FirstOrDefault(c => c.ClientId == (disconnectedClientId ?? c.ClientId));
                    if (clientHandler != null)
                    {
                        disconnectedClientId = clientHandler.ClientId;
                        _connectedClients.Remove(clientHandler);
                    }
                }

                if (!string.IsNullOrEmpty(disconnectedClientId))
                {
                    LogInfo($"[{disconnectedClientId}] Client disconnected");
                    ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs { ClientId = disconnectedClientId });
                }
            }
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

                // Close all client connections
                lock (_clientLock)
                {
                    foreach (var client in _connectedClients)
                    {
                        await client.DisconnectAsync();
                    }
                    _connectedClients.Clear();
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
        public string ClientId { get; set; }
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        public string ClientId { get; set; }
    }
}
