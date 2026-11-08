using System.Net;
using System.Net.Sockets;

namespace SerialPortMacros.Transport;

/// <summary>
/// TCP server for one logical port: accepts one client at a time (e.g. an ESP) and
/// delivers the received bytes to <see cref="DataReceived"/>.
/// Events are raised from the receive task, not from the UI thread.
/// </summary>
public sealed class TcpServerPort
{
    private readonly object _sendLock = new();
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;

    public int LocalPort { get; private set; }

    public bool IsListening => _listener != null;
    public bool IsClientConnected => _client?.Connected == true && _stream != null;

    /// <summary>Bytes received (buffer, number of valid bytes).</summary>
    public event Action<byte[], int>? DataReceived;

    /// <summary>A new client connected (used to reset the decoder).</summary>
    public event Action? ClientConnected;

    /// <summary>Status messages to show (text, is it an error?).</summary>
    public event Action<string, bool>? Status;

    /// <summary>Starts listening. Throws if the port is not available.</summary>
    public void Start(int localPort)
    {
        Stop();

        var listener = new TcpListener(IPAddress.Any, localPort);
        listener.Start();

        var cts = new CancellationTokenSource();
        _listener = listener;
        _cts = cts;
        LocalPort = localPort;

        _ = Task.Run(() => AcceptLoopAsync(listener, cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        try { _client?.Close(); } catch { }

        _cts?.Dispose();
        _cts = null;
        _listener = null;
        _client = null;
        _stream = null;
    }

    public bool Send(byte[] data)
    {
        var stream = _stream;
        if (stream == null || _client?.Connected != true)
            return false;

        lock (_sendLock)
            stream.Write(data, 0, data.Length);
        return true;
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(token);

                // No Nagle delay and a write timeout: a send cannot block the
                // caller for long.
                client.NoDelay = true;
                client.SendTimeout = 1000;
                try { client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }

                _client = client;
                _stream = client.GetStream();
                ClientConnected?.Invoke();
                Status?.Invoke($"ESP connected to TCP port {LocalPort}", false);

                await ReceiveLoopAsync(client, token);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Status?.Invoke($"TCP error on port {LocalPort}: {ex.Message}", true);
            }
            finally
            {
                if (ReferenceEquals(_client, client))
                {
                    _stream = null;
                    _client = null;
                }
                try { client?.Close(); } catch { }
            }
        }
    }

    private async Task ReceiveLoopAsync(TcpClient client, CancellationToken token)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[4096];

        while (!token.IsCancellationRequested && client.Connected)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read == 0)
                break;

            DataReceived?.Invoke(buffer, read);
        }
    }
}
