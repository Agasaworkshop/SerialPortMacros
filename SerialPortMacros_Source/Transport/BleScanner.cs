using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace SerialPortMacros.Transport;

/// <summary>A BLE device seen while scanning.</summary>
public sealed class BleDeviceInfo
{
    public ulong Address { get; init; }
    public string Name { get; set; } = "";
    public short Rssi { get; set; }
    public bool HasUartService { get; set; }
    public DateTime LastSeen { get; set; }

    public string AddressText => FormatAddress(Address);

    public static string FormatAddress(ulong address) =>
        string.Join(":", Enumerable.Range(0, 6).Select(i => ((address >> (8 * (5 - i))) & 0xFF).ToString("X2")));
}

/// <summary>
/// Scans for BLE advertisements (active scan, so the names in scan responses are seen too).
/// <see cref="DeviceUpdated"/> is raised from a background thread.
/// </summary>
public sealed class BleScanner : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher = new() { ScanningMode = BluetoothLEScanningMode.Active };
    private readonly Dictionary<ulong, BleDeviceInfo> _devices = new();
    private readonly object _lock = new();

    public event Action<BleDeviceInfo>? DeviceUpdated;

    /// <summary>Scanning stopped by Windows (e.g. Bluetooth turned off); the text explains why.</summary>
    public event Action<string>? Stopped;

    public BleScanner()
    {
        _watcher.Received += OnReceived;
        _watcher.Stopped += (_, args) =>
        {
            if (args.Error != BluetoothError.Success)
                Stopped?.Invoke(args.Error == BluetoothError.RadioNotAvailable
                    ? "Bluetooth is off or not available."
                    : $"Scan stopped: {args.Error}");
        };
    }

    /// <summary>Null if BLE can be used, otherwise the reason it cannot.</summary>
    public static async Task<string?> CheckAdapterAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null) return "No Bluetooth adapter found.";
            if (!adapter.IsLowEnergySupported) return "The Bluetooth adapter does not support Bluetooth LE.";
            return null;
        }
        catch (Exception ex)
        {
            return "Bluetooth is not available: " + ex.Message;
        }
    }

    public void Start() => _watcher.Start();

    public void Stop()
    {
        try { _watcher.Stop(); } catch { }
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        BleDeviceInfo info;
        lock (_lock)
        {
            if (!_devices.TryGetValue(args.BluetoothAddress, out info!))
            {
                info = new BleDeviceInfo { Address = args.BluetoothAddress };
                _devices[args.BluetoothAddress] = info;
            }

            // Names and service lists can arrive in different packets (advertisement / scan response).
            if (!string.IsNullOrWhiteSpace(args.Advertisement.LocalName))
                info.Name = args.Advertisement.LocalName;
            if (args.Advertisement.ServiceUuids.Contains(BleUartPort.ServiceUuid))
                info.HasUartService = true;

            info.Rssi = args.RawSignalStrengthInDBm;
            info.LastSeen = DateTime.Now;

            // Hand out a copy: the stored object keeps changing on this thread.
            info = new BleDeviceInfo
            {
                Address = info.Address,
                Name = info.Name,
                Rssi = info.Rssi,
                HasUartService = info.HasUartService,
                LastSeen = info.LastSeen
            };
        }

        DeviceUpdated?.Invoke(info);
    }

    public void Dispose()
    {
        Stop();
        _watcher.Received -= OnReceived;
    }
}
