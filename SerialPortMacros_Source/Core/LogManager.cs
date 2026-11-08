namespace SerialPortMacros.Core;

/// <summary>
/// Gestione thread-safe dei file di log di una sessione.
/// Un file per ogni porta abilitata + un file "misto" quando le porte sono più di una.
/// La scrittura è bufferizzata: <see cref="Flush"/> viene chiamato periodicamente
/// dal thread di processing.
/// </summary>
public sealed class LogManager
{
    private readonly object _lock = new();
    private readonly StreamWriter?[] _portWriters = new StreamWriter?[AppSettings.PortCount];
    private StreamWriter? _mixedWriter;
    private volatile bool _active;

    /// <summary>Aggiunge "HH:mm:ss.fff | " a ogni riga.</summary>
    public volatile bool TimeStamps = true;

    /// <summary>Se true, anche i comandi inviati (utente, macro, sequenze) vengono loggati.</summary>
    public volatile bool LogUserInput;

    public bool IsActive => _active;

    public string?[] PortLogPaths { get; } = new string?[AppSettings.PortCount];
    public string? MixedLogPath { get; private set; }
    public string? SessionDirectory { get; private set; }

    /// <summary>Apre una nuova sessione di log. <paramref name="enabledPorts"/> ha 4 elementi.</summary>
    public void Start(bool[] enabledPorts)
    {
        lock (_lock)
        {
            CloseWritersLocked();

            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            SessionDirectory = Path.Combine(AppPaths.Logs, stamp);
            Directory.CreateDirectory(SessionDirectory);

            int count = 0;
            for (int i = 0; i < AppSettings.PortCount; i++)
            {
                PortLogPaths[i] = null;
                if (!enabledPorts[i]) continue;

                count++;
                PortLogPaths[i] = Path.Combine(SessionDirectory, $"log{i + 1}.txt");
                _portWriters[i] = CreateWriter(PortLogPaths[i]!);
            }

            MixedLogPath = null;
            if (count > 1)
            {
                MixedLogPath = Path.Combine(SessionDirectory, "log_mixed.txt");
                _mixedWriter = CreateWriter(MixedLogPath);
            }

            _active = true;
        }
    }

    /// <summary>Chiude la sessione. Restituisce i percorsi dei log per porta (null se non usata).</summary>
    public string?[] Stop()
    {
        lock (_lock)
        {
            _active = false;
            CloseWritersLocked();
            return (string?[])PortLogPaths.Clone();
        }
    }

    /// <summary>Riga ricevuta dalla porta logica <paramref name="logicalPort"/> (1..4).</summary>
    public void WriteReceived(int logicalPort, string source, string text, DateTime when)
    {
        if (!_active) return;

        string? stamp = TimeStamps ? when.ToString("HH:mm:ss.fff") : null;

        lock (_lock)
        {
            var w = _portWriters[logicalPort - 1];
            if (w != null)
                w.WriteLine(stamp != null ? $"{stamp} | {text}" : text);

            _mixedWriter?.WriteLine(stamp != null ? $"{stamp} | {source}: {text}" : $"{source}: {text}");
        }
    }

    /// <summary>Comando inviato: scritto in tutti i log aperti (se abilitato).</summary>
    public void WriteSent(string text)
    {
        if (!_active || !LogUserInput) return;

        string line = TimeStamps
            ? $"{DateTime.Now:HH:mm:ss.fff} | You: {text}"
            : $"You: {text}";

        lock (_lock)
        {
            foreach (var w in _portWriters)
                w?.WriteLine(line);
            _mixedWriter?.WriteLine(line);
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            foreach (var w in _portWriters)
                try { w?.Flush(); } catch { }
            try { _mixedWriter?.Flush(); } catch { }
        }
    }

    private void CloseWritersLocked()
    {
        for (int i = 0; i < _portWriters.Length; i++)
        {
            try { _portWriters[i]?.Dispose(); } catch { }
            _portWriters[i] = null;
        }
        try { _mixedWriter?.Dispose(); } catch { }
        _mixedWriter = null;
    }

    private static StreamWriter CreateWriter(string path) =>
        new(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16))
        {
            AutoFlush = false
        };
}
