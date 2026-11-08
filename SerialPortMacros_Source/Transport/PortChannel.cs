using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using SerialPortMacros.Core;
using SerialPortMacros.Decoding;

namespace SerialPortMacros.Transport;

public enum ConnectionMode
{
    Serial,
    Tcp
}

/// <summary>
/// Una porta logica (1..4): seriale oppure server TCP, con il proprio decoder.
/// Riceve byte sul thread del driver, li decodifica e passa i messaggi alla pipeline.
/// Non tocca mai la UI.
/// </summary>
public sealed class PortChannel
{
    private readonly DataPipeline _pipeline;
    private readonly object _serialSendLock = new();
    private readonly List<DecodedFrame> _frames = new();   // usato solo dal thread di ricezione
    private byte[] _serialBuffer = new byte[4096];
    private volatile IStreamDecoder _decoder = new TextLineDecoder();
    private volatile bool _serialOpen;
    private volatile string _tcpName;

    public PortChannel(int number, DataPipeline pipeline)
    {
        Number = number;
        _pipeline = pipeline;
        _tcpName = $"TCP{number}";

        SerialPortHelpers.ApplyDefaults(Serial);
        Serial.DataReceived += OnSerialData;

        Tcp.DataReceived += (buffer, count) => Decode(buffer.AsSpan(0, count), _tcpName);
        Tcp.ClientConnected += () => _decoder.Reset();
        Tcp.Status += (text, isError) => StatusMessage?.Invoke(text, isError);
    }

    /// <summary>Numero della porta logica (1..4).</summary>
    public int Number { get; }

    public SerialPort Serial { get; } = new();
    public TcpServerPort Tcp { get; } = new();

    public ConnectionMode Mode { get; set; } = ConnectionMode.Serial;

    public bool SerialOpen => _serialOpen;
    public bool IsActive => _serialOpen || Tcp.IsListening;

    /// <summary>Formato dati attivo (testo o binario).</summary>
    public PortDataFormat DataFormat { get; private set; } = new();

    /// <summary>Messaggi di stato (es. connessione TCP); sollevato da thread di rete.</summary>
    public event Action<string, bool>? StatusMessage;

    /// <summary>
    /// Nome usato per testo, log e grafici quando la porta è in TCP
    /// (aggiornato dalla UI: il nome della COM selezionata oppure "TCPn").
    /// </summary>
    public string TcpDisplayName
    {
        get => _tcpName;
        set => _tcpName = string.IsNullOrWhiteSpace(value) ? $"TCP{Number}" : value.Trim();
    }

    /// <summary>Applica un nuovo formato dati. Lancia <see cref="FormatException"/> se non valido.</summary>
    public void SetDataFormat(PortDataFormat format)
    {
        var decoder = format.CreateDecoder();
        DataFormat = format.Clone();
        _decoder = decoder;
    }

    // ------------------------------------------------------------------
    // Seriale
    // ------------------------------------------------------------------

    public bool OpenSerial(string portName, int baud, out string? error)
    {
        error = null;
        try
        {
            if (Serial.IsOpen) Serial.Close();
            Serial.PortName = portName;
            Serial.BaudRate = baud;
            _decoder.Reset();
            Serial.Open();
            _serialOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _serialOpen = false;
            return false;
        }
    }

    public void CloseSerial()
    {
        _serialOpen = false;
        try { if (Serial.IsOpen) Serial.Close(); } catch { }
    }

    private void OnSerialData(object sender, SerialDataReceivedEventArgs e)
    {
        string name;
        int read;
        try
        {
            int available = Serial.BytesToRead;
            if (available <= 0) return;
            if (available > _serialBuffer.Length)
                _serialBuffer = new byte[Math.Max(available, _serialBuffer.Length * 2)];

            read = Serial.Read(_serialBuffer, 0, available);
            name = Serial.PortName;
        }
        catch (Exception)
        {
            return;
        }

        if (!_serialOpen || string.IsNullOrWhiteSpace(name))
            return;

        Decode(_serialBuffer.AsSpan(0, read), name);
    }

    private void Decode(ReadOnlySpan<byte> data, string source)
    {
        // Normalmente arriva un solo flusso alla volta (seriale O TCP); il lock
        // protegge il decoder nel breve momento di passaggio da una modalità all'altra.
        lock (_frames)
        {
            _frames.Clear();
            try
            {
                _decoder.Feed(data, _frames);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decoder porta {Number}: {ex.Message}");
                _decoder.Reset();
                return;
            }
            _pipeline.EnqueueBatch(_frames, source, Number);
        }
    }

    // ------------------------------------------------------------------
    // Invio (thread-safe)
    // ------------------------------------------------------------------

    public bool Send(string text) => Send(Encoding.ASCII.GetBytes(text));

    public bool Send(byte[] data)
    {
        try
        {
            if (Mode == ConnectionMode.Serial)
            {
                if (!_serialOpen) return false;
                lock (_serialSendLock)
                    Serial.Write(data, 0, data.Length);
                return true;
            }

            return Tcp.Send(data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Invio porta {Number}: {ex.Message}");
            return false;
        }
    }
}
