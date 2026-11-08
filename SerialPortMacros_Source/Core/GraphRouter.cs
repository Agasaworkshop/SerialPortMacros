namespace SerialPortMacros.Core;

/// <summary>
/// Inoltra i numeri ricevuti ai grafici aperti (Form4).
/// La UI modifica l'elenco; il thread di processing legge una copia immutabile
/// (nessun lock nel percorso caldo). <see cref="Form4.AddDataPoint(double, double)"/> è thread-safe.
/// </summary>
public sealed class GraphRouter
{
    private volatile Dictionary<string, Form4[]> _snapshot = new();

    /// <summary>Grafici aperti per sorgente. Da usare solo sul thread UI.</summary>
    public Dictionary<string, List<Form4>> Open { get; } = new();

    /// <summary>Pubblica le modifiche fatte su <see cref="Open"/> (thread UI).</summary>
    public void Publish()
    {
        var copy = new Dictionary<string, Form4[]>(Open.Count);
        foreach (var kv in Open)
            if (kv.Value.Count > 0)
                copy[kv.Key] = kv.Value.ToArray();
        _snapshot = copy;
    }

    /// <summary>Chiamato dal thread di processing.</summary>
    public void Route(string source, List<double> numbers, double clock)
    {
        var snap = _snapshot;
        if (snap.Count == 0 || numbers.Count == 0) return;
        if (!snap.TryGetValue(source, out var forms)) return;

        int count = Math.Min(forms.Length, numbers.Count);
        for (int i = 0; i < count; i++)
        {
            var graph = forms[i];
            if (graph != null && !graph.IsDisposed)
                graph.AddDataPoint(numbers[i], clock);
        }
    }
}
