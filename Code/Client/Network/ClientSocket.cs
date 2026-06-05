using System.Net.Sockets;
using System.Text;
using Shared.Models;

namespace Client.Network
{
    public sealed class ClientSocket : IDisposable
    {
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _cts;

        public event Action<Packet>? PacketReceived;
        public event Action<string>? Disconnected;

        public bool IsConnected => _client?.Connected == true;

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            Disconnect();
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken);
            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        public async Task SendAsync(Packet packet, CancellationToken cancellationToken = default)
        {
            if (_writer == null)
                throw new InvalidOperationException("Chua ket noi server.");

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await _writer.WriteLineAsync(PacketHelper.Serialize(packet).AsMemory(), cancellationToken);
                await _writer.FlushAsync(cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _reader != null)
                {
                    var line = await _reader.ReadLineAsync(cancellationToken);
                    if (line == null)
                        break;

                    var packet = PacketHelper.Deserialize(line);
                    if (packet != null)
                        PacketReceived?.Invoke(packet);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Disconnected?.Invoke(ex.Message);
                return;
            }

            Disconnected?.Invoke("Mat ket noi server.");
        }

        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }
            _reader = null;
            _writer = null;
            _client = null;
            _cts = null;
        }

        public void Dispose()
        {
            Disconnect();
            _sendLock.Dispose();
        }
    }
}
