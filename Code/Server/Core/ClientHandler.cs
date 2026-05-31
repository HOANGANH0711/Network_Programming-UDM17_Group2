using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private string _clientId;
        private bool _isConnected;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler? Disconnected;

        public ClientHandler(TcpClient tcpClient, string clientId)
        {
            _tcpClient = tcpClient;
            _clientId = clientId;
            _networkStream = tcpClient.GetStream();
            _isConnected = true;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Handle client communication (receive messages)
        /// </summary>
        public async Task HandleAsync()
        {
            try
            {
                byte[] buffer = new byte[4096];

                while (_isConnected && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token);

                        if (bytesRead == 0)
                        {
                            // Client disconnected
                            _isConnected = false;
                            break;
                        }

                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs { Message = message });
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        _isConnected = false;
                        break;
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
                if (!_isConnected || _networkStream == null)
                {
                    throw new InvalidOperationException("Client is not connected");
                }

                byte[] data = Encoding.UTF8.GetBytes(message);
                await _networkStream.WriteAsync(data, 0, data.Length);
                await _networkStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] Error sending message: {ex.Message}");
                _isConnected = false;
            }
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
                _networkStream?.Close();
                _tcpClient?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientId}] Error disconnecting: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public string ClientId => _clientId;
        public bool IsConnected => _isConnected;
    }

    // Event args class
    public class MessageReceivedEventArgs : EventArgs
    {
        public string? Message { get; set; }
    }
}
