using SerialPortMacros.Core;

namespace SerialPortMacros.Macros;

/// <summary>File di esempio creati nella cartella custom_macros.</summary>
public static class CustomMacroTemplates
{
    public const string Simple = @"// Macro custom semplice: (porta, stringa) in ingresso -> (porta, stringa) in uscita.
// Il file viene compilato automaticamente quando premi Play.
//
// Nella finestra Macros scegli ""Custom code"" e scrivi il nome di questo file.
// La chiave e le porte della macro decidono quali messaggi arrivano qui
// (chiave ""*"" = tutti i messaggi delle porte selezionate).

public class MyMacro : SimpleMacro
{
    protected override void OnStart()
    {
        Context.Print(""MyMacro avviata"");
    }

    // port: porta di provenienza (1..4). Restituisci (porta di destinazione, messaggio)
    // oppure null per non inviare nulla. Porta 0 = stessa porta, -1 = tutte.
    public override (int Port, string Message)? OnMessage(int port, string message)
    {
        if (message.StartsWith(""PING""))
            return (port, ""PONG"");

        return null;
    }
}";

    public const string Full = @"// Macro custom completa (ICustomMacro).
//
//  input.Port     porta di provenienza 1..4
//  input.Message  la stringa ricevuta (per i frame binari: la traduzione in testo)
//  input.Values   i numeri del messaggio (estratti dal testo o decodificati dal frame binario)
//  input.Time     tempo in secondi (monotono, utile per calcolare dt)
//
//  Restituisci zero, uno o più MacroOutput:
//    MacroOutput.Line(2, ""testo"")          riga + ""\n"" sulla porta 2
//    MacroOutput.Raw(1, ""testo"")           testo così com'è
//    MacroOutput.Bytes(1, new byte[]{..})  byte grezzi
//    MacroOutput.Reply(""ok"")               riga sulla porta di provenienza
//
//  Context (passato a Start):
//    Context.Variables[""nome""]   variabili condivise con le interfacce (Form7)
//    Context.Send(porta, testo)  invio da un tuo timer/thread
//    Context.Print(testo)        scrive nella finestra principale
//
//  Process viene chiamato sul thread di elaborazione, mai in parallelo con sé stesso:
//  tienilo veloce. Lo stato dei campi resta fra un messaggio e l'altro
//  (viene azzerato quando la macro viene ricaricata).

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

        // Esempio: rimanda il primo valore raddoppiato alla porta 2.
        yield return MacroOutput.Line(2, (value * 2).ToString(CultureInfo.InvariantCulture));
    }

    public void Stop()
    {
    }
}";

    /// <summary>Crea gli esempi se la cartella è vuota.</summary>
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

    /// <summary>Crea un nuovo file da template (se non esiste) e ne restituisce il percorso.</summary>
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
