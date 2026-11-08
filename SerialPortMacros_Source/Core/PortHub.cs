using SerialPortMacros.Transport;

namespace SerialPortMacros.Core;

/// <summary>
/// Punto di accesso unico (thread-safe) alle 4 porte logiche e ai servizi condivisi:
/// usato da Form1, dalle macro, dalle sequenze e dalle interfacce.
/// </summary>
public sealed class PortHub
{
    public PortHub(DataPipeline pipeline, LogManager log, DisplayBuffer display, LiveData live)
    {
        Pipeline = pipeline;
        Log = log;
        Display = display;
        Live = live;

        Channels = new PortChannel[AppSettings.PortCount];
        for (int i = 0; i < Channels.Length; i++)
            Channels[i] = new PortChannel(i + 1, pipeline);
    }

    public DataPipeline Pipeline { get; }
    public LogManager Log { get; }
    public DisplayBuffer Display { get; }
    public LiveData Live { get; }
    public PortChannel[] Channels { get; }

    /// <summary>Porta logica 1..4.</summary>
    public PortChannel this[int logicalPort] => Channels[logicalPort - 1];

    public bool Send(int logicalPort, string text) =>
        logicalPort is >= 1 and <= AppSettings.PortCount && this[logicalPort].Send(text);

    public bool Send(int logicalPort, byte[] data) =>
        logicalPort is >= 1 and <= AppSettings.PortCount && this[logicalPort].Send(data);

    /// <summary>Invia alle porte con targets[i] == true (indice 0 = porta 1).</summary>
    public void SendTo(bool[] targets, string text)
    {
        for (int i = 0; i < Channels.Length && i < targets.Length; i++)
            if (targets[i])
                Channels[i].Send(text);
    }

    /// <summary>Aggiunge una riga al testo a video (da qualunque thread).</summary>
    public void Show(string text, string user, Color color) =>
        Display.Add(Pipeline.FormatLine(text, user, DateTime.Now), color);
}
