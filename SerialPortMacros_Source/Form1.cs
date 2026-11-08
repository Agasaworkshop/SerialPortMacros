using System.Collections.Concurrent;
using SerialPortMacros.Core;
using SerialPortMacros.Macros;
using SerialPortMacros.Sequences;
using SerialPortMacros.Transport;

namespace SerialPortMacros
{
    /// <summary>
    /// Finestra principale. Il codice è diviso per area:
    ///   Form1.cs           servizi, avvio/chiusura, mappa dei controlli delle 4 porte
    ///   Form1.Ports.cs     connessione seriale/TCP, impostazioni porta, formato dati
    ///   Form1.Display.cs   testo a video (decimato), input utente, cronologia comandi
    ///   Form1.Logging.cs   sessioni di log
    ///   Form1.Settings.cs  lettura/scrittura settings.json
    ///   Form1.Tools.cs     macro, grafici, sequenze, interfacce, log viewer
    ///
    /// La logica non-UI sta nelle cartelle Core, Transport, Decoding, Macros, Sequences.
    /// </summary>
    public partial class Form1 : Form
    {
        // ---- Servizi (thread-safe, indipendenti dalla UI) ----
        private readonly LogManager _log = new();
        private readonly DisplayBuffer _display = new();
        private readonly LiveData _live = new();
        private readonly GraphRouter _graphs = new();
        private readonly MacroEngine _macros = new();
        private readonly DataPipeline _pipeline;
        private readonly PortHub _hub;
        private readonly SequenceRunner _sequence = new();

        private AppSettings _settings = new();

        /// <summary>Controlli di una porta logica nella finestra principale.</summary>
        private sealed record PortUi(
            Button Connect, TextBox Value, ComboBox Com, Label ValueLabel, Button Settings,
            CheckBox Send, CheckBox Log, CheckBox Tcp, NumericUpDown GraphCount, Button Graphs);

        private readonly PortUi[] _ui;

        private System.Windows.Forms.Timer _uiTimer = null!;
        private System.Windows.Forms.Timer _portScanTimer = null!;
        private Icon? _iconOpen, _iconClosed;

        public Form1()
        {
            InitializeComponent();

            _pipeline = new DataPipeline(_log, _display, _live, _graphs, _macros);
            _hub = new PortHub(_pipeline, _log, _display, _live);
            _macros.Attach(_hub);

            _ui = new[]
            {
                new PortUi(button3, textBox3, comboBox2, label1, button8, checkBox1, checkBox8, checkBox13, numericUpDown1, button16),
                new PortUi(button5, textBox5, comboBox1, label2, button9, checkBox2, checkBox7, checkBox12, numericUpDown2, button17),
                new PortUi(button4, textBox4, comboBox3, label3, button10, checkBox3, checkBox6, checkBox11, numericUpDown3, button18),
                new PortUi(button6, textBox6, comboBox4, label4, button11, checkBox4, checkBox5, checkBox14, numericUpDown4, button19),
            };

            foreach (var ch in _hub.Channels)
                ch.StatusMessage += (text, isError) =>
                    _hub.Show(text, isError ? "Error" : "System", isError ? Color.Red : Color.DarkGreen);

            _sequence.Command += OnSequenceCommand;
        }

        // ------------------------------------------------------------------
        // API usata dagli altri form (Form3, Form4, Form6, Form7)
        // ------------------------------------------------------------------

        public ConcurrentDictionary<string, double> PanelVariables => _live.Variables;
        public ConcurrentDictionary<string, string> LatestSerialValues => _live.LatestText;
        public ConcurrentDictionary<int, List<double>> LatestPortNumbers => _live.LatestNumbers;

        /// <summary>Macro attualmente caricate (Form3 le modifica direttamente).</summary>
        public Script[] scripts => _macros.Scripts;

        public PortHub Hub => _hub;

        // ------------------------------------------------------------------
        // Avvio / chiusura
        // ------------------------------------------------------------------

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox6.Items.Clear();
            comboBox6.Items.AddRange(LineEndings.DisplayNames);

            for (int i = 0; i < _ui.Length; i++)
                RefreshComPorts(i);

            textBox1.TabStop = false;

            try { _iconOpen = new Icon(AppPaths.IconOpen); } catch { }
            try { _iconClosed = new Icon(AppPaths.IconClosed); } catch { }

            LoadSettings();

            CustomMacroTemplates.EnsureExamples();
            ScanScripts();

            _pipeline.Start();

            _portScanTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _portScanTimer.Tick += PortScanTimer_Tick;
            _portScanTimer.Start();

            // La UI legge il buffer di visualizzazione al proprio ritmo (~20 fps).
            _uiTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _uiTimer.Tick += (_, _) => FlushDisplay();
            _uiTimer.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _uiTimer?.Stop();
            _portScanTimer?.Stop();
            _watchdogCts?.Cancel();
            _macros.Running = false;

            foreach (var ch in _hub.Channels)
            {
                ch.Tcp.Stop();
                ch.CloseSerial();
            }

            _pipeline.Dispose();   // svuota la coda e fa il flush dei log
            _log.Stop();

            SaveSettings();
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            ActiveControl = null;
        }

        /// <summary>Indice 0..3 della porta a cui appartiene un controllo.</summary>
        private int PortIndexOf(object? control)
        {
            for (int i = 0; i < _ui.Length; i++)
            {
                var u = _ui[i];
                if (control == u.Connect || control == u.Value || control == u.Com || control == u.Settings ||
                    control == u.Send || control == u.Log || control == u.Tcp || control == u.GraphCount ||
                    control == u.Graphs)
                    return i;
            }
            return -1;
        }

        private void UpdateIcon()
        {
            bool anyActive = _hub.Channels.Any(c => c.IsActive);
            var icon = anyActive ? _iconClosed : _iconOpen;
            if (icon != null && Icon != icon)
                Icon = icon;
        }
    }
}
