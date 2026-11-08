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
                result.Add(new Script { Name = Path.GetFileName(path), FilePath = path, LoadError = ex.Message });
            }
        }

        return result.ToArray();
    }

    public static Script Load(string path)
    {
        string[] lines = File.ReadAllLines(path);
        var s = new Script { Name = Path.GetFileName(path), FilePath = path };

        string L(int i) => i < lines.Length ? lines[i].Trim() : "";
        bool B(int i) => bool.TryParse(L(i), out bool b) && b;

        if (lines.Length > 0) s.Key = lines[0];
        s.Effect = int.TryParse(L(1), out int effect) ? (MacroEffect)effect : MacroEffect.None;
        s.ListenPort1 = B(2);
        s.ListenPort2 = B(3);
        s.ListenPort3 = B(4);
        s.ListenPort4 = B(5);

        switch (s.Effect)
        {
            case MacroEffect.Keypress:
                s.KeyPress = lines.Length > 6 ? lines[6] : "";
                s.Active = B(7);
                break;

            case MacroEffect.Reply:
                s.Reply = lines.Length > 6 ? lines[6] : "";
                s.ReplyPort1 = B(7);
                s.ReplyPort2 = B(8);
                s.ReplyPort3 = B(9);
                s.ReplyPort4 = B(10);
                s.Active = B(11);
                s.LineEndingIndex = int.TryParse(L(12), out int le) ? le : 1; // old files: NL
                break;

            case MacroEffect.Custom:
                s.CustomFile = L(6);
                s.Active = B(7);
                break;
        }

        return s;
    }

    public static void Save(Script s)
    {
        var lines = new List<string>
        {
            s.Key ?? "",
            ((int)s.Effect).ToString(),   // stored as a number (1, 2, 3)
            s.ListenPort1.ToString(),
            s.ListenPort2.ToString(),
            s.ListenPort3.ToString(),
            s.ListenPort4.ToString()
        };

        switch (s.Effect)
        {
            case MacroEffect.Keypress:
                lines.Add(s.KeyPress ?? "");
                lines.Add(s.Active.ToString());
                break;

            case MacroEffect.Reply:
                lines.Add(s.Reply ?? "");
                lines.Add(s.ReplyPort1.ToString());
                lines.Add(s.ReplyPort2.ToString());
                lines.Add(s.ReplyPort3.ToString());
                lines.Add(s.ReplyPort4.ToString());
                lines.Add(s.Active.ToString());
                lines.Add(s.LineEndingIndex.ToString());
                break;

            case MacroEffect.Custom:
                lines.Add(s.CustomFile ?? "");
                lines.Add(s.Active.ToString());
                break;
        }

        File.WriteAllLines(s.FilePath, lines);
    }

    /// <summary>Creates a new example macro file. Returns false if it already exists.</summary>
    public static bool CreateNew(string fileName)
    {
        string path = Path.Combine(AppPaths.Scripts, fileName);
        if (File.Exists(path)) return false;

        Save(new Script
        {
            FilePath = path,
            Key = "test",
            Effect = MacroEffect.Keypress,
            KeyPress = "%100%!LMB"
        });
        return true;
    }
}
