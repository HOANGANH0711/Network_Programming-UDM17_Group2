using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestClient
{
    /// <summary>
    /// Simple test client for verifying server connectivity
    /// Demonstrates 1-n client connections capability
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔═════════════════════════════════════╗");
            Console.WriteLine("║  Caro Online - Test Client         ║");
            Console.WriteLine("║  Press 's' for single client test   ║");
            Console.WriteLine("║  Press 'm' for multiple clients     ║");
            Console.WriteLine("║  Press 'q' to quit                 ║");
            Console.WriteLine("╚═════════════════════════════════════╝");
            Console.WriteLine();

            while (true)
            {
                Console.Write("\nSelect test mode (s/m/q): ");
                string input = Console.ReadLine()?.ToLower();

                switch (input)
                {
                    case "s":
                        await TestSingleClientAsync();
                        break;
                    case "m":
                        Console.Write("Enter number of test clients (2-10): ");
                        if (int.TryParse(Console.ReadLine(), out int count) && count >= 2 && count <= 10)
                        {
                            await TestMultipleClientsAsync(count);
                        }
                        else
                        {
                            Console.WriteLine("Invalid input. Please enter a number between 2 and 10.");
                        }
                        break;
                    case "q":
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }

        static async Task TestSingleClientAsync()
        {
            Console.WriteLine("\n--- Single Client Connection Test ---");
            try
            {
                var client = new SimpleTestClient("127.0.0.1", 5000, "TestClient_1");
                await client.ConnectAsync();

                if (client.IsConnected)
                {
                    await client.SendMessageAsync("Hello from Test Client 1");
                    await Task.Delay(2000);
                    await client.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static async Task TestMultipleClientsAsync(int numberOfClients)
        {
            Console.WriteLine($"\n--- Multiple Client Connection Test ({numberOfClients} clients) ---");

            var tasks = new Task[numberOfClients];
            var clients = new SimpleTestClient[numberOfClients];

            // Connect all clients
            for (int i = 0; i < numberOfClients; i++)
            {
                int index = i;
                clients[i] = new SimpleTestClient("127.0.0.1", 5000, $"TestClient_{i + 1}");
                tasks[i] = clients[i].ConnectAsync();
            }

            await Task.WhenAll(tasks);

            // Send messages from each client
            await Task.Delay(1000);
            for (int i = 0; i < numberOfClients; i++)
            {
                if (clients[i].IsConnected)
                {
                    await clients[i].SendMessageAsync($"Message from TestClient_{i + 1}");
                }
            }

            // Wait a bit
            await Task.Delay(2000);

            // Disconnect all clients
            for (int i = 0; i < numberOfClients; i++)
            {
                await clients[i].DisconnectAsync();
            }

            Console.WriteLine("All clients disconnected.");
        }
    }

    class SimpleTestClient
    {
        private string _host;
        private int _port;
        private string _clientName;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isConnected;

        public SimpleTestClient(string host, int port, string clientName)
        {
            _host = host;
            _port = port;
            _clientName = clientName;
            _isConnected = false;
        }

        public async Task ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_host, _port);
                _networkStream = _tcpClient.GetStream();
                _isConnected = true;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{_clientName}] Connected to server {_host}:{_port}");
                Console.ResetColor();

                // Start receiving messages
                _ = ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{_clientName}] Connection failed: {ex.Message}");
                Console.ResetColor();
                _isConnected = false;
            }
        }

        public async Task SendMessageAsync(string message)
        {
            try
            {
                if (!_isConnected) return;

                byte[] data = Encoding.UTF8.GetBytes(message);
                await _networkStream.WriteAsync(data, 0, data.Length);
                await _networkStream.FlushAsync();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{_clientName}] Sent: {message}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientName}] Send error: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            try
            {
                byte[] buffer = new byte[1024];

                while (_isConnected)
                {
                    int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        _isConnected = false;
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[{_clientName}] Received: {message}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientName}] Receive error: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _isConnected = false;
                _networkStream?.Close();
                _tcpClient?.Close();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[{_clientName}] Disconnected from server");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_clientName}] Disconnect error: {ex.Message}");
            }
        }

        public bool IsConnected => _isConnected;
    }
}
