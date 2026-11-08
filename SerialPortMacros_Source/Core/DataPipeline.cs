using System.Collections.Concurrent;
using System.Diagnostics;
using SerialPortMacros.Decoding;
using SerialPortMacros.Macros;

namespace SerialPortMacros.Core;

/// <summary>
/// Receive pipeline.
///
///   serial / TCP thread  ──Enqueue──►  queue  ──►  processing thread (above-normal priority)
///                                                   1. live values (Form7, macros)
///                                                   2. log files
///                                                   3. DisplayBuffer  ──►  UI (timer, decimated)
///                                                   4. macros
///                                                   5. graphs
///
/// Processing never depends on the UI: if the window slows down, logging and macros
/// still receive every single message; only the on-screen text is decimated.
/// </summary>
public sealed class DataPipeline : IDisposable
{
    private readonly BlockingCollection<RxMessage> _queue = new(new ConcurrentQueue<RxMessage>());
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();

    private readonly LogManager _log;
    private readonly DisplayBuffer _display;
    private readonly LiveData _live;
    private readonly GraphRouter _graphs;
    private readonly MacroEngine _macros;

    // Last arrival time per logical port (indices 1..4), used to spread timestamps.
    private readonly double[] _lastRxClock = new double[AppSettings.PortCount + 1];
    private readonly object _enqueueLock = new();

    private long _lastMessageTick;
    private long _processed;

    /// <summary>Show timestamps in the on-screen text.</summary>
    public volatile bool TimeStamps = true;

    public DataPipeline(LogManager log, DisplayBuffer display, LiveData live, GraphRouter graphs, MacroEngine macros)
    {
        _log = log;
        _display = display;
        _live = live;
        _graphs = graphs;
        _macros = macros;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "RX processing",
            Priority = ThreadPriority.AboveNormal
        };
    }

    public void Start() => _thread.Start();

    /// <summary>Environment.TickCount64 of the last received message.</summary>
    public long LastMessageTick => Interlocked.Read(ref _lastMessageTick);

    public long ProcessedCount => Interlocked.Read(ref _processed);
    public int PendingCount => _queue.Count;

    /// <summary>
    /// Queues the frames that arrived in the same packet (a single serial/TCP read).
    /// If they all had the same timestamp the plots would show "steps", so they are
    /// spread evenly between the arrival of the previous packet and the current one.
    /// Thread-safe.
    /// </summary>
    public void EnqueueBatch(List<DecodedFrame> frames, string source, int logicalPort)
    {
        int n = frames.Count;
        if (n == 0) return;

        Interlocked.Exchange(ref _lastMessageTick, Environment.TickCount64);

        double now = Form4.NowSeconds;
        DateTime wallNow = DateTime.Now;
        double span;

        lock (_enqueueLock)
        {
            double prev = _lastRxClock[logicalPort];
            // After a silence it is unknown when the lines were produced: limit the interval.
            span = n > 1 ? Math.Min(Math.Max(now - prev, 0.0), 0.25) : 0.0;
            _lastRxClock[logicalPort] = now;
        }

        for (int i = 0; i < n; i++)
        {
            double back = span * (n - 1 - i) / n; // the last line gets exactly the arrival time
            var f = frames[i];
            _queue.Add(new RxMessage(f.Text, source, logicalPort, wallNow.AddSeconds(-back), now - back, f.Values));
        }
    }

    private void Run()
    {
        var token = _cts.Token;
        long lastFlush = Environment.TickCount64;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_queue.TryTake(out RxMessage m, 100, token))
                {
                    Process(m);
                    // Drain everything already queued before waiting again.
                    while (_queue.TryTake(out m))
                        Process(m);
                }

                long tick = Environment.TickCount64;
                if (tick - lastFlush >= 500)
                {
                    lastFlush = tick;
                    _log.Flush();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _log.Flush();
        }
    }

    private void Process(in RxMessage m)
    {
        try
        {
            List<double> numbers = m.Values != null ? new List<double>(m.Values) : NumberParser.Parse(m.Text);

            _live.Update(m, numbers);

            if (_log.IsActive)
                _log.WriteReceived(m.Port, m.Source, m.Text, m.Wall);

            // Only an enqueue (no UI work). Done before the macros so the received
            // line appears on screen before any reply it triggers.
            _display.Add(FormatLine(m.Text, m.Source, m.Wall), ColorUtils.PortColor(m.Port));

            _macros.Process(m, numbers);

            _graphs.Route(m.Source, numbers, m.Clock);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Pipeline: " + ex);
        }
        finally
        {
            Interlocked.Increment(ref _processed);
        }
    }

    public string FormatLine(string text, string user, DateTime when) =>
        TimeStamps ? $"{when:HH:mm:ss.fff} | {user}: {text}" : $"{user}: {text}";

    public void Dispose()
    {
        _cts.Cancel();
        if (_thread.IsAlive)
            _thread.Join(1000);
        _cts.Dispose();
    }
}
