using System.Collections.Concurrent;

// =====================================================================================
//  API PUBBLICA PER LE MACRO CUSTOM
//
//  Una macro custom è una classe C# che implementa ICustomMacro (oppure eredita da
//  SimpleMacro). Può stare in:
//    - un file .cs nella cartella "custom_macros": viene compilato automaticamente;
//    - una .dll compilata a parte (progetto Class Library che referenzia SerialPortMacros.dll).
//
//  Il codice viene chiamato dal thread di processing per ogni messaggio che passa il
//  filtro della macro (chiave + porte). Mai in parallelo con sé stesso.
//  Deve essere veloce: un'elaborazione lenta rallenta l'intera pipeline. Per lavori
//  lunghi o periodici usa un tuo thread/timer e IMacroContext.Send().
// =====================================================================================

namespace SerialPortMacros.Macros;

/// <summary>Il messaggio che ha attivato la macro.</summary>
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

    /// <summary>Porta logica di provenienza (1..4).</summary>
    public int Port { get; }

    /// <summary>Nome della sorgente ("COM3", "TCP1", ...).</summary>
    public string Source { get; }

    /// <summary>La stringa ricevuta (per i frame binari: la loro traduzione in testo).</summary>
    public string Message { get; }

    /// <summary>
    /// I numeri del messaggio: estratti dal testo oppure decodificati dal frame binario
    /// (nell'ordine dei campi definiti).
    /// </summary>
    public IReadOnlyList<double> Values { get; }

    /// <summary>Ora di ricezione.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Tempo monotono in secondi (stesso orologio dei grafici): utile per calcolare dt.</summary>
    public double Time { get; }
}

/// <summary>Un messaggio da inviare. Usa i metodi statici per crearlo.</summary>
public readonly struct MacroOutput
{
    /// <summary>Rispondi sulla stessa porta da cui è arrivato il messaggio.</summary>
    public const int SourcePort = 0;

    /// <summary>Invia a tutte e quattro le porte.</summary>
    public const int AllPorts = -1;

    private MacroOutput(int port, string? text, byte[]? data)
    {
        Port = port;
        Text = text;
        Data = data;
    }

    /// <summary>1..4, <see cref="SourcePort"/> o <see cref="AllPorts"/>.</summary>
    public int Port { get; }

    /// <summary>Testo da inviare (già comprensivo dell'eventuale fine riga).</summary>
    public string? Text { get; }

    /// <summary>Byte grezzi da inviare (in alternativa a <see cref="Text"/>).</summary>
    public byte[]? Data { get; }

    /// <summary>Testo seguito da un fine riga (default "\n").</summary>
    public static MacroOutput Line(int port, string text, string lineEnding = "\n") =>
        new(port, text + lineEnding, null);

    /// <summary>Testo inviato così com'è.</summary>
    public static MacroOutput Raw(int port, string text) => new(port, text, null);

    /// <summary>Byte grezzi (es. un frame binario per il micro).</summary>
    public static MacroOutput Bytes(int port, byte[] data) => new(port, null, data);

    /// <summary>Riga di risposta sulla porta di provenienza.</summary>
    public static MacroOutput Reply(string text, string lineEnding = "\n") => Line(SourcePort, text, lineEnding);
}

/// <summary>Servizi a disposizione della macro.</summary>
public interface IMacroContext
{
    /// <summary>Variabili condivise con le interfacce (Form7) e con le altre macro.</summary>
    ConcurrentDictionary<string, double> Variables { get; }

    /// <summary>Tempo monotono in secondi (stesso orologio di <see cref="MacroInput.Time"/>).</summary>
    double Now { get; }

    /// <summary>Invia una riga. Utilizzabile anche da thread/timer propri della macro.</summary>
    void Send(int port, string text, string lineEnding = "\n");

    /// <summary>Invia byte grezzi.</summary>
    void SendBytes(int port, byte[] data);

    /// <summary>Scrive un messaggio nella finestra principale.</summary>
    void Print(string text);

    /// <summary>Ultima riga ricevuta su una porta (1..4), o null.</summary>
    string? LatestMessage(int port);

    /// <summary>Ultimi numeri ricevuti su una porta (1..4), o null.</summary>
    IReadOnlyList<double>? LatestValues(int port);
}

/// <summary>Contratto completo di una macro custom.</summary>
public interface ICustomMacro
{
    /// <summary>Chiamato quando le macro vengono avviate (tasto Play) o ricaricate.</summary>
    void Start(IMacroContext context) { }

    /// <summary>
    /// Elabora un messaggio. Restituisci i messaggi da inviare, oppure null / vuoto
    /// per non inviare nulla. Puoi usare "yield return".
    /// </summary>
    IEnumerable<MacroOutput>? Process(MacroInput input);

    /// <summary>Chiamato quando le macro vengono fermate o ricaricate.</summary>
    void Stop() { }
}

/// <summary>
/// Versione semplificata: (porta, stringa) in ingresso → (porta, stringa) in uscita.
/// Restituisci null per non inviare nulla. La stringa di uscita riceve "\n" in coda.
/// </summary>
public abstract class SimpleMacro : ICustomMacro
{
    /// <summary>Disponibile da <see cref="OnStart"/> in poi.</summary>
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
    /// <paramref name="port"/>: porta di provenienza 1..4. Restituisci la porta di
    /// destinazione (1..4, 0 = stessa porta, -1 = tutte) e il messaggio, oppure null.
    /// </summary>
    public abstract (int Port, string Message)? OnMessage(int port, string message);
}
