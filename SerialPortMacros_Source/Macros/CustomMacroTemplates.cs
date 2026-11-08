using SerialPortMacros.Core;

namespace SerialPortMacros.Macros;

/// <summary>Example files created in the custom_macros folder.</summary>
public static class CustomMacroTemplates
{
    public const string Simple = @"// Simple custom macro: (port, string) in -> (port, string) out.
// This file is compiled automatically when you press Play.
//
// In the Macros window choose ""Custom code"" and enter the name of this file.
// The macro key and ports decide which messages reach this code
// (key ""*"" = every message from the selected ports).

public class MyMacro : SimpleMacro
{
    protected override void OnStart()
    {
        Context.Print(""MyMacro started"");
    }

    // port: source port (1..4). Return (destination port, message)
    // or null to send nothing. Port 0 = same port, -1 = all ports.
    public override (int Port, string Message)? OnMessage(int port, string message)
    {
        if (message.StartsWith(""PING""))
            return (port, ""PONG"");

        return null;
    }
}";

    public const string Full = @"// Full custom macro (ICustomMacro).
//
//  input.Port     source port 1..4
//  input.Message  the received string (for binary frames: the values rendered as text)
//  input.Values   the message numbers (parsed from the text or decoded from the binary frame)
//  input.Time     time in seconds (monotonic, handy to compute dt)
//
//  Return zero, one or more MacroOutput:
//    MacroOutput.Line(2, ""text"")           line + ""\n"" on port 2
//    MacroOutput.Raw(1, ""text"")            text sent as is
//    MacroOutput.Bytes(1, new byte[]{..})  raw bytes
//    MacroOutput.Reply(""ok"")               line on the source port
//
//  Context (passed to Start):
//    Context.Variables[""name""]   variables shared with the interfaces (Form7)
//    Context.Send(port, text)    send from your own timer/thread
//    Context.Print(text)         writes to the main window
//
//  Process runs on the processing thread, never in parallel with itself:
//  keep it fast. Field values persist between messages
//  (they are reset when the macro is reloaded).

public class MyFullMacro : ICustomMacro
{
    private IMacroContext Context;
    private double _lastTime;

    public void Start(IMacroContext context)
    {
        Context = context;
        _lastTime = context.Now;
    }

    public IEnumerable<MacroOutput> Process(MacroInput input)
    {
        if (input.Values.Count == 0)
            yield break;

        double dt = input.Time - _lastTime;
        _lastTime = input.Time;

        double value = input.Values[0];
        Context.Variables[""last_value""] = value;

        // Example: send the first value, doubled, to port 2.
        yield return MacroOutput.Line(2, (value * 2).ToString(CultureInfo.InvariantCulture));
    }

    public void Stop()
    {
    }
}";

    /// <summary>Creates the examples if the folder is empty.</summary>
    public static void EnsureExamples()
    {
        string dir = AppPaths.CustomMacros;
        if (Directory.EnumerateFiles(dir).Any()) return;

        try
        {
            File.WriteAllText(Path.Combine(dir, "Example_Simple.cs"), Simple);
            File.WriteAllText(Path.Combine(dir, "Example_Full.cs"), Full);
        }
        catch
        {
        }
    }

    /// <summary>Creates a new file from the template (if missing) and returns its path.</summary>
    public static string CreateIfMissing(string spec)
    {
        string path = CustomMacroLoader.ResolvePath(spec, out _);
        if (!File.Exists(path) && Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            string className = SanitizeClassName(Path.GetFileNameWithoutExtension(path));
            File.WriteAllText(path, Simple.Replace("MyMacro", className));
        }
        return path;
    }

    private static string SanitizeClassName(string name)
    {
        var chars = name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        string result = new string(chars);
        if (result.Length == 0 || char.IsDigit(result[0])) result = "Macro_" + result;
        return result;
    }
}
