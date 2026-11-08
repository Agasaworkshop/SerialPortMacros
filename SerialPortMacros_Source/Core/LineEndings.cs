namespace SerialPortMacros.Core;

/// <summary>
/// Shared line-ending conversions (combo boxes, macros, sequences, Form7).
/// Indices: 0 = none, 1 = NL, 2 = CR, 3 = CR+NL.
/// </summary>
public static class LineEndings
{
    public static readonly string[] DisplayNames =
    {
        "No line ending", "New line", "Carriage return", "Both NL & CR"
    };

    public static readonly string[] ShortNames = { "None", "NL", "CR", "Both" };

    public static string FromIndex(int index, string fallback = "") => index switch
    {
        0 => "",
        1 => "\n",
        2 => "\r",
        3 => "\r\n",
        _ => fallback
    };

    /// <summary>Name used by Form7 ("New line", "Carriage return", "Both ...").</summary>
    public static string? FromName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (name.Contains("Both")) return "\r\n";
        if (name.Contains("New line")) return "\n";
        if (name.Contains("Carriage return")) return "\r";
        return "";
    }

    /// <summary>Tags used in sequences: {NL} {CR} {NLCR} {N}.</summary>
    public static string FromTag(string tag) => tag.ToUpperInvariant() switch
    {
        "NL" => "\n",
        "CR" => "\r",
        "NLCR" => "\r\n",
        _ => ""
    };
}
