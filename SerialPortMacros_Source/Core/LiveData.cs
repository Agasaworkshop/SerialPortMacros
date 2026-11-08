using System.Collections.Concurrent;

namespace SerialPortMacros.Core;

/// <summary>
/// Valori "live" condivisi fra processing, interfacce (Form7) e macro custom.
/// Tutte le collezioni sono thread-safe.
/// </summary>
public sealed class LiveData
{
    /// <summary>Variabili dei pannelli (Form7) e delle macro custom.</summary>
    public ConcurrentDictionary<string, double> Variables { get; } = new();

    /// <summary>Ultima riga ricevuta, per chiave "port1".."port4" e per nome sorgente ("COM3").</summary>
    public ConcurrentDictionary<string, string> LatestText { get; } = new();

    /// <summary>Ultimi numeri ricevuti per porta logica (1..4).</summary>
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
