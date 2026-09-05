using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace SerialPortMacros
{
    public partial class Log : Form
    {
        private LogParser parser;
        private string logPath;
        public bool plot_ready = false;


        public Log(string logPath)
        {
            InitializeComponent();

            this.logPath = logPath;
            parser = new LogParser();

            // Parsing iniziale con threshold 5
            if (!parser.Parse(logPath, 5))
            {
                MessageBox.Show(
                    "Non è stato possibile trovare un formato dati valido.",
                    "Errore",
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
            {
                AddElement($"Element{i + 1}");
            }

            InitializeXAxisSelector();
            plot_ready = true;
        }
        private void InitializeTable()
        {
            dataGridView1.Columns.Clear();

            // Nasconde la colonna laterale delle righe
            dataGridView1.RowHeadersVisible = false;

            // Checkbox Plot
            DataGridViewCheckBoxColumn checkColumn =
                new DataGridViewCheckBoxColumn();

            checkColumn.Name = "Plot";
            checkColumn.HeaderText = "";
            checkColumn.Width = 40;
            checkColumn.AutoSizeMode =
                DataGridViewAutoSizeColumnMode.None;

            dataGridView1.Columns.Add(checkColumn);


            // Element
            DataGridViewTextBoxColumn elementColumn =
                new DataGridViewTextBoxColumn();

            elementColumn.Name = "Element";
            elementColumn.HeaderText = "Element";
            elementColumn.ReadOnly = true;
            elementColumn.Width = 75;
            elementColumn.AutoSizeMode =
                DataGridViewAutoSizeColumnMode.None;

            dataGridView1.Columns.Add(elementColumn);


            // Gain
            DataGridViewTextBoxColumn gainColumn =
                new DataGridViewTextBoxColumn();

            gainColumn.Name = "Gain";
            gainColumn.HeaderText = "Gain";
            gainColumn.AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;

            dataGridView1.Columns.Add(gainColumn);

            DataGridViewTextBoxColumn cutoffColumn =
                new DataGridViewTextBoxColumn();

            cutoffColumn.Name = "Cutoff";
            cutoffColumn.HeaderText = "Cutoff [Hz]";
            cutoffColumn.Width = 55;
            cutoffColumn.AutoSizeMode =
                DataGridViewAutoSizeColumnMode.None;

            dataGridView1.Columns.Add(cutoffColumn);
        }


        private void AddElement(string name)
        {
            int rowIndex = dataGridView1.Rows.Add();

            DataGridViewRow row =
                dataGridView1.Rows[rowIndex];

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
            {
                comboBox1.Items.Add($"Element{i}");
            }

            comboBox1.SelectedIndex = 0;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBox1.Enabled = comboBox1.SelectedIndex == 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RefreshPlot();
            plot_ready = true;
        }
        private void RefreshPlot()
        {
            if (parser == null || parser.Data == null)
                return;

            formsPlot1.Plot.Clear();

            double[] xs = BuildXAxis();

            bool atLeastOneSignal = false;

            // Il filtro è disponibile solamente con asse X Auto
            bool filterEnabled =
                comboBox1.SelectedIndex == 0;

            double samplingTime = 1.0;

            if (filterEnabled)
            {
                if (!double.TryParse(
                    textBox1.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out samplingTime))
                {
                    samplingTime = 1.0;
                }

                if (samplingTime <= 0)
                    samplingTime = 1.0;
            }

            double samplingFrequency =
                1.0 / samplingTime;

            for (int element = 0;
                 element < parser.ElementCount;
                 element++)
            {
                DataGridViewRow row =
                    dataGridView1.Rows[element];

                // =====================================
                // CHECKBOX PLOT
                // =====================================

                bool plot =
                    row.Cells["Plot"].Value != null &&
                    Convert.ToBoolean(row.Cells["Plot"].Value);

                if (!plot)
                    continue;

                atLeastOneSignal = true;


                // =====================================
                // GAIN
                // =====================================

                double gain = 1.0;

                double.TryParse(
                    row.Cells["Gain"].Value?.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out gain);


                // =====================================
                // CUTOFF
                // =====================================

                double cutoff = 0.0;

                double.TryParse(
                    row.Cells["Cutoff"].Value?.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out cutoff);


                // =====================================
                // DATI ORIGINALI
                // =====================================

                double[] originalY =
                    parser.Data[element];

                double[] filteredY =
                    (double[])originalY.Clone();


                // =====================================
                // FILTRO
                // Solo con asse X AUTO
                // =====================================

                if (filterEnabled &&
                    cutoff > 0 &&
                    cutoff < samplingFrequency / 2.0)
                {
                    filteredY =
                        ButterworthFilter.LowPass(
                            filteredY,
                            samplingFrequency,
                            cutoff,
                            4);
                }


                // =====================================
                // GAIN
                // =====================================

                double[] scaledY =
                    new double[filteredY.Length];

                for (int i = 0; i < filteredY.Length; i++)
                {
                    scaledY[i] =
                        filteredY[i] * gain;
                }


                // =====================================
                // PLOT
                // =====================================

                var signal =
                    formsPlot1.Plot.Add.Scatter(
                        xs,
                        scaledY);

                signal.MarkerSize = 0;

                signal.LegendText =
                    $"Element{element + 1}";
            }


            if (!atLeastOneSignal)
            {
                formsPlot1.Refresh();
                return;
            }

            formsPlot1.Plot.Axes.AutoScale();

            formsPlot1.Refresh();
        }


        public static class ButterworthFilter
        {
            public static double[] LowPass(
                double[] input,
                double sampleRate,
                double cutoff,
                int order = 4)
            {
                if (input == null || input.Length == 0)
                    return Array.Empty<double>();

                if (cutoff <= 0)
                    return (double[])input.Clone();

                if (cutoff >= sampleRate / 2.0)
                    return (double[])input.Clone();

                // Per ora implementiamo il filtro come
                // cascata di sezioni biquad.
                int stages = order / 2;

                double[] output =
                    (double[])input.Clone();

                for (int stage = 0; stage < stages; stage++)
                {
                    output = ApplyLowPassBiquad(
                        output,
                        sampleRate,
                        cutoff);
                }

                return output;
            }


            private static double[] ApplyLowPassBiquad(
                double[] input,
                double sampleRate,
                double cutoff)
            {
                double Q = 1.0 / Math.Sqrt(2.0);

                double omega =
                    2.0 * Math.PI * cutoff / sampleRate;

                double sinOmega = Math.Sin(omega);
                double cosOmega = Math.Cos(omega);

                double alpha =
                    sinOmega / (2.0 * Q);

                double b0 =
                    (1.0 - cosOmega) / 2.0;

                double b1 =
                    1.0 - cosOmega;

                double b2 =
                    (1.0 - cosOmega) / 2.0;

                double a0 =
                    1.0 + alpha;

                double a1 =
                    -2.0 * cosOmega;

                double a2 =
                    1.0 - alpha;

                // Normalizzazione
                b0 /= a0;
                b1 /= a0;
                b2 /= a0;
                a1 /= a0;
                a2 /= a0;

                double[] output =
                    new double[input.Length];

                double x1 = 0;
                double x2 = 0;

                double y1 = 0;
                double y2 = 0;

                for (int i = 0; i < input.Length; i++)
                {
                    double x0 = input[i];

                    double y0 =
                        b0 * x0 +
                        b1 * x1 +
                        b2 * x2 -
                        a1 * y1 -
                        a2 * y2;

                    output[i] = y0;

                    x2 = x1;
                    x1 = x0;

                    y2 = y1;
                    y1 = y0;
                }

                return output;
            }
        }

        private void RefreshParser()
        {
            int threshold = (int)numericUpDown1.Value;

            if (threshold < 1)
            {
                MessageBox.Show(
                    "Il threshold deve essere almeno 1.",
                    "Valore non valido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            if (!parser.Parse(logPath, threshold))
            {
                MessageBox.Show(
                    $"Non è stato trovato un formato con almeno {threshold} occorrenze.",
                    "Formato non trovato",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            InitializeTable();

            for (int i = 0; i < parser.ElementCount; i++)
            {
                AddElement($"Element{i + 1}");
            }

            InitializeXAxisSelector();
        }
        private void numericUpDown1_Leave(object sender, EventArgs e)
        {
            RefreshParser();
        }
        private double[] BuildXAxis()
        {
            int sampleCount = parser.SampleCount;

            // ==========================================
            // AUTO
            // ==========================================

            if (comboBox1.SelectedIndex == 0)
            {
                double samplingTime = 1.0;

                if (!double.TryParse(
                    textBox1.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out samplingTime))
                {
                    samplingTime = 1.0;
                }

                if (samplingTime <= 0)
                    samplingTime = 1.0;

                double[] xs = new double[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                    xs[i] = i * samplingTime;

                return xs;
            }


            // ==========================================
            // ELEMENTO COME ASSE X
            // ==========================================

            int elementIndex = comboBox1.SelectedIndex - 1;

            if (elementIndex >= 0 &&
                elementIndex < parser.ElementCount)
            {
                return parser.Data[elementIndex];
            }


            // Fallback
            return Enumerable.Range(0, sampleCount)
                             .Select(i => (double)i)
                             .ToArray();
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (!plot_ready) { return; }
            if (dataGridView1.IsCurrentCellDirty &&
                dataGridView1.CurrentCell is DataGridViewCheckBoxCell)
            {
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void dataGridView1_CellValueChanged(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (!plot_ready)
                return;

            if (e.RowIndex < 0)
                return;

            string columnName =
                dataGridView1.Columns[e.ColumnIndex].Name;

            if (columnName == "Plot" ||
                columnName == "Gain" ||
                columnName == "Cutoff")
            {
                RefreshPlot();
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            plot_ready = false;
        }
    }

    public class LogParser
    {
        public double[][] Data { get; private set; }

        public int ElementCount { get; private set; }

        public int SampleCount { get; private set; }


        public bool Parse(
            string filePath,
            int minimumOccurrences = 5)
        {
            if (!File.Exists(filePath))
                return false;

            string[] lines = File.ReadAllLines(filePath);

            // -------------------------------------------------
            // PRIMA PASSATA
            // Conta quante righe contengono N elementi
            // -------------------------------------------------

            Dictionary<int, int> occurrences = new Dictionary<int, int>();

            foreach (string line in lines)
            {
                List<double> numbers = ParseDataLine(line);

                if (numbers.Count == 0)
                    continue;

                int count = numbers.Count;

                if (!occurrences.ContainsKey(count))
                    occurrences[count] = 0;

                occurrences[count]++;
            }


            // -------------------------------------------------
            // Trova il formato più frequente che supera
            // la soglia minima
            // -------------------------------------------------

            int selectedElementCount = -1;
            int maxOccurrences = 0;

            foreach (var pair in occurrences)
            {
                if (pair.Value >= minimumOccurrences &&
                    pair.Value > maxOccurrences)
                {
                    selectedElementCount = pair.Key;
                    maxOccurrences = pair.Value;
                }
            }


            // Nessun formato sufficientemente frequente
            if (selectedElementCount == -1)
            {
                Data = null;
                ElementCount = 0;
                SampleCount = 0;

                return false;
            }


            // -------------------------------------------------
            // SECONDA PASSATA
            // Estrae tutte le righe compatibili
            // -------------------------------------------------

            List<List<double>> samples =
                new List<List<double>>();

            foreach (string line in lines)
            {
                List<double> numbers = ParseDataLine(line);

                if (numbers.Count == selectedElementCount)
                {
                    samples.Add(numbers);
                }
            }


            if (samples.Count == 0)
                return false;


            // -------------------------------------------------
            // Costruzione della matrice
            // -------------------------------------------------

            ElementCount = selectedElementCount;
            SampleCount = samples.Count;

            Data = new double[ElementCount][];

            for (int element = 0; element < ElementCount; element++)
            {
                Data[element] = new double[SampleCount];

                for (int sample = 0; sample < SampleCount; sample++)
                {
                    Data[element][sample] =
                        samples[sample][element];
                }
            }

            return true;
        }


        // =====================================================
        // PARSE DELLA SINGOLA RIGA
        // =====================================================

        private List<double> ParseDataLine(string line)
        {
            line = RemoveTimestamp(line);

            return ParseNumbers(line);
        }


        // =====================================================
        // RIMOZIONE TIMESTAMP
        // =====================================================

        private string RemoveTimestamp(string line)
        {
            Match match = Regex.Match(
                line,
                @"^\s*\d{2}:\d{2}:\d{2}(?:\.\d+)?\s*\|\s*"
            );

            if (match.Success)
                return line.Substring(match.Length);

            return line;
        }


        // =====================================================
        // ESTRAZIONE NUMERI
        // =====================================================

        public static List<double> ParseNumbers(string s)
        {
            var numbers = new List<double>();

            int i = 0;

            while (i < s.Length)
            {
                // Cerca l'inizio di un numero
                while (i < s.Length &&
                       !char.IsDigit(s[i]) &&
                       s[i] != '-' &&
                       s[i] != '+')
                {
                    i++;
                }

                if (i >= s.Length)
                    break;

                int start = i;

                // Segno
                if (s[i] == '-' || s[i] == '+')
                    i++;

                bool hasDigit = false;
                bool hasDot = false;

                while (i < s.Length)
                {
                    char c = s[i];

                    if (char.IsDigit(c))
                    {
                        hasDigit = true;
                        i++;
                    }
                    else if (c == '.' && !hasDot)
                    {
                        hasDot = true;
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (hasDigit)
                {
                    string token =
                        s.Substring(start, i - start);

                    if (double.TryParse(
                        token,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value))
                    {
                        numbers.Add(value);
                    }
                }
            }

            return numbers;
        }
    }
}
