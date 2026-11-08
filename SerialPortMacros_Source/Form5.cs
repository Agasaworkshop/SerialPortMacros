using System.Globalization;
using SerialPortMacros.Core;

namespace SerialPortMacros
{
    /// <summary>
    /// Log viewer: finds the most common numeric line format in a log file and plots
    /// the selected elements, with optional gain and low-pass filter per element.
    /// </summary>
    public partial class Log : Form
    {
        private readonly LogParser parser;
        private readonly string logPath;
        private bool _plotReady;

        public Log(string logPath)
        {
            InitializeComponent();

            this.logPath = logPath;
            parser = new LogParser();

            // Initial parse with threshold 5
            if (!parser.Parse(logPath, 5))
            {
                MessageBox.Show(
                    "No valid data format could be found.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }
        }

        private void Form5_Load(object sender, EventArgs e)
        {
            InitializeTable();
            numericUpDown1.Value = 5;

            for (int i = 0; i < parser.ElementCount; i++)
                AddElement($"Element{i + 1}");

            InitializeXAxisSelector();
            _plotReady = true;
        }

        // ------------------------------------------------------------------
        // Element table and X axis selector
        // ------------------------------------------------------------------

        private void InitializeTable()
        {
            dataGridView1.Columns.Clear();

            // Hide the row header column
            dataGridView1.RowHeadersVisible = false;

            // Plot check box
            var checkColumn = new DataGridViewCheckBoxColumn
            {
                Name = "Plot",
                HeaderText = "",
                Width = 40,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            dataGridView1.Columns.Add(checkColumn);

            // Element
            var elementColumn = new DataGridViewTextBoxColumn
            {
                Name = "Element",
                HeaderText = "Element",
                ReadOnly = true,
                Width = 75,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            dataGridView1.Columns.Add(elementColumn);

            // Gain
            var gainColumn = new DataGridViewTextBoxColumn
            {
                Name = "Gain",
                HeaderText = "Gain",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
            dataGridView1.Columns.Add(gainColumn);

            // Cutoff
            var cutoffColumn = new DataGridViewTextBoxColumn
            {
                Name = "Cutoff",
                HeaderText = "Cutoff [Hz]",
                Width = 55,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            dataGridView1.Columns.Add(cutoffColumn);
        }

        private void AddElement(string name)
        {
            int rowIndex = dataGridView1.Rows.Add();
            DataGridViewRow row = dataGridView1.Rows[rowIndex];

            row.Cells["Plot"].Value = false;
            row.Cells["Element"].Value = name;
            row.Cells["Gain"].Value = 1.0;
            row.Cells["Cutoff"].Value = 0.0;
        }

        private void InitializeXAxisSelector()
        {
            comboBox1.Items.Clear();
            comboBox1.Items.Add("Auto");

            for (int i = 1; i <= parser.ElementCount; i++)
                comboBox1.Items.Add($"Element{i}");

            comboBox1.SelectedIndex = 0;
        }

        // ------------------------------------------------------------------
        // Plot
        // ------------------------------------------------------------------

        private void RefreshPlot()
        {
            if (parser == null || parser.Data == null)
                return;

            formsPlot1.Plot.Clear();

            double[] xs = BuildXAxis();
            bool atLeastOneSignal = false;

            // The filter is only available with the Auto X axis
            bool filterEnabled = comboBox1.SelectedIndex == 0;

            double samplingTime = 1.0;
            if (filterEnabled)
            {
                if (!double.TryParse(textBox1.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out samplingTime))
                    samplingTime = 1.0;

                if (samplingTime <= 0)
                    samplingTime = 1.0;
            }

            double samplingFrequency = 1.0 / samplingTime;

            for (int element = 0; element < parser.ElementCount; element++)
            {
                DataGridViewRow row = dataGridView1.Rows[element];

                // Plot check box
                bool plot = row.Cells["Plot"].Value != null && Convert.ToBoolean(row.Cells["Plot"].Value);
                if (!plot)
                    continue;

                atLeastOneSignal = true;

                // Gain
                double gain = 1.0;
                double.TryParse(row.Cells["Gain"].Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out gain);

                // Cutoff
                double cutoff = 0.0;
                double.TryParse(row.Cells["Cutoff"].Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out cutoff);

                // Original data
                double[] originalY = parser.Data[element];
                double[] filteredY = (double[])originalY.Clone();

                // Filter (Auto X axis only)
                if (filterEnabled && cutoff > 0 && cutoff < samplingFrequency / 2.0)
                    filteredY = ButterworthFilter.LowPass(filteredY, samplingFrequency, cutoff, 4);

                // Gain
                double[] scaledY = new double[filteredY.Length];
                for (int i = 0; i < filteredY.Length; i++)
                    scaledY[i] = filteredY[i] * gain;

                // Plot
                var signal = formsPlot1.Plot.Add.Scatter(xs, scaledY);
                signal.MarkerSize = 0;
                signal.LegendText = $"Element{element + 1}";
            }

            if (!atLeastOneSignal)
            {
                formsPlot1.Refresh();
                return;
            }

            formsPlot1.Plot.Axes.AutoScale();
            formsPlot1.Refresh();
        }

        private double[] BuildXAxis()
        {
            int sampleCount = parser.SampleCount;

            // Auto: sample index * sampling time
            if (comboBox1.SelectedIndex == 0)
            {
                double samplingTime = 1.0;

                if (!double.TryParse(textBox1.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out samplingTime))
                    samplingTime = 1.0;

                if (samplingTime <= 0)
                    samplingTime = 1.0;

                double[] xs = new double[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                    xs[i] = i * samplingTime;

                return xs;
            }

            // An element used as X axis
            int elementIndex = comboBox1.SelectedIndex - 1;
            if (elementIndex >= 0 && elementIndex < parser.ElementCount)
                return parser.Data[elementIndex];

            // Fallback
            return Enumerable.Range(0, sampleCount).Select(i => (double)i).ToArray();
        }

        private void RefreshParser()
        {
            int threshold = (int)numericUpDown1.Value;

            if (threshold < 1)
            {
                MessageBox.Show(
                    "The threshold must be at least 1.",
                    "Invalid value",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (!parser.Parse(logPath, threshold))
            {
                MessageBox.Show(
                    $"No format with at least {threshold} occurrences was found.",
                    "Format not found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            InitializeTable();

            for (int i = 0; i < parser.ElementCount; i++)
                AddElement($"Element{i + 1}");

            InitializeXAxisSelector();
        }

        // ------------------------------------------------------------------
        // Control events
        // ------------------------------------------------------------------

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBox1.Enabled = comboBox1.SelectedIndex == 0;
        }

        // Plot button
        private void button1_Click(object sender, EventArgs e)
        {
            RefreshPlot();
            _plotReady = true;
        }

        private void numericUpDown1_Leave(object sender, EventArgs e)
        {
            RefreshParser();
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            _plotReady = false;
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (!_plotReady)
                return;

            // Commit check box clicks immediately so CellValueChanged fires.
            if (dataGridView1.IsCurrentCellDirty && dataGridView1.CurrentCell is DataGridViewCheckBoxCell)
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!_plotReady)
                return;

            if (e.RowIndex < 0)
                return;

            string columnName = dataGridView1.Columns[e.ColumnIndex].Name;
            if (columnName == "Plot" || columnName == "Gain" || columnName == "Cutoff")
                RefreshPlot();
        }
    }
}
