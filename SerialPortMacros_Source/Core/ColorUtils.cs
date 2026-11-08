namespace SerialPortMacros.Core;

public static class ColorUtils
{
    /// <summary>Text color of each logical port (1..4).</summary>
    public static Color PortColor(int logicalPort) => logicalPort switch
    {
        1 => Color.Red,
        2 => Color.Blue,
        3 => Color.Orange,
        4 => Color.Green,
        _ => Color.Black
    };

    public static Color FromHsv(double hue, double saturation, double value)
    {
        int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        double f = hue / 60 - Math.Floor(hue / 60);

        value *= 255;
        int v = Convert.ToInt32(value);
        int p = Convert.ToInt32(value * (1 - saturation));
        int q = Convert.ToInt32(value * (1 - f * saturation));
        int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

        return hi switch
        {
            0 => Color.FromArgb(255, v, t, p),
            1 => Color.FromArgb(255, q, v, p),
            2 => Color.FromArgb(255, p, v, t),
            3 => Color.FromArgb(255, p, q, v),
            4 => Color.FromArgb(255, t, p, v),
            _ => Color.FromArgb(255, v, p, q)
        };
    }

    /// <summary>Easily distinguishable colors, evenly spread over the hue spectrum.</summary>
    public static Color Distinct(int index, int total)
    {
        double hue = 360.0 / Math.Max(total, 1) * index;
        return FromHsv(hue, 0.7, 0.9);
    }
}
