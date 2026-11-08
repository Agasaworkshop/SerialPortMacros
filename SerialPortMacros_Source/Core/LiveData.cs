using System.Collections.Concurrent;

namespace SerialPortMacros.Core;

/// <summary>
/// Live values shared between processing, interfaces (Form7) and custom macros.
/// All collections are thread-safe.
/// </summary>
public sealed class LiveData
{
    /// <summary>Panel variables (Form7) and custom macro variables.</summary>
    public ConcurrentDictionary<string, double> Variables { get; } = new();

    /// <summary>Last received line, keyed by "port1".."port4" and by source name ("COM3").</summary>
    public ConcurrentDictionary<string, string> LatestText { get; } = new();

    /// <summary>Last received numbers per logical port (1..4).</summary>
    public ConcurrentDictionary<int, List<double>> LatestNumbers { get; } = new();

    private static readonly string[] PortKeys = { "", "port1", "port2", "port3", "port4" };

    public void Update(in RxMessage m, List<double> numbers)
    {
        LatestText[PortKeys[m.Port]] = m.Text;
        LatestText[m.Source] = m.Text;
        if (numbers.Count > 0)
            LatestNumbers[m.Port] = numbers;
    }
}
