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
    public partial class Form5 : Form
    {
        private LogParser parser;
        private string logPath;


        public Form5(string logPath)
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
        }


        private void AddElement(string name)
        {
            int rowIndex = dataGridView1.Rows.Add();

            DataGridViewRow row =
                dataGridView1.Rows[rowIndex];

            row.Cells["Plot"].Value = false;
            row.Cells["Element"].Value = name;
            row.Cells["Gain"].Value = 1.0;
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
        }
        private void RefreshPlot()
        {
            if (parser == null || parser.Data == null)
                return;

            formsPlot1.Plot.Clear();

            double[] xs = BuildXAxis();

            bool atLeastOneSignal = false;


            for (int element = 0;
                 element < parser.ElementCount;
                 element++)
            {
                DataGridViewRow row =
                    dataGridView1.Rows[element];

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
                // CREA I DATI SCALATI
                // =====================================

                double[] originalY = parser.Data[element];

                double[] scaledY = new double[originalY.Length];

                for (int i = 0; i < originalY.Length; i++)
                {
                    scaledY[i] = originalY[i] * gain;
                }


                // =====================================
                // PLOT
                // =====================================

                var signal =
                    formsPlot1.Plot.Add.Scatter(xs, scaledY);

                // niente pallini
                signal.MarkerSize = 0;

                signal.LegendText =
                    $"Element{element + 1}";
            }


            if (!atLeastOneSignal)
            {
                formsPlot1.Refresh();
                return;
            }


            // ScottPlot determina automaticamente
            // i limiti del grafico
            formsPlot1.Plot.Axes.AutoScale();

            formsPlot1.Refresh();
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
