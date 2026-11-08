using SerialPortMacros.Core;

namespace SerialPortMacros.Macros;

/// <summary>
/// Reads/writes the macro files ("scripts" folder). One value per line:
///
///   0  key ("*" = every message, "===" = lines starting with ===)
///   1  type: 1 = Keypress, 2 = Write back, 3 = Custom code
///   2-5  ports that trigger the macro (true/false for ports 1..4)
///
///   Keypress:     6 key/text           7 active
///   Write back:   6 reply  7-10 destination ports  11 active  12 line ending
///   Custom code:  6 file (.cs/.dll[:Class])  7 active
/// </summary>
public static class ScriptStore
{
    public static Script[] LoadAll()
    {
        string dir = AppPaths.Scripts;
        var result = new List<Script>();

        foreach (string path in Directory.GetFiles(dir).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                result.Add(Load(path));
            }
            catch (Exception ex)
            {
                // An unreadable file must not prevent loading the others.
                result.Add(new Script { name = Path.GetFileName(path), path = path, LoadError = ex.Message });
            }
        }

        return result.ToArray();
    }

    public static Script Load(string path)
    {
        string[] lines = File.ReadAllLines(path);
        var s = new Script { name = Path.GetFileName(path), path = path };

        string L(int i) => i < lines.Length ? lines[i].Trim() : "";
        bool B(int i) => bool.TryParse(L(i), out bool b) && b;

        if (lines.Length > 0) s.key = lines[0];
        int.TryParse(L(1), out s.trigger);
        s.p1 = B(2);
        s.p2 = B(3);
        s.p3 = B(4);
        s.p4 = B(5);

        switch (s.trigger)
        {
            case Script.TriggerKeypress:
                s.keypress = lines.Length > 6 ? lines[6] : "";
                s.active = B(7);
                break;

            case Script.TriggerReply:
                s.reply = lines.Length > 6 ? lines[6] : "";
                s.p1_w = B(7);
                s.p2_w = B(8);
                s.p3_w = B(9);
                s.p4_w = B(10);
                s.active = B(11);
                s.lineEndingIndex = int.TryParse(L(12), out int le) ? le : 1; // old files: NL
                break;

            case Script.TriggerCustom:
                s.customFile = L(6);
                s.active = B(7);
                break;
        }

        return s;
    }

    public static void Save(Script s)
    {
        var lines = new List<string>
        {
            s.key ?? "",
            s.trigger.ToString(),
            s.p1.ToString(),
            s.p2.ToString(),
            s.p3.ToString(),
            s.p4.ToString()
        };

        switch (s.trigger)
        {
            case Script.TriggerKeypress:
                lines.Add(s.keypress ?? "");
                lines.Add(s.active.ToString());
                break;

            case Script.TriggerReply:
                lines.Add(s.reply ?? "");
                lines.Add(s.p1_w.ToString());
                lines.Add(s.p2_w.ToString());
                lines.Add(s.p3_w.ToString());
                lines.Add(s.p4_w.ToString());
                lines.Add(s.active.ToString());
                lines.Add(s.lineEndingIndex.ToString());
                break;

            case Script.TriggerCustom:
                lines.Add(s.customFile ?? "");
                lines.Add(s.active.ToString());
                break;
        }

        File.WriteAllLines(s.path, lines);
    }

    /// <summary>Creates a new example macro file. Returns false if it already exists.</summary>
    public static bool CreateNew(string fileName)
    {
        string path = Path.Combine(AppPaths.Scripts, fileName);
        if (File.Exists(path)) return false;

        Save(new Script
        {
            path = path,
            key = "test",
            trigger = Script.TriggerKeypress,
            keypress = "%100%!LMB"
        });
        return true;
    }
}
