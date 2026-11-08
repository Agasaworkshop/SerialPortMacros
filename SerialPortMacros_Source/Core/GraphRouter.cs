namespace SerialPortMacros.Core;

/// <summary>
/// Forwards received numbers to the open graphs (Form4).
/// The UI edits the list; the processing thread reads an immutable snapshot
/// (no lock on the hot path). <see cref="Form4.AddDataPoint(double, double)"/> is thread-safe.
/// </summary>
public sealed class GraphRouter
{
    private volatile Dictionary<string, Form4[]> _snapshot = new();

    /// <summary>Open graphs per source. UI thread only.</summary>
    public Dictionary<string, List<Form4>> Open { get; } = new();

    /// <summary>Publishes the changes made to <see cref="Open"/> (UI thread).</summary>
    public void Publish()
    {
        var copy = new Dictionary<string, Form4[]>(Open.Count);
        foreach (var kv in Open)
            if (kv.Value.Count > 0)
                copy[kv.Key] = kv.Value.ToArray();
        _snapshot = copy;
    }

    /// <summary>Called by the processing thread.</summary>
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
