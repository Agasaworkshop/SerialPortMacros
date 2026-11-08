namespace SerialPortMacros.Core;

/// <summary>
/// A message received from a logical port, already split and timestamped.
/// </summary>
public readonly struct RxMessage
{
    /// <summary>Line text (for binary frames: the values rendered as text).</summary>
    public readonly string Text;

    /// <summary>Source name shown on screen ("COM3", "TCP1", ...).</summary>
    public readonly string Source;

    /// <summary>Logical port 1..4.</summary>
    public readonly int Port;

    /// <summary>Wall-clock receive time (for logs and text).</summary>
    public readonly DateTime Wall;

    /// <summary>Monotonic clock shared by the graphs (<see cref="Form4.NowSeconds"/>).</summary>
    public readonly double Clock;

    /// <summary>
    /// Already decoded numeric values (binary frames). If null, the numbers are
    /// extracted from the text with <see cref="NumberParser"/>.
    /// </summary>
    public readonly double[]? Values;

    public RxMessage(string text, string source, int port, DateTime wall, double clock, double[]? values)
    {
        Text = text;
        Source = source;
        Port = port;
        Wall = wall;
        Clock = clock;
        Values = values;
    }
}
