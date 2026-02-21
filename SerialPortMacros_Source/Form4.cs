using ScottPlot;
using ScottPlot.AxisLimitManagers;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SerialPortMacros
{
    public partial class Form4 : Form
    {
        private System.Windows.Forms.Timer timerPlot;
        private Stopwatch sw = new Stopwatch();
        private double timeWindow = 10;
        private bool legend_on = true;

        public bool isMaster = false;


        public Form4 MasterForm { get; set; } = null;


        private ConcurrentQueue<(double t, double y)> mainQueue = new ConcurrentQueue<(double t, double y)>();
        private DataLogger logger_main;


        public Dictionary<Form4, child_form> children = new Dictionary<Form4, child_form>();

        public bool merging = false;
        public Form1 mainform;
        public bool is_visible = true;


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
            }else
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
                formsPlot1.Plot.ShowLegend(children.Values.Select(c => c.legendItem).ToArray());
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
            MasterForm?.AddChildData(this, value);
        }


        public void AddChildData(Form4 child, double y)
        {
            double t = sw.Elapsed.TotalSeconds;

            if (!isMaster) return;

            if (!children.TryGetValue(child, out var childEntry))
                throw new Exception("Il master riceve dati da un figlio non registrato!");

            childEntry.childQueue.Enqueue((t, y));
        }

        private void TimerPlot_Tick(object sender, EventArgs e)
        {
            bool updated = false;
            double now = sw.Elapsed.TotalSeconds;

            if (isMaster)
            {
                foreach (var childEntry in children.Values)
                {
                    while (childEntry.childQueue.TryDequeue(out var p))
                    {
                        try
                        {
                            childEntry.logger.Add(p.t, p.y);
                        }
                        catch
                        {
                        }
                        updated = true;
                    }
                }
            }
            else
            {
                while (mainQueue.TryDequeue(out var p))
                {
                    try
                    {
                        logger_main.Add(p.t, p.y);
                    }
                    catch { }
                    updated = true;
                }
            }

            if (updated)
            {
                double xMax = now;
                double xMin = now - timeWindow;
                formsPlot1.Plot.Axes.ContinuouslyAutoscale = false;

                if (isMaster)
                {
                    foreach (var childEntry in children.Values)
                    {
                        childEntry.logger.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                    }
                }
                else
                {
                    logger_main.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                }
                formsPlot1.Plot.Axes.SetLimitsX(xMin, xMax);
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

        }

        private void Form4_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isMaster) return;

            foreach (var childEntry in children.Values)
            {
                try
                {
                    childEntry.child?.Show();
                }
                catch
                {
                }
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            timeWindow = (double)numericUpDown1.Value;
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
        public DataLogger logger;
        public LegendItem legendItem;

        public child_form(Form4 in_child, System.Drawing.Color clr, ScottPlot.WinForms.FormsPlot formsPlot)
        {
            child = in_child;
            color = clr;

            // Crea il logger direttamente
            logger = formsPlot.Plot.Add.DataLogger();
            var spColor = ScottPlot.Color.FromColor(color);
            logger.Color = spColor;

            // Crea il LegendItem
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
