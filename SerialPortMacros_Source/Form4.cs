using System.Collections.Concurrent;
using System.Diagnostics;
using ScottPlot;
using ScottPlot.Plottables;

namespace SerialPortMacros
{
    /// <summary>
    /// Real-time graph of one value (or, as "master", of several merged graphs).
    /// Data arrives from any thread through <see cref="AddDataPoint(double, double)"/>
    /// and is drawn by a ~30 fps UI timer.
    /// </summary>
    public partial class Form4 : Form
    {
        // Clock shared by ALL graphs (single and merged).
        // Points are stamped with this clock when the data arrives from the port,
        // not when the UI thread gets around to processing it: this is what removes
        // the "chunky" look (points bunched together).
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        public static double NowSeconds => Clock.Elapsed.TotalSeconds;

        // ---- State used by Form1 (merge handling) ----

        /// <summary>True for a merged graph that shows the series of other graphs.</summary>
        public bool IsMaster { get; }

        /// <summary>The merged graph this graph forwards its data to (null if not merged).</summary>
        public Form4? MasterForm { get; set; }

        /// <summary>Master only: the merged child graphs.</summary>
        public Dictionary<Form4, MergedChild> Children { get; } = new Dictionary<Form4, MergedChild>();

        /// <summary>If false the form does not queue its own data (hidden by a merge, or closed).</summary>
        public volatile bool AcceptsData = true;

        private readonly Form1 _mainForm;
        private bool _merging;
        private bool _playing = true;

        // ---- Private state ----
        private readonly ConcurrentQueue<(double t, double y)> _queue = new ConcurrentQueue<(double t, double y)>();
        private PlotSeries? _series;
        private System.Windows.Forms.Timer? _plotTimer;
        private double _timeWindow = 10;
        private bool _legendVisible = true;
        private bool _forceRefresh = true;

        public Form4(Form1 form1, bool masterFlag = false)
        {
            InitializeComponent();
            IsMaster = masterFlag;
            _mainForm = form1;
        }

        private void Form4_Load(object sender, EventArgs e)
        {
            if (IsMaster)
            {
                button2.Visible = true;
            }
            else
            {
                label2.Visible = false;
                button3.Visible = false;
            }

            // Axis limits are set manually on every frame.
            formsPlot1.Plot.Axes.ContinuouslyAutoscale = false;
            formsPlot1.Plot.Legend.IsVisible = true;

            if (IsMaster)
            {
                formsPlot1.Plot.ShowLegend(Children.Values.Select(c => c.Legend).ToArray());
                formsPlot1.Plot.Legend.Alignment = Alignment.LowerLeft;
            }
            else
            {
                _series = new PlotSeries(formsPlot1.Plot, ScottPlot.Color.FromColor(System.Drawing.Color.Blue));
            }

            _plotTimer = new System.Windows.Forms.Timer();
            _plotTimer.Interval = 33; // ~30 fps
            _plotTimer.Tick += PlotTimer_Tick;
            _plotTimer.Start();
        }

        // ------------------------------------------------------------------
        // Data input (thread-safe)
        // ------------------------------------------------------------------

        /// <summary>
        /// Thread-safe: can be called from any thread.
        /// <paramref name="t"/> = <see cref="NowSeconds"/> taken when the data was received.
        /// </summary>
        public void AddDataPoint(double value, double t)
        {
            if (!IsMaster && AcceptsData)
                _queue.Enqueue((t, value));

            MasterForm?.AddChildData(this, value, t);
        }

        public void AddDataPoint(double value) => AddDataPoint(value, NowSeconds);

