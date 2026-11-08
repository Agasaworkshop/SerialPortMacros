using System.Collections.Concurrent;
using SerialPortMacros.Core;

namespace SerialPortMacros.Macros;

/// <summary>
/// Runs the macros on received messages. <see cref="Process"/> is called by the
/// processing thread; <see cref="SetScripts"/> and <see cref="Running"/> by the UI.
/// </summary>
public sealed class MacroEngine
{
    private static readonly Color ReplyColor = Color.Brown;
    private static readonly Color CustomColor = Color.DarkMagenta;
    private static readonly Color ErrorColor = Color.Red;

    private PortHub? _hub;
    private volatile Script[] _scripts = Array.Empty<Script>();
    private volatile bool _running;
    private MacroContext? _context;

    public void Attach(PortHub hub)
    {
        _hub = hub;
        _context = new MacroContext(hub);
    }

    public Script[] Scripts => _scripts;

    /// <summary>Starts/stops macro execution (Play button).</summary>
    public bool Running
    {
        get => _running;
        set
        {
            if (_running == value) return;
            if (value)
            {
                StartCustom(_scripts);
                _running = true;
            }
            else
            {
                _running = false;
                StopCustom(_scripts);
            }
        }
    }

    /// <summary>
    /// Replaces the macro list (after re-reading the files) and loads the custom
    /// macros. Returns the load errors, one per entry.
    /// </summary>
    public List<string> SetScripts(Script[] scripts)
    {
        var errors = new List<string>();

        foreach (var s in scripts)
        {
            if (s.LoadError != null)
            {
                errors.Add($"{s.name}: {s.LoadError}");
                continue;
            }
            if (s.trigger != Script.TriggerCustom || !s.active)
                continue;

            try
            {
                s.CustomInstance = CustomMacroLoader.CreateInstance(s.customFile);
            }
            catch (Exception ex)
            {
                s.LoadError = ex.Message;
                errors.Add($"{s.name}: {ex.Message}");
            }
        }

        var old = _scripts;
        if (_running)
        {
            StopCustom(old);
            StartCustom(scripts);
        }
        _scripts = scripts;

        return errors;
    }

    /// <summary>Processing thread.</summary>
    public void Process(in RxMessage m, List<double> numbers)
    {
        if (!_running || _hub == null) return;

        foreach (var s in _scripts)
        {
            if (!s.active || !s.ListensTo(m.Port) || !s.Matches(m.Text))
                continue;

            switch (s.trigger)
            {
                case Script.TriggerKeypress:
                    KeyPressAction.Run(s.keypress, m.Text);
                    break;

                case Script.TriggerReply:
                    _hub.Show(s.reply, s.name, ReplyColor);
                    _hub.SendTo(s.ReplyTargets, s.reply + s.LineEnding);
                    break;

                case Script.TriggerCustom:
                    RunCustom(s, m, numbers);
                    break;
            }
        }
    }

    private void RunCustom(Script s, in RxMessage m, List<double> numbers)
    {
        var macro = s.CustomInstance;
        if (macro == null || !s.Started) return;

        var input = new MacroInput(m.Port, m.Source, m.Text, numbers, m.Wall, m.Clock);
        List<MacroOutput>? outputs = null;

        try
        {
            lock (s) // the same instance never runs in parallel with Start/Stop
            {
                var result = macro.Process(input);
                if (result != null)
                    outputs = result.ToList(); // materialize (yield return) inside the try
            }
        }
        catch (Exception ex)
        {
            ReportError(s, ex);
            return;
        }

        if (outputs == null) return;

        foreach (var o in outputs)
            Emit(s.name, o, m.Port);
    }

    private void Emit(string user, MacroOutput o, int sourcePort)
    {
        if (_hub == null) return;

        int first, last;
        if (o.Port == MacroOutput.AllPorts) { first = 1; last = AppSettings.PortCount; }
        else
        {
            int p = o.Port == MacroOutput.SourcePort ? sourcePort : o.Port;
            if (p < 1 || p > AppSettings.PortCount) return;
            first = last = p;
        }

        for (int p = first; p <= last; p++)
        {
            if (o.Data != null) _hub.Send(p, o.Data);
            else if (o.Text != null) _hub.Send(p, o.Text);
        }

        string shown = o.Data != null
            ? "[bin] " + Convert.ToHexString(o.Data)
            : (o.Text ?? "").TrimEnd('\r', '\n');
        string target = o.Port == MacroOutput.AllPorts ? "all" : $"P{first}";
        _hub.Show(shown, $"{user} → {target}", CustomColor);
    }

    private void ReportError(Script s, Exception ex)
    {
        // At most one message every 2 seconds per macro, so the text is not flooded.
        long now = Environment.TickCount64;
        if (now - s.LastErrorTick < 2000) return;
        s.LastErrorTick = now;
        _hub?.Show($"{ex.GetType().Name}: {ex.Message}", $"{s.name} (error)", ErrorColor);
    }

    private void StartCustom(Script[] scripts)
    {
        if (_context == null) return;
        foreach (var s in scripts)
        {
            if (s.CustomInstance == null || s.Started) continue;
            try
            {
                lock (s)
                {
                    s.CustomInstance.Start(_context);
                    s.Started = true;
                }
            }
            catch (Exception ex)
            {
                s.LastErrorTick = 0;
                ReportError(s, ex);
            }
        }
    }

    private void StopCustom(Script[] scripts)
    {
        foreach (var s in scripts)
        {
            if (s.CustomInstance == null || !s.Started) continue;
            try
            {
                lock (s)
                {
                    s.Started = false;
                    s.CustomInstance.Stop();
                }
            }
            catch (Exception ex)
            {
                s.LastErrorTick = 0;
                ReportError(s, ex);
            }
        }
    }

    /// <summary><see cref="IMacroContext"/> implementation passed to the macros.</summary>
    private sealed class MacroContext : IMacroContext
    {
        private readonly PortHub _hub;

        public MacroContext(PortHub hub) => _hub = hub;

        public ConcurrentDictionary<string, double> Variables => _hub.Live.Variables;

        public double Now => Form4.NowSeconds;

        public void Send(int port, string text, string lineEnding = "\n") => SendRaw(port, text + lineEnding, null);

        public void SendBytes(int port, byte[] data) => SendRaw(port, null, data);

        private void SendRaw(int port, string? text, byte[]? data)
        {
            if (port == MacroOutput.AllPorts)
            {
                for (int p = 1; p <= AppSettings.PortCount; p++) SendOne(p, text, data);
            }
            else SendOne(port, text, data);
        }

        private void SendOne(int port, string? text, byte[]? data)
        {
            if (data != null) _hub.Send(port, data);
            else if (text != null) _hub.Send(port, text);
        }

        public void Print(string text) => _hub.Show(text, "Macro", CustomColor);

        public string? LatestMessage(int port) =>
            _hub.Live.LatestText.TryGetValue($"port{port}", out var s) ? s : null;

        public IReadOnlyList<double>? LatestValues(int port) =>
            _hub.Live.LatestNumbers.TryGetValue(port, out var v) ? v : null;
    }
}
