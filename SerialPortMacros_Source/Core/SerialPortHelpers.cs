using System.IO.Ports;

namespace SerialPortMacros.Core;

/// <summary>Conversioni testo &lt;-&gt; impostazioni seriali (usate da settings e Form2).</summary>
public static class SerialPortHelpers
{
    public static readonly string[] ParityNames = { "Odd", "Even", "None" };
    public static readonly string[] StopBitsNames = { "None", "One", "OnePointFive", "Two" };

    public static string ToText(Parity parity) => parity switch
    {
        Parity.Odd => "Odd",
        Parity.Even => "Even",
        _ => "None"
    };

    public static string ToText(StopBits stopBits) => stopBits switch
    {
        StopBits.One => "One",
        StopBits.Two => "Two",
        StopBits.OnePointFive => "OnePointFive",
        _ => "None"
    };

    public static Parity ParseParity(string? value) => value switch
    {
        "Odd" => Parity.Odd,
        "Even" => Parity.Even,
        _ => Parity.None
    };

    public static StopBits ParseStopBits(string? value) => value switch
    {
        "None" => StopBits.None,
        "OnePointFive" => StopBits.OnePointFive,
        "Two" => StopBits.Two,
        _ => StopBits.One
    };

    /// <summary>Imposta i parametri di default usati all'avvio.</summary>
    public static void ApplyDefaults(SerialPort port)
    {
        port.BaudRate = 9600;
        port.Encoding = System.Text.Encoding.ASCII;
        port.NewLine = "\n";
        port.Parity = Parity.None;
        port.DataBits = 8;
        port.StopBits = StopBits.One;
    }
}
