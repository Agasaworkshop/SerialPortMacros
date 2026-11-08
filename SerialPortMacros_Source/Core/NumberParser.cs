using System.Globalization;

namespace SerialPortMacros.Core;

/// <summary>
/// Extracts every number found in a line of text ("T=12.5 H=-3" -> [12.5, -3]).
/// Same rules as the original Form1 version, without allocating substrings.
/// </summary>
public static class NumberParser
{
    public static List<double> Parse(string s)
    {
        var numbers = new List<double>();
        Parse(s, numbers);
        return numbers;
    }

    public static void Parse(string s, List<double> numbers)
    {
        int i = 0;
        ReadOnlySpan<char> span = s;

        while (i < span.Length)
        {
            while (i < span.Length && !char.IsDigit(span[i]) && span[i] != '-' && span[i] != '.')
                i++;

            int start = i;
            int dotCount = 0;

            while (i < span.Length && (char.IsDigit(span[i]) || span[i] == '.' || span[i] == '-'))
            {
                if (span[i] == '.')
                {
                    dotCount++;
                    if (dotCount > 1) break;
                }
                i++;
            }

            if (start < i &&
                double.TryParse(span.Slice(start, i - start), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double val))
            {
                numbers.Add(val);
            }
        }
    }
}
