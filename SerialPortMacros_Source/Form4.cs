using ScottPlot;
using ScottPlot.Colormaps;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using SerialPortMacros;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialPortMacros
{
    public partial class Form4 : Form
    {


        private System.Windows.Forms.Timer timerPlot;
        private Stopwatch sw = new Stopwatch();
        private double timeWindow = 10;

        // Ruolo della form
        public bool isMaster = false;

        // Form base: riferimento al master (facoltativo)
        public Form4 MasterForm { get; set; } = null;

        // Queue e logger per form base
        private ConcurrentQueue<(double t, double y)> mainQueue = new ConcurrentQueue<(double t, double y)>();
        private DataLogger logger_main;

        // Queue e logger per form master
        private ConcurrentQueue<(double t, double y)> child1Queue = new ConcurrentQueue<(double t, double y)>();
        private ConcurrentQueue<(double t, double y)> child2Queue = new ConcurrentQueue<(double t, double y)>();
        private DataLogger logger_child1;
        private DataLogger logger_child2;
        public bool merging = false;
        public Form1 mainform;
        public Form4 child1;
        public Form4 child2;




        public Form4(Form1 form1, bool masterFlag = false)
        {
            InitializeComponent();
            isMaster = masterFlag;

            formsPlot1.Plot.Axes.ContinuouslyAutoscale = true;

            timerPlot = new System.Windows.Forms.Timer();
            timerPlot.Interval = 50;
            timerPlot.Tick += TimerPlot_Tick;

            sw.Start();
            timerPlot.Start();
            mainform = form1;


            if (isMaster)
            {
                // Logger per due figli
                logger_child1 = formsPlot1.Plot.Add.DataLogger();
                logger_child1.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);

                logger_child2 = formsPlot1.Plot.Add.DataLogger();
                logger_child2.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Green);
            }
            else
            {
                // Logger principale
                logger_main = formsPlot1.Plot.Add.DataLogger();
                logger_main.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
            }

            formsPlot1.Plot.Axes.ContinuouslyAutoscale = true;
            timerPlot = new System.Windows.Forms.Timer();
            timerPlot.Interval = 50;
            timerPlot.Tick += TimerPlot_Tick;
            sw.Start();
            timerPlot.Start();
        }

        private void Form4_Load(object sender, EventArgs e)
        {
            if (isMaster)
            {
                button1.Visible = false;
                button2.Visible = true;
            }
        }

        public void AddDataPoint(double value)
        {
            double now = sw.Elapsed.TotalSeconds;

            if (!isMaster)
            {
                if (this.Visible)
                    mainQueue.Enqueue((now, value));

                // Inoltra al master se presente
                MasterForm?.AddChildData(this, now, value);
            }
        }
        
        public void AddChildData(Form4 child, double t, double y)
        {
            if (!isMaster) return;

            // assegna il figlio 1 o 2 se non ancora assegnato
            if (child1 == null)
                child1 = child;
            else if (child2 == null && child != child1)
                child2 = child;

            // ora decide a quale logger inviare
            if (child == child1)
                child1Queue.Enqueue((t, y));
            else if (child == child2)
                child2Queue.Enqueue((t, y));
            else
                throw new Exception("Il master riceve dati da un figlio non registrato!");
        }


        private void TimerPlot_Tick(object sender, EventArgs e)
        {
            bool updated = false;
            double now = sw.Elapsed.TotalSeconds;

            if (isMaster)
            {
                while (child1Queue.TryDequeue(out var p1))
                {
                    logger_child1.Add(p1.t, p1.y);
                    updated = true;
                }
                while (child2Queue.TryDequeue(out var p2))
                {
                    logger_child2.Add(p2.t, p2.y);
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
                    logger_child1.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                    logger_child2.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                }
                else
                {
                    logger_main.Data.Coordinates.RemoveAll(c => c.X < now - timeWindow);
                }

                formsPlot1.Refresh();
            }
        }

        private void button1_Click(object sender, EventArgs e) //unisci
        {
            if (!merging)
            {
                button1.Image = Properties.Resources.SyncArrowStatusBar5_extra_16x;
                merging = true;
                mainform.merge_graphs1(this.Text, this);
            } else
            { 
                button1.Image = Properties.Resources.SyncArrowStatusBar5_16x;
                merging = false;
                mainform.undo_merge();

            }
        }

        private void button2_Click(object sender, EventArgs e) //separa
        {
            if (isMaster)
            {
                child1.Show();
                child2.Show();
            }
        }
        public void reset_merge()
        {
            merging = false;
            button1.Image = Properties.Resources.SyncArrowStatusBar5_16x;
        }
    }
}
