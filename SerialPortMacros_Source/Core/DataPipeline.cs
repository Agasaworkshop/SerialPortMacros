using System.Collections.Concurrent;
using System.Diagnostics;
using SerialPortMacros.Decoding;
using SerialPortMacros.Macros;

namespace SerialPortMacros.Core;

/// <summary>
/// Pipeline di ricezione.
///
///   thread seriale / TCP  ──Enqueue──►  coda  ──►  thread di processing (priorità alta)
///                                                   1. valori live (Form7, macro)
///                                                   2. log su file
///                                                   3. macro
///                                                   4. grafici
///                                                   5. DisplayBuffer  ──►  UI (timer, decimata)
///
/// Il processing non dipende mai dalla UI: se l'interfaccia rallenta, log e macro
/// continuano a ricevere ogni singolo messaggio; è solo il testo a video a essere decimato.
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

    // Ultimo arrivo per porta logica (indici 1..4), usato per distribuire i timestamp.
    private readonly double[] _lastRxClock = new double[AppSettings.PortCount + 1];
    private readonly object _enqueueLock = new();

    private long _lastMessageTick;
    private long _processed;

    /// <summary>Timestamp nel testo a video.</summary>
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

    /// <summary>Environment.TickCount64 dell'ultimo messaggio ricevuto.</summary>
    public long LastMessageTick => Interlocked.Read(ref _lastMessageTick);

    public long ProcessedCount => Interlocked.Read(ref _processed);
    public int PendingCount => _queue.Count;

    /// <summary>
    /// Accoda i frame arrivati nello stesso pacchetto (una singola lettura seriale/TCP).
    /// Se avessero tutti lo stesso timestamp il grafico mostrerebbe "gradini": vengono
    /// quindi distribuiti uniformemente fra l'arrivo del pacchetto precedente e quello attuale.
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
            // Dopo un silenzio non si sa quando le righe sono state generate: limita l'intervallo.
            span = n > 1 ? Math.Min(Math.Max(now - prev, 0.0), 0.25) : 0.0;
            _lastRxClock[logicalPort] = now;
        }

        for (int i = 0; i < n; i++)
        {
            double back = span * (n - 1 - i) / n; // l'ultima riga ha esattamente l'ora di arrivo
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
                    // Svuota tutto quello che è già in coda prima di tornare ad attendere.
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

            // Solo un accodamento (nessun lavoro UI): va prima delle macro così la
            // riga ricevuta appare a video prima delle eventuali risposte.
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
