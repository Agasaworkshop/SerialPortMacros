using System.Globalization;
using System.Text.RegularExpressions;

namespace SerialPortMacros.Core;

/// <summary>
/// Reads a log file and extracts the numeric data of the most common line format
/// (the number of values per line that occurs most often). Used by the log viewer.
/// </summary>
public class LogParser
{
    /// <summary>Data[element][sample].</summary>
    public double[][] Data { get; private set; }

    public int ElementCount { get; private set; }

    public int SampleCount { get; private set; }

    /// <summary>
    /// Parses the file. Returns false if no format occurs at least
    /// <paramref name="minimumOccurrences"/> times.
    /// </summary>
    public bool Parse(string filePath, int minimumOccurrences = 5)
    {
        if (!File.Exists(filePath))
            return false;

        string[] lines = File.ReadAllLines(filePath);

        // First pass: count how many lines contain N values.
        var occurrences = new Dictionary<int, int>();

        foreach (string line in lines)
        {
            List<double> numbers = ParseDataLine(line);
            if (numbers.Count == 0)
                continue;

            int count = numbers.Count;
            if (!occurrences.ContainsKey(count))
                occurrences[count] = 0;

            occurrences[count]++;
        }

        // Find the most frequent format above the minimum threshold.
        int selectedElementCount = -1;
        int maxOccurrences = 0;

        foreach (var pair in occurrences)
        {
            if (pair.Value >= minimumOccurrences && pair.Value > maxOccurrences)
            {
                selectedElementCount = pair.Key;
                maxOccurrences = pair.Value;
            }
        }

        // No format is frequent enough.
        if (selectedElementCount == -1)
        {
            Data = null;
            ElementCount = 0;
            SampleCount = 0;
            return false;
        }

        // Second pass: extract every matching line.
        var samples = new List<List<double>>();

        foreach (string line in lines)
        {
            List<double> numbers = ParseDataLine(line);
            if (numbers.Count == selectedElementCount)
                samples.Add(numbers);
        }

        if (samples.Count == 0)
            return false;

        // Build the matrix.
        ElementCount = selectedElementCount;
        SampleCount = samples.Count;
        Data = new double[ElementCount][];

        for (int element = 0; element < ElementCount; element++)
        {
            Data[element] = new double[SampleCount];
            for (int sample = 0; sample < SampleCount; sample++)
                Data[element][sample] = samples[sample][element];
        }

        return true;
    }

    // ------------------------------------------------------------------
    // Single line
    // ------------------------------------------------------------------

    private List<double> ParseDataLine(string line)
    {
        line = RemoveTimestamp(line);
        return ParseNumbers(line);
    }

    /// <summary>Removes a leading "HH:mm:ss[.fff] | " timestamp.</summary>
    private string RemoveTimestamp(string line)
    {
        Match match = Regex.Match(line, @"^\s*\d{2}:\d{2}:\d{2}(?:\.\d+)?\s*\|\s*");

        if (match.Success)
            return line.Substring(match.Length);

        return line;
    }

    /// <summary>
    /// Extracts the numbers of a line. Unlike <see cref="NumberParser"/> it also accepts
    /// a leading '+' sign.
    /// </summary>
    public static List<double> ParseNumbers(string s)
    {
        var numbers = new List<double>();
        int i = 0;

        while (i < s.Length)
        {
            // Find the start of a number
            while (i < s.Length && !char.IsDigit(s[i]) && s[i] != '-' && s[i] != '+')
                i++;

            if (i >= s.Length)
                break;

            int start = i;

            // Sign
            if (s[i] == '-' || s[i] == '+')
                i++;

            bool hasDigit = false;
            bool hasDot = false;

            while (i < s.Length)
            {
                char c = s[i];

                if (char.IsDigit(c))
                {
                    hasDigit = true;
                    i++;
                }
                else if (c == '.' && !hasDot)
                {
                    hasDot = true;
                    i++;
                }
                else
                {
                    break;
                }
            }

            if (hasDigit)
            {
                string token = s.Substring(start, i - start);

                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    numbers.Add(value);
            }
        }

        return numbers;
    }
}
