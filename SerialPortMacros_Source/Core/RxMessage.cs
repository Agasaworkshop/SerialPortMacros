namespace SerialPortMacros.Core;

/// <summary>
/// Un messaggio ricevuto da una porta logica, già separato e marcato temporalmente.
/// </summary>
public readonly struct RxMessage
{
    /// <summary>Testo della riga (per i frame binari: la traduzione in testo dei valori).</summary>
    public readonly string Text;

    /// <summary>Nome della sorgente mostrato a video ("COM3", "TCP1", ...).</summary>
    public readonly string Source;

    /// <summary>Porta logica 1..4.</summary>
    public readonly int Port;

    /// <summary>Ora di sistema di ricezione (per log e testo).</summary>
    public readonly DateTime Wall;

    /// <summary>Orologio monotono condiviso dai grafici (<see cref="Form4.NowSeconds"/>).</summary>
    public readonly double Clock;

    /// <summary>
    /// Valori numerici già decodificati (frame binari). Se null i numeri vengono
    /// estratti dal testo con <see cref="NumberParser"/>.
    /// </summary>
    public readonly double[]? Values;

    public RxMessage(string text, string source, int port, DateTime wall, double clock, double[]? values)
    {
        Text = text;
        Source = source;
        Port = port;
        Wall = wall;
        Clock = clock;
        Values = values;
    }
}
