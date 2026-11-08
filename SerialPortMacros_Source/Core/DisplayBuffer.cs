namespace SerialPortMacros.Core;

public readonly record struct DisplayLine(string Text, Color Color);

/// <summary>
/// Buffer thread-safe fra il thread di processing e la UI.
///
/// Il processing aggiunge righe senza mai bloccarsi; la UI le preleva al proprio ritmo
/// (timer). Se la UI non riesce a stare al passo il buffer tiene solo le righe più
/// recenti e conta quelle scartate: la visualizzazione viene "decimata", mentre log,
/// macro e grafici ricevono comunque tutti i dati.
/// </summary>
public sealed class DisplayBuffer
{
    private readonly object _lock = new();
    private readonly Queue<DisplayLine> _lines = new();
    private long _dropped;

    public DisplayBuffer(int maxPendingLines = 2000)
    {
        MaxPendingLines = maxPendingLines;
    }

    /// <summary>Righe massime in attesa; oltre questa soglia le più vecchie vengono scartate.</summary>
    public int MaxPendingLines { get; set; }

    public int Count
    {
        get { lock (_lock) return _lines.Count; }
    }

    public void Add(string text, Color color)
    {
        lock (_lock)
        {
            _lines.Enqueue(new DisplayLine(text, color));
            while (_lines.Count > MaxPendingLines)
            {
                _lines.Dequeue();
                _dropped++;
            }
        }
    }

    /// <summary>
    /// Sposta tutte le righe in attesa in <paramref name="destination"/>.
    /// Restituisce il numero di righe scartate dall'ultimo prelievo.
    /// </summary>
    public long DrainTo(List<DisplayLine> destination)
    {
        lock (_lock)
        {
            destination.AddRange(_lines);
            _lines.Clear();
            long dropped = _dropped;
            _dropped = 0;
            return dropped;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _lines.Clear();
            _dropped = 0;
        }
    }
}
