using SerialPortMacros.Transport;

namespace SerialPortMacros.Core;

/// <summary>
/// Single thread-safe access point to the 4 logical ports and the shared services,
/// used by Form1, macros, sequences and interfaces.
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

    /// <summary>Logical port 1..4.</summary>
    public PortChannel this[int logicalPort] => Channels[logicalPort - 1];

    public bool Send(int logicalPort, string text) =>
        logicalPort is >= 1 and <= AppSettings.PortCount && this[logicalPort].Send(text);

    public bool Send(int logicalPort, byte[] data) =>
        logicalPort is >= 1 and <= AppSettings.PortCount && this[logicalPort].Send(data);

    /// <summary>Sends to every port with targets[i] == true (index 0 = port 1).</summary>
    public void SendTo(bool[] targets, string text)
    {
        for (int i = 0; i < Channels.Length && i < targets.Length; i++)
            if (targets[i])
                Channels[i].Send(text);
    }

    /// <summary>Adds a line to the on-screen text (from any thread).</summary>
    public void Show(string text, string user, Color color) =>
        Display.Add(Pipeline.FormatLine(text, user, DateTime.Now), color);
}
