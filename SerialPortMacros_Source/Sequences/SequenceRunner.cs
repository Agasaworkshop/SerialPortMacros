using System.Diagnostics;
using System.Text.RegularExpressions;
using SerialPortMacros.Core;

namespace SerialPortMacros.Sequences;

/// <summary>
/// Runs a sequence file, one command per line:
///
///   {wait(500)}                 waits 500 ms
///   {1,3}message{NL}            sends to ports 1 and 3 with line ending NL / CR / NLCR / N
/// </summary>
public sealed class SequenceRunner
{
    private static readonly Regex WaitRegex = new(@"^\{wait\((\d+)\)\}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CommandRegex = new(@"^\{([^}]+)\}(.*?)\s*\{(NLCR|NL|CR|N)\}\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ManualResetEventSlim _resume = new(true);

    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }

    /// <summary>Raised for each command (ports 1..4, message with line ending), from a background thread.</summary>
    public event Action<int[], string>? Command;

    public void Pause()
    {
        IsPaused = true;
        _resume.Reset();
    }

    public void Resume()
    {
        IsPaused = false;
        _resume.Set();
    }

    public async Task RunAsync(string filePath)
    {
        IsRunning = true;
        Resume();
        try
        {
            await Task.Run(() => Execute(filePath));
        }
        finally
        {
            IsRunning = false;
            Resume();
        }
    }

    private void Execute(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);

        for (int i = 0; i < lines.Length; i++)
        {
            _resume.Wait();

            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            var wait = WaitRegex.Match(line);
            if (wait.Success)
            {
                Delay(int.Parse(wait.Groups[1].Value));
                continue;
            }

            var cmd = CommandRegex.Match(line);
            if (!cmd.Success)
            {
                Debug.WriteLine($"Invalid sequence line {i + 1}: {line}");
                continue;
            }

            string message = cmd.Groups[2].Value.TrimEnd() + LineEndings.FromTag(cmd.Groups[3].Value);
            int[] ports = cmd.Groups[1].Value
                .Split(',')
                .Select(p => int.TryParse(p.Trim(), out int n) ? n : 0)
                .Where(n => n is >= 1 and <= AppSettings.PortCount)
                .ToArray();

            _resume.Wait();
            Command?.Invoke(ports, message);
        }
    }

    /// <summary>Delay that stops while paused (only time spent not paused counts).</summary>
    private void Delay(int milliseconds)
    {
        int remaining = milliseconds;
        while (remaining > 0)
        {
            _resume.Wait();
            var sw = Stopwatch.StartNew();
            Thread.Sleep(Math.Min(remaining, 10));
            remaining -= (int)sw.ElapsedMilliseconds;
        }
    }
}
