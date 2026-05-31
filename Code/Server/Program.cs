using Server.Core;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║    Caro Online - Game Server        ║");
        Console.WriteLine("║       Member 1: Server Core          ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine();

        // Initialize ServerManager
        int port = 5000;
        var serverManager = new ServerManager(port);

        // Subscribe to connection events
        serverManager.ClientConnected += (sender, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"→ Event: Client Connected [ID: {e.ClientId}]");
            Console.WriteLine($"  Connected Clients: {serverManager.GetClientCount()}");
            Console.ResetColor();
        };

        serverManager.ClientDisconnected += (sender, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"← Event: Client Disconnected [ID: {e.ClientId}]");
            Console.WriteLine($"  Remaining Clients: {serverManager.GetClientCount()}");
            Console.ResetColor();
        };

        // Create a cancellation token for graceful shutdown
        var cts = new CancellationTokenSource();

        // Handle Ctrl+C for graceful shutdown
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            // Start the server
            var serverTask = serverManager.StartAsync();

            // Monitor server status
            var monitorTask = MonitorServerAsync(serverManager, cts.Token);

            // Wait for cancellation
            await Task.WhenAll(serverTask, monitorTask);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Critical Error: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            // Gracefully stop the server
            await serverManager.StopAsync();
            Console.WriteLine("\nServer shutdown complete.");
        }
    }

    static async Task MonitorServerAsync(ServerManager serverManager, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10000, cancellationToken); // Check every 10 seconds

                if (serverManager.IsRunning)
                {
                    int clientCount = serverManager.GetClientCount();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"\n[Monitor] Active Connections: {clientCount}");
                    Console.ResetColor();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }
}
