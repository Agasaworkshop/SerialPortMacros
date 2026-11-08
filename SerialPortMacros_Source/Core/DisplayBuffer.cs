namespace SerialPortMacros.Core;

public readonly record struct DisplayLine(string Text, Color Color);

/// <summary>
/// Thread-safe buffer between the processing thread and the UI.
///
/// Processing adds lines without ever blocking; the UI takes them at its own pace
/// (timer). If the UI cannot keep up, the buffer keeps only the most recent lines and
/// counts the dropped ones: the display is decimated, while logging, macros and graphs
/// still receive all the data.
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

    /// <summary>Maximum pending lines; beyond this limit the oldest ones are dropped.</summary>
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
    /// Moves all pending lines into <paramref name="destination"/>.
    /// Returns the number of lines dropped since the previous call.
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
