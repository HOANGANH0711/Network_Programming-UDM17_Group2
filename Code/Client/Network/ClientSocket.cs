using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shared.Models;

namespace Client.Network
{
    public class ClientSocket : IDisposable
    {
        private TcpClient? client;
        private StreamReader? reader;
        private StreamWriter? writer;
        private CancellationTokenSource? receiveCts;

        public event Action<Packet>? OnPacketReceived;
        public event Action<string>? OnError;
        public event Action? OnDisconnected;

        public bool IsConnected => client?.Connected == true;

        public async Task ConnectAsync(string ip, int port)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(ip, port);

                NetworkStream stream = client.GetStream();
                reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true
                };

                receiveCts = new CancellationTokenSource();
                _ = Task.Run(() => ReceiveLoopAsync(receiveCts.Token));
            }
            catch (Exception ex)
            {
                Dispose();
                throw new InvalidOperationException("Khong the ket noi server.", ex);
            }
        }

        public async Task SendAsync(Packet packet)
        {
            if (writer == null || !IsConnected)
                throw new InvalidOperationException("Chua ket noi server.");

            string json = Serializer.Serialize(packet);
            await writer.WriteLineAsync(json);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && reader != null)
                {
                    string? json = await reader.ReadLineAsync();

                    if (json == null)
                        break;

                    Packet? packet = Serializer.Deserialize(json);

                    if (packet != null)
                        OnPacketReceived?.Invoke(packet);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                OnDisconnected?.Invoke();
            }
        }

        public void Dispose()
        {
            receiveCts?.Cancel();
            writer?.Dispose();
            reader?.Dispose();
            client?.Close();
            receiveCts?.Dispose();

            receiveCts = null;
            writer = null;
            reader = null;
            client = null;
        }
    }
}
