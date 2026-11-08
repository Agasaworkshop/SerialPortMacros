using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using SerialPortMacros;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SerialPortMacros
{
    public partial class Form4 : Form
    {
        // Clock condiviso da TUTTI i grafici (singoli e merged).
        // I punti vengono marcati con questo orologio nel momento in cui il dato
        // arriva dalla porta, non quando il thread UI riesce a elaborarlo:
        // e' questo che elimina l'effetto "a pezzi" (punti raggruppati).
        private static readonly Stopwatch _clock = Stopwatch.StartNew();
        public static double NowSeconds => _clock.Elapsed.TotalSeconds;

        private System.Windows.Forms.Timer timerPlot;
        private double timeWindow = 10;
        private bool legend_on = true;

        public bool isMaster = false;

        public Form4 MasterForm { get; set; } = null;

        private readonly ConcurrentQueue<(double t, double y)> mainQueue = new ConcurrentQueue<(double t, double y)>();
        private PlotSeries mainSeries;

        public Dictionary<Form4, child_form> children = new Dictionary<Form4, child_form>();

        public bool merging = false;
        public Form1 mainform;

        // Se false il form non accoda dati propri (nascosto da merge o chiuso).
        public volatile bool is_visible = true;

        public bool playing = true;
        private bool _forceRefresh = true;

        public Form4(Form1 form1, bool masterFlag = false)
        {
            InitializeComponent();
            isMaster = masterFlag;
            mainform = form1;
        }

        private void Form4_Load(object sender, EventArgs e)
        {
            if (isMaster)
            {
                button2.Visible = true;
            }
            else
            {
                label2.Visible = false;
                button3.Visible = false;
            }

            // I limiti degli assi sono gestiti manualmente a ogni frame.
            formsPlot1.Plot.Axes.ContinuouslyAutoscale = false;
            formsPlot1.Plot.Legend.IsVisible = true;

            if (isMaster)
            {
                formsPlot1.Plot.ShowLegend(children.Values.Select(c => c.legendItem).ToArray());
                formsPlot1.Plot.Legend.Alignment = Alignment.LowerLeft;
            }
            else
            {
                mainSeries = new PlotSeries(formsPlot1.Plot, ScottPlot.Color.FromColor(System.Drawing.Color.Blue));
            }

            timerPlot = new System.Windows.Forms.Timer();
            timerPlot.Interval = 33; // ~30 fps
            timerPlot.Tick += TimerPlot_Tick;
            timerPlot.Start();
        }

        /// <summary>
        /// Thread-safe: puo' essere chiamato da qualunque thread.
        /// t = Form4.NowSeconds preso al momento della ricezione.
        /// </summary>
        public void AddDataPoint(double value, double t)
        {
            if (!isMaster && is_visible)
                mainQueue.Enqueue((t, value));

            MasterForm?.AddChildData(this, value, t);
        }

        public void AddDataPoint(double value) => AddDataPoint(value, NowSeconds);

        public void AddChildData(Form4 child, double y, double t)
        {
            if (!isMaster) return;

            if (children.TryGetValue(child, out var childEntry))
                childEntry.childQueue.Enqueue((t, y));
        }

        private static bool Drain(ConcurrentQueue<(double t, double y)> queue, PlotSeries series)
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

        private void TimerPlot_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            double now = NowSeconds;
            bool updated = false;

            // I dati vengono sempre svuotati dalle code (anche in pausa) per
            // evitare crescita illimitata; in pausa semplicemente non si ridisegna.
            if (isMaster)
            {
                foreach (var childEntry in children.Values)
                    updated |= Drain(childEntry.childQueue, childEntry.series);
            }
            else
            {
                updated = Drain(mainQueue, mainSeries);
            }

            if (!playing)
                return;

            if (!updated && !_forceRefresh)
                return;

            _forceRefresh = false;

            double xMin = now - timeWindow;
            double yMin = double.PositiveInfinity;
            double yMax = double.NegativeInfinity;

            if (isMaster)
            {
                foreach (var childEntry in children.Values)
                {
                    childEntry.series.TrimBefore(xMin);
                    childEntry.series.ExpandRange(ref yMin, ref yMax);
                }
            }
            else if (mainSeries != null)
            {
                mainSeries.TrimBefore(xMin);
                mainSeries.ExpandRange(ref yMin, ref yMax);
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

        private void button1_Click(object sender, EventArgs e) // unisci
        {
            if (!merging)
            {
                button1.Image = Properties.Resources.SyncArrowStatusBar5_extra_16x;
                merging = true;
                mainform.merge_graphs1(this.Text, this);
            }
            else
            {
                button1.Image = Properties.Resources.SyncArrowStatusBar5_16x;
                merging = false;
                mainform.undo_merge();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!isMaster) return;

            foreach (var childEntry in children.Values)
            {
                try
                {
                    if (childEntry.child != null)
                    {
                        childEntry.child.Show();
                        childEntry.child.is_visible = true;
                    }
                }
                catch
                {
                }
            }
        }

        public void reset_merge()
        {
            merging = false;
            button1.Image = Properties.Resources.SyncArrowStatusBar5_16x;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (legend_on)
            {
                button3.Image = Properties.Resources.Not_Visible_16x;
                legend_on = false;
                formsPlot1.Plot.Legend.IsVisible = false;
                toolTip1.SetToolTip(button3, "Show");
            }
            else
            {
                button3.Image = Properties.Resources.Visible_16x;
                legend_on = true;
                formsPlot1.Plot.Legend.IsVisible = true;
                toolTip1.SetToolTip(button3, "Hide");
            }

            _forceRefresh = true;
        }

        private void Form4_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Smetti di accettare dati: evita code che crescono all'infinito.
            is_visible = false;

            try { timerPlot?.Stop(); } catch { }

            if (!isMaster) return;

            foreach (var childEntry in children.Values)
            {
                try
                {
                    if (childEntry.child != null)
                    {
                        // Il figlio non deve piu' inoltrare dati a un master chiuso.
                        childEntry.child.MasterForm = null;
                        childEntry.child.is_visible = true;
                        childEntry.child.Show();
                    }
                }
                catch
                {
                }
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            timeWindow = (double)numericUpDown1.Value;
            _forceRefresh = true;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (playing)
            {
                // PAUSA
                playing = false;
                button4.Image = Properties.Resources.Play;
                toolTip1.SetToolTip(button4, "Play");
            }
            else
            {
                // PLAY
                playing = true;
                _forceRefresh = true;
                button4.Image = Properties.Resources.Pause;
                toolTip1.SetToolTip(button4, "Pause");
            }
        }
    }

    /// <summary>
    /// Serie temporale: due liste X/Y collegate a uno scatter-line di ScottPlot.
    /// Tutto avviene sul thread UI (il timer del grafico), quindi nessun lock.
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

        // Rimuove in un colpo solo tutti i punti piu' vecchi di xMin.
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

            // Mantiene un punto appena fuori finestra: la linea arriva fino al bordo sinistro.
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

    public class CircularPlotBuffer
    {
        public readonly double[] Xs;
        public readonly double[] Ys;

        private int index = 0;
        private bool filled = false;

        public CircularPlotBuffer(int capacity)
        {
            Xs = new double[capacity];
            Ys = new double[capacity];
        }

        public void Add(double x, double y)
        {
            Xs[index] = x;
            Ys[index] = y;

            index++;
            if (index >= Xs.Length)
            {
                index = 0;
                filled = true;
            }
        }

        public int Count => filled ? Xs.Length : index;
        public int Start => filled ? index : 0;
        public void CopyOrdered(double[] xsDest, double[] ysDest)
        {
            int count = Count;
            int start = Start;

            if (count == 0)
                return;

            if (!filled)
            {
                Array.Copy(Xs, xsDest, count);
                Array.Copy(Ys, ysDest, count);
            }
            else
            {
                int rightLen = Xs.Length - start;

                Array.Copy(Xs, start, xsDest, 0, rightLen);
                Array.Copy(Ys, start, ysDest, 0, rightLen);

                Array.Copy(Xs, 0, xsDest, rightLen, start);
                Array.Copy(Ys, 0, ysDest, rightLen, start);
            }
        }
    }


    public class child_form
    {
        public ConcurrentQueue<(double t, double y)> childQueue = new ConcurrentQueue<(double t, double y)>();
        public System.Drawing.Color color;
        public Form4 child;
        public PlotSeries series;
        public LegendItem legendItem;

        public child_form(Form4 in_child, System.Drawing.Color clr, ScottPlot.WinForms.FormsPlot formsPlot)
        {
            child = in_child;
            color = clr;

            var spColor = ScottPlot.Color.FromColor(color);
            series = new PlotSeries(formsPlot.Plot, spColor);

            legendItem = new LegendItem()
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