        public void AddChildData(Form4 child, double y, double t)
        {
            if (!IsMaster) return;

            if (Children.TryGetValue(child, out var childEntry))
                childEntry.Queue.Enqueue((t, y));
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        private static bool Drain(ConcurrentQueue<(double t, double y)> queue, PlotSeries? series)
        {
            if (series == null) return false;

            bool any = false;
            int guard = 200000;
            while (guard-- > 0 && queue.TryDequeue(out var p))
            {
                series.Add(p.t, p.y);
                any = true;
            }
            return any;
        }

        private void PlotTimer_Tick(object? sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            double now = NowSeconds;
            bool updated = false;

            // The queues are always drained (even while paused) so they cannot grow
            // without limit; while paused the graph is simply not redrawn.
            if (IsMaster)
            {
                foreach (var childEntry in Children.Values)
                    updated |= Drain(childEntry.Queue, childEntry.Series);
            }
            else
            {
                updated = Drain(_queue, _series);
            }

            if (!_playing)
                return;

            if (!updated && !_forceRefresh)
                return;

            _forceRefresh = false;

            double xMin = now - _timeWindow;
            double yMin = double.PositiveInfinity;
            double yMax = double.NegativeInfinity;

            if (IsMaster)
            {
                foreach (var childEntry in Children.Values)
                {
                    childEntry.Series.TrimBefore(xMin);
                    childEntry.Series.ExpandRange(ref yMin, ref yMax);
                }
            }
            else if (_series != null)
            {
                _series.TrimBefore(xMin);
                _series.ExpandRange(ref yMin, ref yMax);
            }

            var plot = formsPlot1.Plot;
            plot.Axes.SetLimitsX(xMin, now);

            if (yMin <= yMax)
            {
                double span = yMax - yMin;
                double pad = span > 1e-12 ? span * 0.08 : Math.Max(Math.Abs(yMax) * 0.05, 1.0);
                plot.Axes.SetLimitsY(yMin - pad, yMax + pad);
            }

            formsPlot1.Refresh();
        }

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        // Merge with another graph
        private void button1_Click(object sender, EventArgs e)
        {
            if (!_merging)
            {
                button1.Image = Properties.Resources.SyncArrowStatusBar5_extra_16x;
                _merging = true;
                _mainForm.MergeGraph(this);
            }
            else
            {
                button1.Image = Properties.Resources.SyncArrowStatusBar5_16x;
                _merging = false;
                _mainForm.CancelMerge();
            }
        }

        // Master only: show the merged child graphs again
        private void button2_Click(object sender, EventArgs e)
        {
            if (!IsMaster) return;

            foreach (var childEntry in Children.Values)
            {
                try
                {
                    if (childEntry.Child != null)
                    {
                        childEntry.Child.Show();
                        childEntry.Child.AcceptsData = true;
                    }
                }
                catch
                {
                }
            }
        }

        // Show / hide the legend
        private void button3_Click(object sender, EventArgs e)
        {
            if (_legendVisible)
            {
                button3.Image = Properties.Resources.Not_Visible_16x;
                _legendVisible = false;
                formsPlot1.Plot.Legend.IsVisible = false;
                toolTip1.SetToolTip(button3, "Show");
            }
            else
            {
                button3.Image = Properties.Resources.Visible_16x;
                _legendVisible = true;
                formsPlot1.Plot.Legend.IsVisible = true;
                toolTip1.SetToolTip(button3, "Hide");
            }

            _forceRefresh = true;
        }

        // Play / pause
        private void button4_Click(object sender, EventArgs e)
        {
            if (_playing)
            {
                _playing = false;
                button4.Image = Properties.Resources.Play;
                toolTip1.SetToolTip(button4, "Play");
            }
            else
            {
                _playing = true;
                _forceRefresh = true;
                button4.Image = Properties.Resources.Pause;
                toolTip1.SetToolTip(button4, "Pause");
            }
        }

        // Time window [s]
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            _timeWindow = (double)numericUpDown1.Value;
            _forceRefresh = true;
        }

        public void ResetMerge()
        {
            _merging = false;
            button1.Image = Properties.Resources.SyncArrowStatusBar5_16x;
        }

        private void Form4_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop accepting data, so queues cannot grow forever.
            AcceptsData = false;

            try { _plotTimer?.Stop(); } catch { }

            if (!IsMaster) return;

            foreach (var childEntry in Children.Values)
            {
                try
                {
                    if (childEntry.Child != null)
                    {
                        // The child must no longer forward data to a closed master.
                        childEntry.Child.MasterForm = null;
                        childEntry.Child.AcceptsData = true;
                        childEntry.Child.Show();
                    }
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>
    /// Time series: two X/Y lists bound to a ScottPlot scatter line.
    /// Everything happens on the UI thread (the graph timer), so no locking is needed.
    /// </summary>
    public class PlotSeries
    {
        public readonly List<double> Xs = new List<double>(4096);
        public readonly List<double> Ys = new List<double>(4096);
        public readonly Scatter Line;

        public PlotSeries(Plot plot, ScottPlot.Color color)
        {
            Line = plot.Add.ScatterLine(Xs, Ys);
            Line.Color = color;
            Line.LineWidth = 1.5f;
        }

        public void Add(double x, double y)
        {
            Xs.Add(x);
            Ys.Add(y);
        }

        /// <summary>Removes, in one go, every point older than <paramref name="xMin"/>.</summary>
        public void TrimBefore(double xMin)
        {
            int n = Xs.Count;
            if (n == 0 || Xs[0] >= xMin) return;

            int lo = 0, hi = n;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Xs[mid] < xMin) lo = mid + 1; else hi = mid;
            }

            // Keep one point just outside the window so the line reaches the left edge.
            int remove = Math.Max(0, lo - 1);
            if (remove > 0)
            {
                Xs.RemoveRange(0, remove);
                Ys.RemoveRange(0, remove);
            }
        }

        public void ExpandRange(ref double yMin, ref double yMax)
        {
            for (int i = 0; i < Ys.Count; i++)
            {
                double y = Ys[i];
                if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }
        }
    }

    /// <summary>One child graph inside a merged ("master") graph.</summary>
    public class MergedChild
    {
        public ConcurrentQueue<(double t, double y)> Queue = new ConcurrentQueue<(double t, double y)>();
        public System.Drawing.Color Color;
        public Form4 Child;
        public PlotSeries Series;
        public LegendItem Legend;

        public MergedChild(Form4 child, System.Drawing.Color color, ScottPlot.WinForms.FormsPlot formsPlot)
        {
            Child = child;
            Color = color;

            var spColor = ScottPlot.Color.FromColor(color);
            Series = new PlotSeries(formsPlot.Plot, spColor);

            Legend = new LegendItem()
            {
                LineColor = spColor,
                MarkerFillColor = spColor,
                MarkerLineColor = spColor,
                MarkerShape = MarkerShape.Cross,
                LineWidth = 2,
                LabelText = child.Text
            };
        }
    }
}
