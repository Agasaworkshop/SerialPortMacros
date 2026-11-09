using System.Diagnostics;
using System.Threading.Channels;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace SerialPortMacros.Transport;

/// <summary>
/// "Serial over Bluetooth LE" with the Nordic UART Service (NUS), the de facto standard
/// used by ESP32, nRF and many BLE modules:
///   service 6E400001-..., RX 6E400002-... (PC writes), TX 6E400003-... (device notifies).
///
/// Received bytes are delivered to <see cref="DataReceived"/> from a WinRT thread pool
/// thread. Sends are queued and written in order by a background task, in chunks that
/// fit the negotiated MTU. Windows keeps trying to reconnect while the port is open.
/// </summary>
public sealed class BleUartPort
{
    public static readonly Guid ServiceUuid = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
    public static readonly Guid RxUuid = Guid.Parse("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");  // PC -> device
    public static readonly Guid TxUuid = Guid.Parse("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");  // device -> PC

    private readonly object _lock = new();
    private BluetoothLEDevice? _device;
    private GattSession? _session;
    private GattDeviceService? _service;
    private GattCharacteristic? _rx;
    private GattCharacteristic? _tx;
    private Channel<byte[]>? _sendQueue;
    private bool _writeWithoutResponse;
    private volatile bool _open;

    /// <summary>Bytes received (buffer, number of valid bytes).</summary>
    public event Action<byte[], int>? DataReceived;

    /// <summary>The link is up (used to reset the decoder).</summary>
    public event Action? Connected;

    /// <summary>Status messages to show (text, is it an error?).</summary>
    public event Action<string, bool>? Status;

    /// <summary>True between a successful <see cref="ConnectAsync"/> and <see cref="Disconnect"/>.</summary>
    public bool IsOpen => _open;

    /// <summary>True while the radio link is actually up.</summary>
    public bool IsLinkUp => _device?.ConnectionStatus == BluetoothConnectionStatus.Connected;

    public string DeviceName { get; private set; } = "";

    /// <summary>Connects to the device and subscribes to the TX notifications.</summary>
    public async Task<bool> ConnectAsync(ulong address, string name)
    {
        Disconnect();
        DeviceName = name;

        try
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (device == null)
            {
                Status?.Invoke($"BLE device {name} not found (is it on and in range?)", true);
                return false;
            }

            // Ask Windows to keep the link up and to reconnect automatically.
            var session = await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId);
            session.MaintainConnection = true;

            var services = await device.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached);
            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            {
                session.Dispose();
                device.Dispose();
                Status?.Invoke($"{name} does not offer the Nordic UART Service ({services.Status})", true);
                return false;
            }

            var service = services.Services[0];
            var rx = await FindCharacteristicAsync(service, RxUuid);
            var tx = await FindCharacteristicAsync(service, TxUuid);
            if (rx == null || tx == null)
            {
                service.Dispose();
                session.Dispose();
                device.Dispose();
                Status?.Invoke($"{name}: Nordic UART RX/TX characteristics not found", true);
                return false;
            }

            tx.ValueChanged += OnValueChanged;
            var cccd = await tx.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (cccd != GattCommunicationStatus.Success)
            {
                tx.ValueChanged -= OnValueChanged;
                service.Dispose();
                session.Dispose();
                device.Dispose();
                Status?.Invoke($"{name}: cannot enable notifications ({cccd})", true);
                return false;
            }

            lock (_lock)
            {
                _device = device;
                _session = session;
                _service = service;
                _rx = rx;
                _tx = tx;
                _writeWithoutResponse = rx.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse);
                _sendQueue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
                _open = true;
            }

            device.ConnectionStatusChanged += OnConnectionStatusChanged;
            _ = Task.Run(() => SendLoopAsync(_sendQueue));

            Connected?.Invoke();
            Status?.Invoke($"BLE connected to {name}", false);
            return true;
        }
        catch (Exception ex)
        {
            Status?.Invoke($"BLE error with {name}: {ex.Message}", true);
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        GattCharacteristic? tx;
        BluetoothLEDevice? device;
        GattSession? session;
        GattDeviceService? service;

        lock (_lock)
        {
            if (!_open && _device == null) return;
            _open = false;

            tx = _tx; device = _device; session = _session; service = _service;
            _tx = null; _rx = null; _device = null; _session = null; _service = null;
            _sendQueue?.Writer.TryComplete();
            _sendQueue = null;
        }

        try { if (tx != null) tx.ValueChanged -= OnValueChanged; } catch { }
        try { if (device != null) device.ConnectionStatusChanged -= OnConnectionStatusChanged; } catch { }
        try { if (session != null) session.MaintainConnection = false; } catch { }
        try { service?.Dispose(); } catch { }
        try { session?.Dispose(); } catch { }
        try { device?.Dispose(); } catch { }
    }

    /// <summary>Queues data to send. Returns false if the port is not open.</summary>
    public bool Send(byte[] data)
    {
        var queue = _sendQueue;
        if (!_open || queue == null) return false;
        return queue.Writer.TryWrite(data);
    }

    // ------------------------------------------------------------------

    private static async Task<GattCharacteristic?> FindCharacteristicAsync(GattDeviceService service, Guid uuid)
    {
        var result = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Uncached);
        return result.Status == GattCommunicationStatus.Success && result.Characteristics.Count > 0
            ? result.Characteristics[0]
            : null;
    }

    private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var buffer = args.CharacteristicValue;
        if (buffer == null || buffer.Length == 0) return;

        var bytes = new byte[buffer.Length];
        using (var reader = DataReader.FromBuffer(buffer))
            reader.ReadBytes(bytes);

        DataReceived?.Invoke(bytes, bytes.Length);
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (!_open) return;

        if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
        {
            Connected?.Invoke();
            Status?.Invoke($"BLE link to {DeviceName} restored", false);
        }
        else
        {
            Status?.Invoke($"BLE link to {DeviceName} lost, Windows will try to reconnect", true);
        }
    }

    /// <summary>Writes the queued data in order, split into chunks that fit one BLE packet.</summary>
    private async Task SendLoopAsync(Channel<byte[]> queue)
    {
        try
        {
            await foreach (var data in queue.Reader.ReadAllAsync())
            {
                var rx = _rx;
                if (rx == null) break;

                int chunk = Math.Max(20, (_session?.MaxPduSize ?? 23) - 3);
                for (int offset = 0; offset < data.Length; offset += chunk)
                {
                    int count = Math.Min(chunk, data.Length - offset);
                    var part = new byte[count];
                    System.Buffer.BlockCopy(data, offset, part, 0, count);

                    var option = _writeWithoutResponse ? GattWriteOption.WriteWithoutResponse : GattWriteOption.WriteWithResponse;
                    var status = await rx.WriteValueAsync(CryptographicBuffer.CreateFromByteArray(part), option);
                    if (status != GattCommunicationStatus.Success)
                        Debug.WriteLine($"BLE write to {DeviceName}: {status}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BLE send loop: {ex.Message}");
        }
    }
}
