using ScottPlot;
using ScottPlot.Colormaps;
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
        private System.Windows.Forms.Timer timerPlot;
        private Stopwatch sw = new Stopwatch();
        private double timeWindow = 10;
        private bool legend_on = true;

        // Ruolo della form
        public bool isMaster = false;

        // Form base: riferimento al master (facoltativo)
        public Form4 MasterForm { get; set; } = null;

        // Queue e logger per form base
        private ConcurrentQueue<(double t, double y)> mainQueue = new ConcurrentQueue<(double t, double y)>();
        private DataLogger logger_main;

        // Queue e logger per form master (4 slot)
        private ConcurrentQueue<(double t, double y)> child1Queue = new ConcurrentQueue<(double t, double y)>();
        private ConcurrentQueue<(double t, double y)> child2Queue = new ConcurrentQueue<(double t, double y)>();
        private ConcurrentQueue<(double t, double y)> child3Queue = new ConcurrentQueue<(double t, double y)>();
        private ConcurrentQueue<(double t, double y)> child4Queue = new ConcurrentQueue<(double t, double y)>();

        private DataLogger logger_child1;
        private DataLogger logger_child2;
        private DataLogger logger_child3;
        private DataLogger logger_child4;

        public Form4 child1 = null;
        public Form4 child2 = null;
        public Form4 child3 = null;
        public Form4 child4 = null;
        public LegendItem[] items = new LegendItem[4];

        public bool merging = false;
        public Form1 mainform;

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
                if (child4 != null)
                {
                    button1.Visible = false;
                }
                button2.Visible = true;
            }
            if (!isMaster)
            {
                label2.Visible = false;
                button3.Visible = false;
            }

            formsPlot1.Plot.Axes.ContinuouslyAutoscale = true;
            formsPlot1.Plot.Legend.IsVisible = true;

            timerPlot = new System.Windows.Forms.Timer();
            timerPlot.Interval = 50;
            timerPlot.Tick += TimerPlot_Tick;

            sw.Start();
            timerPlot.Start();


            if (isMaster)
            {
                logger_child1 = formsPlot1.Plot.Add.DataLogger();
                logger_child1.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);
                items[0] = new LegendItem()
                {
                    LineColor = Colors.Red,
                    MarkerFillColor = Colors.Red,
                    MarkerLineColor = Colors.Red,
                    MarkerShape = MarkerShape.Cross,
                    LineWidth = 2,
                    LabelText = child1.Text
                };

                logger_child2 = formsPlot1.Plot.Add.DataLogger();
                logger_child2.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Green);
                items[1] = new LegendItem()
                {
                    LineColor = Colors.Green,
                    MarkerFillColor = Colors.Green,
                    MarkerLineColor = Colors.Green,
                    MarkerShape = MarkerShape.Cross,
                    LineWidth = 2,
                    LabelText = child2.Text
                };


                if (child3 != null)
                {
                    logger_child3 = formsPlot1.Plot.Add.DataLogger();
                    logger_child3.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Orange);
                    items[2] = new LegendItem()
                    {
                        LineColor = Colors.Orange,
                        MarkerFillColor = Colors.Orange,
                        MarkerLineColor = Colors.Orange,
                        MarkerShape = MarkerShape.Cross,
                        LineWidth = 2,
                        LabelText = child3.Text
                    };
                }
                if (child4 != null)
                {
                    logger_child4 = formsPlot1.Plot.Add.DataLogger();
                    logger_child4.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Purple);
                    items[3] = new LegendItem()
                    {
                        LineColor = Colors.Purple,
                        MarkerFillColor = Colors.Purple,
                        MarkerLineColor = Colors.Purple,
                        MarkerShape = MarkerShape.Cross,
                        LineWidth = 2,
                        LabelText = child4.Text
                    };
                }
                formsPlot1.Plot.ShowLegend(items.Where(item => item != null).ToArray());
                formsPlot1.Plot.Legend.Alignment = Alignment.LowerLeft;
            }
            else
            {
                logger_main = formsPlot1.Plot.Add.DataLogger();
                logger_main.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
            }
        }

        public void AddDataPoint(double value)
        {
            double now = sw.Elapsed.TotalSeconds;

            if (!isMaster)
            {
                if (this.Visible)
                    mainQueue.Enqueue((now, value));
            }
            MasterForm?.AddChildData(this, now, value);
        }

        public void AddChildData(Form4 child, double t, double y)
        {
            if (!isMaster) return;

            // smista sulle code se il logger è presente
            if (child == child1)
                child1Queue.Enqueue((t, y));
            else if (child == child2)
                child2Queue.Enqueue((t, y));
            else if (child == child3)
                child3Queue.Enqueue((t, y));
            else if (child == child4)
                child4Queue.Enqueue((t, y));
            else
                throw new Exception("Il master riceve dati da un figlio non registrato (oltre 4)!");
        }

        private void TimerPlot_Tick(object sender, EventArgs e)
        {
            bool updated = false;
            double now = sw.Elapsed.TotalSeconds;

            if (isMaster)
            {
                if (child1 != null)
                    while (child1Queue.TryDequeue(out var p1))
                    {
                        try
                        {
                            logger_child1.Add(p1.t, p1.y);
                        }
                        catch { } //I think very rarely at the start it can get two out of order points, it's not important, it doesn't really change anything but I don't want to get an error
                        updated = true;
                    }

                if (child2 != null)
                    while (child2Queue.TryDequeue(out var p2))
                    {
                        try
                        {
                            logger_child2.Add(p2.t, p2.y);
                        }
                        catch { }
                        updated = true;
                    }

                if (child3 != null)
                    while (child3Queue.TryDequeue(out var p3))
                    {
                        try
                        {
                            logger_child3.Add(p3.t, p3.y);
                        }
                        catch { }
                        updated = true;
                    }

                if (child4 != null)
                    while (child4Queue.TryDequeue(out var p4))
                    {
                        try
                        {
                            logger_child4.Add(p4.t, p4.y);
                        }
                        catch { }
                        updated = true;
                    }
            }
            else
            {
                while (mainQueue.TryDequeue(out var p))
                {
                    logger_main.Add(p.t, p.y);
                    updated = true;
                }
            }

            if (updated)
            {
                double xMax = now;
                double xMin = now - timeWindow;
                formsPlot1.Plot.Axes.SetLimitsX(xMin, xMax);

                if (isMaster)
                {
                    if (child1 != null) logger_child1.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                    if (child2 != null) logger_child2.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                    if (child3 != null) logger_child3.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                    if (child4 != null) logger_child4.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                }
                else
                {
                    logger_main.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                }

                formsPlot1.Refresh();
            }
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

        private void button2_Click(object sender, EventArgs e) // separa
        {
            if (isMaster)
            {
                try
                {
                    child1?.Show();
                }
                catch { } //if the user closes manually the window this would throw an error, it's not really important
                try
                {
                    child2?.Show();
                }
                catch { }
                try
                {
                    child3?.Show();
                }
                catch { }
                try
                {
                    child4?.Show();
                }
                catch { }
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
            }
            else
            {
                button3.Image = Properties.Resources.Visible_16x;
                legend_on = true;
                formsPlot1.Plot.Legend.IsVisible = true;
            }

        }

        private void Form4_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void Form4_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isMaster)
            {
                try
                {
                    child1?.Show();
                }
                catch { } //if the user closes manually the window this would throw an error, it's not really important
                try
                {
                    child2?.Show();
                }
                catch { }
                try
                {
                    child3?.Show();
                }
                catch { }
                try
                {
                    child4?.Show();
                }
                catch { }
            }
        }
    }
}
