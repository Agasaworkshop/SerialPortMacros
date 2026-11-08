using System.Collections.Concurrent;

// =====================================================================================
//  PUBLIC API FOR CUSTOM MACROS
//
//  A custom macro is a C# class that implements ICustomMacro (or derives from
//  SimpleMacro). It can live in:
//    - a .cs file in the "custom_macros" folder: it is compiled automatically;
//    - a separately built .dll (Class Library project referencing SerialPortMacros.dll).
//
//  The code is called by the processing thread for every message that passes the
//  macro filter (key + ports), never in parallel with itself.
//  It must be fast: slow code slows down the whole pipeline. For long or periodic
//  work use your own thread/timer and IMacroContext.Send().
// =====================================================================================

namespace SerialPortMacros.Macros;

/// <summary>The message that triggered the macro.</summary>
public sealed class MacroInput
{
    public MacroInput(int port, string source, string message, IReadOnlyList<double> values, DateTime timestamp, double time)
    {
        Port = port;
        Source = source;
        Message = message;
        Values = values;
        Timestamp = timestamp;
        Time = time;
    }

    /// <summary>Source logical port (1..4).</summary>
    public int Port { get; }

    /// <summary>Source name ("COM3", "TCP1", ...).</summary>
    public string Source { get; }

    /// <summary>The received string (for binary frames: the values rendered as text).</summary>
    public string Message { get; }

    /// <summary>
    /// The message numbers: parsed from the text or decoded from the binary frame
    /// (in the order of the defined fields).
    /// </summary>
    public IReadOnlyList<double> Values { get; }

    /// <summary>Receive time.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Monotonic time in seconds (same clock as the graphs): handy to compute dt.</summary>
    public double Time { get; }
}

/// <summary>A message to send. Use the static methods to create it.</summary>
public readonly struct MacroOutput
{
    /// <summary>Reply on the same port the message came from.</summary>
    public const int SourcePort = 0;

    /// <summary>Send to all four ports.</summary>
    public const int AllPorts = -1;

    private MacroOutput(int port, string? text, byte[]? data)
    {
        Port = port;
        Text = text;
        Data = data;
    }

    /// <summary>1..4, <see cref="SourcePort"/> or <see cref="AllPorts"/>.</summary>
    public int Port { get; }

    /// <summary>Text to send (already including any line ending).</summary>
    public string? Text { get; }

    /// <summary>Raw bytes to send (instead of <see cref="Text"/>).</summary>
    public byte[]? Data { get; }

    /// <summary>Text followed by a line ending (default "\n").</summary>
    public static MacroOutput Line(int port, string text, string lineEnding = "\n") =>
        new(port, text + lineEnding, null);

    /// <summary>Text sent as is.</summary>
    public static MacroOutput Raw(int port, string text) => new(port, text, null);

    /// <summary>Raw bytes (e.g. a binary frame for the micro).</summary>
    public static MacroOutput Bytes(int port, byte[] data) => new(port, null, data);

    /// <summary>Reply line on the source port.</summary>
    public static MacroOutput Reply(string text, string lineEnding = "\n") => Line(SourcePort, text, lineEnding);
}

/// <summary>Services available to the macro.</summary>
public interface IMacroContext
{
    /// <summary>Variables shared with the interfaces (Form7) and the other macros.</summary>
    ConcurrentDictionary<string, double> Variables { get; }

    /// <summary>Monotonic time in seconds (same clock as <see cref="MacroInput.Time"/>).</summary>
    double Now { get; }

    /// <summary>Sends a line. Can also be called from the macro's own threads/timers.</summary>
    void Send(int port, string text, string lineEnding = "\n");

    /// <summary>Sends raw bytes.</summary>
    void SendBytes(int port, byte[] data);

    /// <summary>Writes a message to the main window.</summary>
    void Print(string text);

    /// <summary>Last line received on a port (1..4), or null.</summary>
    string? LatestMessage(int port);

    /// <summary>Last numbers received on a port (1..4), or null.</summary>
    IReadOnlyList<double>? LatestValues(int port);
}

/// <summary>Full contract of a custom macro.</summary>
public interface ICustomMacro
{
    /// <summary>Called when macros are started (Play button) or reloaded.</summary>
    void Start(IMacroContext context) { }

    /// <summary>
    /// Processes a message. Return the messages to send, or null / empty to send
    /// nothing. "yield return" can be used.
    /// </summary>
    IEnumerable<MacroOutput>? Process(MacroInput input);

    /// <summary>Called when macros are stopped or reloaded.</summary>
    void Stop() { }
}

/// <summary>
/// Simplified version: (port, string) in -> (port, string) out.
/// Return null to send nothing. "\n" is appended to the output string.
/// </summary>
public abstract class SimpleMacro : ICustomMacro
{
    /// <summary>Available from <see cref="OnStart"/> onwards.</summary>
    protected IMacroContext Context { get; private set; } = null!;

    void ICustomMacro.Start(IMacroContext context)
    {
        Context = context;
        OnStart();
    }

    void ICustomMacro.Stop() => OnStop();

    IEnumerable<MacroOutput>? ICustomMacro.Process(MacroInput input)
    {
        var result = OnMessage(input.Port, input.Message);
        return result is { } r ? new[] { MacroOutput.Line(r.Port, r.Message) } : null;
    }

    protected virtual void OnStart() { }
    protected virtual void OnStop() { }

    /// <summary>
    /// <paramref name="port"/>: source port 1..4. Return the destination port
    /// (1..4, 0 = same port, -1 = all) and the message, or null.
    /// </summary>
    public abstract (int Port, string Message)? OnMessage(int port, string message);
}
