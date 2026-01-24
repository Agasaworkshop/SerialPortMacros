using ScottPlot;
using ScottPlot.Colormaps;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialPortMacros
{
    public partial class Form4 : Form
    {

        private System.Windows.Forms.Timer timerPlot;
        private ConcurrentQueue<(double t, double y)> dataQueue = new ConcurrentQueue<(double t, double y)>();
        private List<double> xs = new List<double>();
        private List<double> ys = new List<double>();
        private double timeWindow; // ultimi 10 secondi
        private DataLogger logger;
        private double startTime;
        private Stopwatch sw = new Stopwatch();

        public Form4()
        {
            InitializeComponent();
            logger = formsPlot1.Plot.Add.DataLogger();
            formsPlot1.Plot.Axes.ContinuouslyAutoscale = true;
            timerPlot = new System.Windows.Forms.Timer();
            timerPlot.Interval = 50;
            timerPlot.Tick += TimerPlot_Tick;
            sw.Start();
            timerPlot.Start();
        }

        private void Form4_Load(object sender, EventArgs e)
        {
        }

        public void AddDataPoint(double value)
        {
            double now = sw.Elapsed.TotalSeconds;
            dataQueue.Enqueue((now, value));
        }

        private void TimerPlot_Tick(object sender, EventArgs e)
        {
            bool updated = false;
            double now = sw.Elapsed.TotalSeconds;
            while (dataQueue.TryDequeue(out var point))
            {
                logger.Add(point.t, point.y);
                updated = true;
            }

            if (updated)
            {
                double xMax = now;
                double xMin = now - timeWindow;
                formsPlot1.Plot.Axes.SetLimitsX(xMin, xMax);
                timeWindow = (double)numericUpDown1.Value;
                logger.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                formsPlot1.Refresh();
            }
        }

    }
}
