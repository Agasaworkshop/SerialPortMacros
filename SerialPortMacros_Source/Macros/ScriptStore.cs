using SerialPortMacros.Core;

namespace SerialPortMacros.Macros;

/// <summary>
/// Lettura/scrittura dei file macro (cartella "scripts"). Un valore per riga:
///
///   0  chiave ("*" = ogni messaggio, "===" = righe che iniziano con ===)
///   1  tipo: 1 = Keypress, 2 = Write back, 3 = Custom code
///   2-5  porte che attivano la macro (true/false per porta 1..4)
///
///   Keypress:     6 tasto/testo        7 attiva
///   Write back:   6 risposta  7-10 porte di destinazione  11 attiva  12 fine riga
///   Custom code:  6 file (.cs/.dll[:Classe])  7 attiva
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
                // Un file illeggibile non deve impedire di caricare gli altri.
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
                s.lineEndingIndex = int.TryParse(L(12), out int le) ? le : 1; // vecchi file: NL
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

    /// <summary>Crea un nuovo file macro di esempio. Restituisce false se esiste già.</summary>
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
