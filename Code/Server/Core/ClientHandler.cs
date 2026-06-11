using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shared.Models;

namespace Server.Core
{
    /// <summary>
    /// ClientHandler: Manages individual client connection
    /// - Handles send/receive of messages
    /// - Maintains connection state
    /// - Handles disconnection
    /// </summary>
    public class ClientHandler
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly object _sendLock = new object();
        private string _clientId;
        private DateTime _lastActivity;
        private bool _isConnected;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler? Disconnected;

        public ClientHandler(TcpClient tcpClient, string clientId)
        {
            _tcpClient = tcpClient;
            _clientId = clientId;
            _networkStream = tcpClient.GetStream();
            // Use StreamReader/StreamWriter with newline-delimited JSON (client uses WriteLine)
            _reader = new StreamReader(_networkStream, Encoding.UTF8);
            _writer = new StreamWriter(_networkStream, Encoding.UTF8) { AutoFlush = true };
            _isConnected = true;
            _lastActivity = DateTime.UtcNow;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Handle client communication (receive messages)
        /// </summary>
        public async Task HandleAsync()
        {
            try
            {
                // Read newline-delimited JSON packets from client
                while (_isConnected && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    string? line = null;
                    try
                    {
                        if (_reader == null) break;
                        line = await _reader.ReadLineAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        // Network error => treat as disconnect
                        _isConnected = false;
                        break;
                    }

                    if (line == null)
                    {
                        // Stream closed
                        _isConnected = false;
                        break;
                    }

                    // Try to parse packet JSON
                    try
                    {
                        var packet = PacketHelper.Deserialize(line);
                        // update last activity on any incoming data
                        _lastActivity = DateTime.UtcNow;
                        if (packet != null)
                        {
                            MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Packet = packet });
                        }
                        else
                        {
                            // If deserialization failed but we still received text, forward raw text
                            MessageReceived?.Invoke(this, new MessageReceivedEventArgs { RawMessage = line });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{_clientId}] Error parsing packet: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] Error in HandleAsync: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
                Disconnected?.Invoke(this, EventArgs.Empty);
                await DisconnectAsync();
            }
        }

        /// <summary>
        /// Send message to client
        /// </summary>
        public async Task SendMessageAsync(string message)
        {
            try
            {
                if (!_isConnected || _writer == null)
                {
                    throw new InvalidOperationException("Client is not connected");
                }

                // Ensure only one writer writes at a time
                lock (_sendLock)
                {
                    // Write line-delimited JSON / message
                    _writer.WriteLine(message);
                    _writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] Error sending message: {ex.Message}");
                _isConnected = false;
            }
        }

        // Send a Packet object to client (thread-safe)
        public Task SendPacketAsync(Packet packet)
        {
            string json = PacketHelper.Serialize(packet);
            return SendMessageAsync(json);
        }

        /// <summary>
        /// Disconnect the client
        /// </summary>
        public Task DisconnectAsync()
        {
            try
            {
                _isConnected = false;
                _cancellationTokenSource.Cancel();
                try { _writer?.Close(); } catch { }
                try { _reader?.Close(); } catch { }
                try { _networkStream?.Close(); } catch { }
                try { _tcpClient?.Close(); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] Error disconnecting: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public string ClientId => _clientId;
        public bool IsConnected => _isConnected;
        public DateTime LastActivity => _lastActivity;
    }

    // Event args class
    public class MessageReceivedEventArgs : EventArgs
    {
        // Parsed packet when message is valid JSON
        public Packet? Packet { get; set; }

        // Raw message fallback
        public string? RawMessage { get; set; }
    }
}
