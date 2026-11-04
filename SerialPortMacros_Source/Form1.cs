using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using WindowsInput;
using System.Globalization;
using System.Collections.Concurrent;
using System.Text.RegularExpressions; // Regex, Match
using System.Linq;                   // Cast, Select, ToList
using System.Globalization;          // CultureInfo
using System.Windows.Forms;
using System.Text.Json;

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.Design;

namespace SerialPortMacros
{
    public partial class Form1 : Form
    {

        public enum ConnectionMode
        {
            Serial,
            Tcp
        }

        // All ports start in serial mode.
        private readonly ConnectionMode[] _connectionModes =
        {
            ConnectionMode.Serial,
            ConnectionMode.Serial,
            ConnectionMode.Serial,
            ConnectionMode.Serial
        };

        public ConcurrentDictionary<string, double> PanelVariables = new ConcurrentDictionary<string, double>();
        public ConcurrentDictionary<string, string> LatestSerialValues = new ConcurrentDictionary<string, string>();
        public ConcurrentDictionary<int, List<double>> LatestPortNumbers = new ConcurrentDictionary<int, List<double>>();


        // ---- Receive pipeline ----
        // Serial/TCP threads put messages in this queue (with the receive timestamp);
        // the UI thread drains them in batches from the _uiTimer timer.
        private readonly struct RxMessage
        {
            public readonly string Text;
            public readonly string? PortName;   // null => to be resolved on the UI thread (TCP)
            public readonly int Logical;
            public readonly Color Color;
            public readonly DateTime Wall;
            public readonly double Clock;

            public RxMessage(string text, string? portName, int logical, Color color, DateTime wall, double clock)
            {
                Text = text; PortName = portName; Logical = logical;
                Color = color; Wall = wall; Clock = clock;
            }
        }

        private readonly ConcurrentQueue<RxMessage> _rxQueue = new ConcurrentQueue<RxMessage>();
        private readonly List<(string Text, Color Color)> _pendingDisplay = new List<(string, Color)>();
        private readonly List<(string Text, Color Color)> _heldDisplay = new List<(string, Color)>();
        private readonly StringBuilder _displaySb = new StringBuilder();
        private long _lastWriterFlushTick;
        private bool _scanningPorts = false;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;

        private readonly int[] _tcpLocalPorts = { 5001, 5002, 5003, 5004 }; // configurable TCP ports
        private readonly string[] _baudRates = { "9600", "9600", "9600", "9600" };
        private bool _loadingSettings = false;

        private readonly TcpListener?[] _tcpListeners = new TcpListener?[4];
        private readonly TcpClient?[] _tcpClients = new TcpClient?[4];
        private readonly NetworkStream?[] _tcpStreams = new NetworkStream?[4];
        private readonly CancellationTokenSource?[] _tcpCts =
            new CancellationTokenSource?[4];

        private readonly object[] _tcpSendLocks =
        {
            new object(), new object(), new object(), new object()
        };

        private readonly StringBuilder[] _tcpBuffers =
        {
            new StringBuilder(), new StringBuilder(),
            new StringBuilder(), new StringBuilder()
        };


        public SerialPort port1 = new SerialPort();
        public SerialPort port2 = new SerialPort();
        public SerialPort port3 = new SerialPort();
        public SerialPort port4 = new SerialPort();
        public int selectedport;
        public bool opn_p1 = false;
        public bool opn_p2 = false;
        public bool opn_p3 = false;
        public bool opn_p4 = false;
        public bool is_running = false;
        public bool time_log = true;
        public Script[] scripts;
        public bool is_locked = true;
        public bool logging = false;
        public StringBuilder mes1 = new StringBuilder();
        public StringBuilder mes2 = new StringBuilder();
        public StringBuilder mes3 = new StringBuilder();
        public StringBuilder mes4 = new StringBuilder();
        public StreamWriter _writer1;
        private string _logFilePath1;
        public StreamWriter _writer2;
        private string _logFilePath2;
        public StreamWriter _writer3;
        private string _logFilePath3;
        public StreamWriter _writer4;
        private string _logFilePath4;
        public StreamWriter _writer5;
        private string _logFilePath5;
        public bool multilog = false;
        public bool looking_for_merge = false;
        private Form4 merging_form = null;
        public Dictionary<string, List<Form4>> openGraphs = new Dictionary<string, List<Form4>>();
        private System.Windows.Forms.Timer _uiTimer;
        private System.Windows.Forms.Timer port_timeout;
        string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private AppSettings settings = new AppSettings();

        private CancellationTokenSource? _watchdogCts;
        private long _lastLogMessageTick;


        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox6.Items.Clear();

            comboBox6.Items.Add("No line ending");
            comboBox6.Items.Add("New line");
            comboBox6.Items.Add("Carriage return");
            comboBox6.Items.Add("Both NL & CR");

            for (int k = 1; k < 5; k++)
            {
                Searchports(k);
            }

            setports();
            ScanScripts();

            textBox1.TabStop = false;


            port_timeout = new System.Windows.Forms.Timer();
            port_timeout.Interval = 2000;
            port_timeout.Tick += remove_ports;
            port_timeout.Start();

            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 25;
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            LoadSettings();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (!is_running)
            {
                button1.Image = Properties.Resources.Pause;
                is_running = true;
                ScanScripts();
            }
            else
            {
                button1.Image = Properties.Resources.Play;
                is_running = false;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ScanScripts();
            Form3 macro_form = new Form3(this);
            macro_form.ShowDialog();
        }

        private List<string> commandHistory = new List<string>();
        private int historyIndex = 0; // 0 = normal textbox (no history active)
        private const int maxHistory = 50;

        private void textBox1_Keypress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;

                string inputText = textBox1.Text;

                if (!string.IsNullOrWhiteSpace(inputText))
                {
                    // Add to history
                    commandHistory.Add(inputText);

                    // Keep only the last 50
                    if (commandHistory.Count > maxHistory)
                        commandHistory.RemoveAt(0);
                }

                historyIndex = 0; // reset index

                aggiungi_a_textbox2(inputText, "You", Color.Black);
                textBox1.Text = "";
                Sendtoports(inputText);

                LogUserInput(inputText);
            }
        }

        private void send_log_message(string inputText)
        {
            aggiungi_a_textbox2(inputText, "You", Color.Black);
            Sendtoports(inputText);

            LogUserInput(inputText);
        }


        // Always on the UI thread (like all other log writes): no concurrency
        // on the StreamWriters. Writing is buffered.
        private void LogUserInput(string inputText)
        {
            if (!logging || !checkBox9.Checked)
                return;

            string log_input_text = $"You: {inputText}";

            if (time_log)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                log_input_text = $"{timestamp} | You: {inputText}";
            }

            if (checkBox8.Checked) _writer1?.WriteLine(log_input_text);
            if (checkBox7.Checked) _writer2?.WriteLine(log_input_text);
            if (checkBox6.Checked) _writer3?.WriteLine(log_input_text);
            if (checkBox5.Checked) _writer4?.WriteLine(log_input_text);
            if (multilog) _writer5?.WriteLine(log_input_text);
        }



        private async void button3_Click(object sender, EventArgs e) => await ToggleConnectionAsync(1);
        private async void button5_Click(object sender, EventArgs e) => await ToggleConnectionAsync(2);
        private async void button4_Click(object sender, EventArgs e) => await ToggleConnectionAsync(3);
        private async void button6_Click(object sender, EventArgs e) => await ToggleConnectionAsync(4);

        private void button7_Click(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            Portsettings(port1, comboBox2.Text);
        }
        private void button9_Click(object sender, EventArgs e)
        {
            Portsettings(port2, comboBox1.Text);
        }
        private void button10_Click(object sender, EventArgs e)
        {
            Portsettings(port3, comboBox3.Text);
        }

        private void button11_Click(object sender, EventArgs e)
        {
            Portsettings(port4, comboBox4.Text);
        }
        private void setports()
        {
            port1.BaudRate = 9600;
            port1.Encoding = Encoding.ASCII;
            port1.NewLine = "\n";
            textBox3.Text = "9600";
            port1.Parity = Parity.None;
            port1.DataBits = 8;
            port1.StopBits = StopBits.One;

            port2.BaudRate = 9600;
            port2.Encoding = Encoding.ASCII;
            port2.NewLine = "\n";
            textBox5.Text = "9600";
            port2.Parity = Parity.None;
            port2.DataBits = 8;
            port2.StopBits = StopBits.One;

            port3.BaudRate = 9600;
            port3.Encoding = Encoding.ASCII;
            port3.NewLine = "\n";
            textBox4.Text = "9600";
            port3.Parity = Parity.None;
            port3.DataBits = 8;
            port3.StopBits = StopBits.One;

            port4.BaudRate = 9600;
            port4.Encoding = Encoding.ASCII;
            port4.NewLine = "\n";
            textBox6.Text = "9600";
            port4.Parity = Parity.None;
            port4.DataBits = 8;
            port4.StopBits = StopBits.One;

            port1.DataReceived += SerialPort_DataReceived1;
            port2.DataReceived += SerialPort_DataReceived2;
            port3.DataReceived += SerialPort_DataReceived3;
            port4.DataReceived += SerialPort_DataReceived4;
        }

        private string getparity(SerialPort port)
        {
            if (port.Parity == Parity.Odd)
                return "Odd";
            if (port.Parity == Parity.Even)
                return "Even";
            return "None";
        }
        private string getstopbits(SerialPort port)
        {
            if (port.StopBits == StopBits.One)
                return "One";
            if (port.StopBits == StopBits.Two)
                return "Two";
            if (port.StopBits == StopBits.OnePointFive)
                return "OnePointFive";
            return "None";
        }
        private void Portsettings(SerialPort port, string name)
        {
            if (name != "")
            {
                Form2 setting_form = new Form2(port);
                setting_form.Text = name;
                setting_form.Paritybox.Text = getparity(port);
                setting_form.databitsbox.Text = port.DataBits.ToString();
                setting_form.stopbitsbox.Text = getstopbits(port);
                setting_form.ShowDialog();
            }
        }

        private void Searchports(int n)
        {
            ComboBox c1 = null;

            if (n == 1)
                c1 = comboBox2;
            else if (n == 2)
                c1 = comboBox1;
            else if (n == 3)
                c1 = comboBox3;
            else if (n == 4)
                c1 = comboBox4;

            if (c1 != null)
            {

                string previouslySelected = c1.SelectedItem as string;


                string[] availablePorts = SerialPort.GetPortNames();

                c1.Items.Clear();
                c1.Items.AddRange(availablePorts);


                if (!string.IsNullOrEmpty(previouslySelected) &&
                    availablePorts.Contains(previouslySelected))
                {
                    c1.SelectedItem = previouslySelected;
                }
            }
        }

        const int MaxChars = 30000;

        private bool _draining = false;

        private string FormatLine(string text, string usr, DateTime when)
        {
            return time_log
                ? $"{when:HH:mm:ss.fff} | {usr}: {text}"
                : $"{usr}: {text}";
        }

        private void QueueDisplay(string line, Color color)
        {
            _pendingDisplay.Add((line, color));
        }

        // API used by the rest of the code (UI thread). Outside the drain the
        // text is shown immediately; during the drain it is accumulated and
        // written in a single operation at the end of the batch.
        private void aggiungi_a_textbox2(string inputText, string usr, Color color)
        {
            QueueDisplay(FormatLine(inputText, usr, DateTime.Now), color);
            if (!_draining)
                FlushDisplay();
        }

        // Writes all pending lines to the RichTextBox, grouping consecutive lines
        // of the same color into a single AppendText.
        private void FlushDisplay()
        {
            if (stop_text)
            {
                // The user is selecting text: hold the lines back.
                if (_pendingDisplay.Count > 0)
                {
                    _heldDisplay.AddRange(_pendingDisplay);
                    _pendingDisplay.Clear();
                    if (_heldDisplay.Count > 20000)
                        _heldDisplay.RemoveRange(0, _heldDisplay.Count - 20000);
                }
                return;
            }

            List<(string Text, Color Color)> items;
            if (_heldDisplay.Count > 0)
            {
                _heldDisplay.AddRange(_pendingDisplay);
                _pendingDisplay.Clear();
                items = _heldDisplay;
            }
            else
            {
                items = _pendingDisplay;
            }

            if (items.Count == 0)
                return;

            int selStart = textBox2.SelectionStart;
            int selLength = textBox2.SelectionLength;

            // With autoscroll on, text older than MaxChars would be discarded anyway,
            // so there is no need to write it at all.
            int startIdx = 0;
            if (is_locked)
            {
                int total = 0;
                for (int k = items.Count - 1; k >= 0; k--)
                {
                    total += items[k].Text.Length + 2;
                    if (total > MaxChars)
                    {
                        startIdx = k + 1;
                        break;
                    }
                }

                if (textBox2.TextLength > MaxChars)
                    textBox2.Text = "";
            }

            bool suspend = is_locked && (items.Count - startIdx) > 1;
            if (suspend)
                SendMessage(textBox2.Handle, WM_SETREDRAW, 0, 0);

            try
            {
                bool hasText = textBox2.TextLength > 0;
                int i = startIdx;
                while (i < items.Count)
                {
                    Color c = items[i].Color;
                    _displaySb.Clear();

                    while (i < items.Count && items[i].Color == c)
                    {
                        if (hasText)
                            _displaySb.Append(Environment.NewLine);
                        _displaySb.Append(items[i].Text);
                        hasText = true;
                        i++;
                    }

                    textBox2.SelectionStart = textBox2.TextLength;
                    textBox2.SelectionLength = 0;
                    textBox2.SelectionColor = c;
                    textBox2.AppendText(_displaySb.ToString());
                }
            }
            finally
            {
                if (suspend)
                {
                    SendMessage(textBox2.Handle, WM_SETREDRAW, 1, 0);
                    textBox2.Invalidate();
                }
            }

            if (is_locked)
            {
                textBox2.ScrollToCaret();
            }
            else
            {
                // Restore the user's position
                textBox2.SelectionStart = selStart;
                textBox2.SelectionLength = selLength;
            }

            _heldDisplay.Clear();
            _pendingDisplay.Clear();
        }

        // Drains in batches the messages coming from the receive threads.
        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (IsDisposed)
                return;

            _draining = true;
            try
            {
                long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 12 / 1000;
                int n = 0;

                while (_rxQueue.TryDequeue(out RxMessage m))
                {
                    try { ProcessRx(m); }
                    catch (Exception ex) { Debug.WriteLine("ProcessRx: " + ex.Message); }

                    if ((++n & 15) == 0 && Stopwatch.GetTimestamp() > deadline)
                        break;
                }
            }
            finally
            {
                _draining = false;
            }

            FlushDisplay();

            if (logging && Environment.TickCount64 - _lastWriterFlushTick >= 500)
                FlushWriters();
        }

        private void FlushWriters()
        {
            _lastWriterFlushTick = Environment.TickCount64;
            try { _writer1?.Flush(); } catch { }
            try { _writer2?.Flush(); } catch { }
            try { _writer3?.Flush(); } catch { }
            try { _writer4?.Flush(); } catch { }
            try { _writer5?.Flush(); } catch { }
        }

        private void ProcessRx(in RxMessage m)
        {
            int n = m.Logical;
            bool po1 = n == 1, po2 = n == 2, po3 = n == 3, po4 = n == 4;

            string portName = m.PortName ?? GetGraphPortName(n);
            if (string.IsNullOrWhiteSpace(portName))
                portName = $"TCP{n}";

            // --- AGGIUNTA MINIMA PER FORM7 ---
            LatestSerialValues[$"port{n}"] = m.Text;
            LatestSerialValues[portName] = m.Text;
            List<double> nums = ParseNumbers(m.Text);
            if (nums.Count > 0)
            {
                LatestPortNumbers[n] = nums;
            }
            // ---------------------------------

            if (logging)
                write_logs(po1, po2, po3, po4, m.Text, portName, m.Wall);

            QueueDisplay(FormatLine(m.Text, portName, m.Wall), m.Color);

            scriptcheck(m.Text, po1, po2, po3, po4);

            if (openGraphs.Count > 0 &&
                openGraphs.TryGetValue(portName, out var forms) &&
                forms.Count > 0)
            {
                int count = Math.Min(forms.Count, nums.Count);
                for (int i = 0; i < count; i++)
                {
                    Form4 graph = forms[i];
                    if (graph != null && !graph.IsDisposed)
                        graph.AddDataPoint(nums[i], m.Clock);
                }
            }
        }
        public void SendCustomMacroLine(bool[] targetPorts, string message, string customLineEnding = "")
        {
            // Verifica se è stata spuntata almeno una porta
            bool hasTargetPorts = targetPorts != null && targetPorts.Length >= 4 && (targetPorts[0] || targetPorts[1] || targetPorts[2] || targetPorts[3]);

            // Se ci sono porte selezionate, invia il messaggio
            if (hasTargetPorts)
            {
                string lineEnding = "";
                if (!string.IsNullOrEmpty(customLineEnding))
                {
                    if (customLineEnding.Contains("New line")) lineEnding = "\n";
                    else if (customLineEnding.Contains("Carriage return") && !customLineEnding.Contains("Both")) lineEnding = "\r";
                    else if (customLineEnding.Contains("Both")) lineEnding = "\r\n";
                }
                else
                {
                    switch (comboBox6.SelectedIndex)
                    {
                        case 1: lineEnding = "\n"; break;
                        case 2: lineEnding = "\r"; break;
                        case 3: lineEnding = "\r\n"; break;
                    }
                }

                string[] subMessages = message.Split(new string[] { "\n", "\\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string subMsg in subMessages)
                {
                    string trimmedMsg = subMsg.Trim();
                    if (string.IsNullOrEmpty(trimmedMsg)) continue;

                    string dataToSend = trimmedMsg + lineEnding;

                    if (targetPorts[0]) SendToLogicalPort(1, opn_p1, true, port1, dataToSend);
                    if (targetPorts[1]) SendToLogicalPort(2, opn_p2, true, port2, dataToSend);
                    if (targetPorts[2]) SendToLogicalPort(3, opn_p3, true, port3, dataToSend);
                    if (targetPorts[3]) SendToLogicalPort(4, opn_p4, true, port4, dataToSend);

                    aggiungi_a_textbox2(trimmedMsg, "Macro/Form7", Color.Purple);
                    LogUserInput(trimmedMsg);

                    if (subMessages.Length > 1) Thread.Sleep(50);
                }
            }
            else
            {
                // Nessuna porta selezionata: aggiorna solo la textbox/log per tracciare l'azione (es. toggle di sola variabile)
                aggiungi_a_textbox2(message, "Macro/Form7 (No Port)", Color.Purple);
                LogUserInput(message);
            }
        }

        // Last receive time per logical port (indices 1..4).
        private readonly double[] _lastRxClock = new double[5];

        // Several lines can arrive in the same packet (a single serial/TCP read).
        // If they all had the same timestamp, the plot would show vertical jumps
        // ("steps") instead of segments. The lines of the packet are therefore
        // spread evenly between the arrival of the previous packet and the
        // current one.
        private void EnqueueBatch(List<string> messages, string? portName, int logical, Color color)
        {
            int n = messages.Count;
            if (n == 0)
                return;

            if (_stopRequested)
                _lastLogMessageTick = Environment.TickCount64;

            double now = Form4.NowSeconds;
            DateTime wallNow = DateTime.Now;

            double prev = _lastRxClock[logical];
            // After a period of silence it is unknown when the lines were generated:
            // limit the interval over which they are spread.
            double span = n > 1 ? Math.Min(Math.Max(now - prev, 0.0), 0.25) : 0.0;
            _lastRxClock[logical] = now;

            for (int i = 0; i < n; i++)
            {
                // The last line gets exactly the arrival time.
                double back = span * (n - 1 - i) / n;
                _rxQueue.Enqueue(new RxMessage(
                    messages[i], portName, logical, color,
                    wallNow.AddSeconds(-back), now - back));
            }
        }





        private int Openport(int baud, SerialPort c_port, string name)
        {
            if (baud > 0 && name != "")
            {
                c_port.PortName = name;
                c_port.BaudRate = baud;
                try
                {
                    c_port.Open();
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine("Access to the port is denied: " + ex.Message);
                    return 0;
                }
                catch (IOException ex)
                {
                    Console.WriteLine("An I/O error occurred: " + ex.Message);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                    return 0;
                }
                return 1;
            }
            return 0;
        }

        private void Sendtoports(string output)
        {
            string lineEnding = "";

            switch (comboBox6.SelectedIndex)
            {
                case 0: // No line ending
                    lineEnding = "";
                    break;

                case 1: // New line
                    lineEnding = "\n";
                    break;

                case 2: // Carriage return
                    lineEnding = "\r";
                    break;

                case 3: // Both NL & CR
                    lineEnding = "\r\n";
                    break;
            }

            string dataToSend = output + lineEnding;

            SendToLogicalPort(1, opn_p1, checkBox1.Checked, port1, dataToSend);
            SendToLogicalPort(2, opn_p2, checkBox2.Checked, port2, dataToSend);
            SendToLogicalPort(3, opn_p3, checkBox3.Checked, port3, dataToSend);
            SendToLogicalPort(4, opn_p4, checkBox4.Checked, port4, dataToSend);
        }


        private void CambiaModalitaPorta(int porta, bool usaWifi)
        {
            int index = porta - 1;
            if (index < 0 || index >= 4) return;

            ConnectionMode newMode = usaWifi ? ConnectionMode.Tcp : ConnectionMode.Serial;
            ConnectionMode oldMode = _connectionModes[index];
            TextBox[] valueBoxes = { textBox3, textBox5, textBox4, textBox6 };
            Label[] labels = { label1, label2, label3, label4 };
            ComboBox[] serialCombos = { comboBox2, comboBox1, comboBox3, comboBox4 };

            // Keep the value of the mode being left.
            if (!_loadingSettings && oldMode == ConnectionMode.Serial && newMode == ConnectionMode.Tcp)
                _baudRates[index] = valueBoxes[index].Text;
            else if (!_loadingSettings && oldMode == ConnectionMode.Tcp && newMode == ConnectionMode.Serial)
            {
                if (int.TryParse(valueBoxes[index].Text, out int currentPort) && currentPort >= 1 && currentPort <= 65535)
                    _tcpLocalPorts[index] = currentPort;
            }

            if (oldMode != newMode && newMode == ConnectionMode.Serial)
                StopTcpPort(porta);

            _connectionModes[index] = newMode;
            labels[index].Text = usaWifi ? "port" : "baud";
            serialCombos[index].Enabled = !usaWifi;

            if (!_loadingSettings)
            {
                if (usaWifi)
                {
                    // First restore the saved port: the previous text was the baud rate.
                    valueBoxes[index].Text = _tcpLocalPorts[index].ToString();
                    EnsureUniqueTcpPort(porta);
                }
                else
                {
                    valueBoxes[index].Text = _baudRates[index];
                }
            }

            if (newMode == ConnectionMode.Serial)
            {
                Button[] buttons = { button3, button5, button4, button6 };
                buttons[index].Image = Properties.Resources.AddConnection;
                toolTip1.SetToolTip(buttons[index], "Connect");
                valueBoxes[index].Enabled = true;
            }
        }

        private int FindAvailableTcpPort(int logicalPort, int preferredPort)
        {
            // A port is logically in use only if another TCP connection is enabled.
            bool InUse(int candidate) => Enumerable.Range(0, 4).Any(i =>
                i != logicalPort - 1 &&
                _connectionModes[i] == ConnectionMode.Tcp &&
                _tcpLocalPorts[i] == candidate);

            if (preferredPort >= 1 && preferredPort <= 65535 && !InUse(preferredPort))
                return preferredPort;

            for (int candidate = 5001; candidate <= 65535; candidate++)
                if (!InUse(candidate)) return candidate;

            throw new InvalidOperationException("No TCP ports available.");
        }

        private void EnsureUniqueTcpPort(int logicalPort)
        {
            int index = logicalPort - 1;
            TextBox[] valueBoxes = { textBox3, textBox5, textBox4, textBox6 };
            int requested = _tcpLocalPorts[index];
            if (int.TryParse(valueBoxes[index].Text, out int typed) && typed >= 1 && typed <= 65535)
                requested = typed;

            int assigned = FindAvailableTcpPort(logicalPort, requested);
            _tcpLocalPorts[index] = assigned;
            valueBoxes[index].Text = assigned.ToString();
            if (assigned != requested)
                aggiungi_a_textbox2($"TCP port {requested} was already used by another active Wi-Fi connection. Automatically assigned port {assigned} to logical port {logicalPort}.", "System", Color.DarkOrange);
        }

        private Task ToggleConnectionAsync(int logicalPort)
        {
            int index = logicalPort - 1;
            Button[] buttons = { button3, button5, button4, button6 };
            TextBox[] baudBoxes = { textBox3, textBox5, textBox4, textBox6 };
            ComboBox[] portCombos = { comboBox2, comboBox1, comboBox3, comboBox4 };
            SerialPort[] serialPorts = { port1, port2, port3, port4 };
            bool[] isOpen = { opn_p1, opn_p2, opn_p3, opn_p4 };
            if (index < 0 || index >= 4) return Task.CompletedTask;

            Button button = buttons[index];
            if (_connectionModes[index] == ConnectionMode.Tcp)
            {
                if (_tcpListeners[index] != null)
                {
                    StopTcpPort(logicalPort);
                    baudBoxes[index].Enabled = true;
                    button.Image = Properties.Resources.AddConnection;
                    toolTip1.SetToolTip(button, "Connect");
                }
                else
                {
                    if (!int.TryParse(baudBoxes[index].Text, out int tcpPort) || tcpPort < 1 || tcpPort > 65535)
                    {
                        MessageBox.Show("Enter a valid TCP port (1-65535).", "Invalid TCP port", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return Task.CompletedTask;
                    }
                    _tcpLocalPorts[index] = tcpPort;
                    int uniquePort = FindAvailableTcpPort(logicalPort, tcpPort);
                    if (uniquePort != tcpPort)
                    {
                        _tcpLocalPorts[index] = uniquePort;
                        baudBoxes[index].Text = uniquePort.ToString();
                        aggiungi_a_textbox2($"TCP port {tcpPort} is already used by an active Wi-Fi connection. Automatically assigned port {uniquePort}.", "System", Color.DarkOrange);
                    }
                    if (isOpen[index]) CloseSerialPort(logicalPort);
                    StartTcpPort(logicalPort);
                    if (_tcpListeners[index] != null)
                    {
                        baudBoxes[index].Enabled = false;
                        button.Image = Properties.Resources.Disconnect;
                        toolTip1.SetToolTip(button, $"TCP listening on port {_tcpLocalPorts[index]}");
                    }
                }
                return Task.CompletedTask;
            }

            if (isOpen[index])
            {
                CloseSerialPort(logicalPort);
                button.Image = Properties.Resources.AddConnection;
                toolTip1.SetToolTip(button, "Connect");
            }
            else
            {
                if (!int.TryParse(baudBoxes[index].Text, out int baud) || baud <= 0)
                {
                    MessageBox.Show("Enter a valid baud rate.");
                    return Task.CompletedTask;
                }
                if (Openport(baud, serialPorts[index], portCombos[index].Text) != 1) return Task.CompletedTask;
                SetSerialOpenFlag(logicalPort, true);
                baudBoxes[index].Enabled = false;
                button.Image = Properties.Resources.Disconnect;
                toolTip1.SetToolTip(button, "Disconnect");
                this.Icon = new Icon(Path.Combine(exeFolder, "icon_closed.ico"));
            }
            return Task.CompletedTask;
        }

        private void CloseSerialPort(int logicalPort)
        {
            SerialPort[] ports = { port1, port2, port3, port4 };
            TextBox[] baudBoxes = { textBox3, textBox5, textBox4, textBox6 };
            int index = logicalPort - 1;
            try { if (ports[index].IsOpen) ports[index].Close(); } catch { }
            SetSerialOpenFlag(logicalPort, false);
            baudBoxes[index].Enabled = true;
            if (!opn_p1 && !opn_p2 && !opn_p3 && !opn_p4 && _tcpListeners.All(x => x == null))
                this.Icon = new Icon(Path.Combine(exeFolder, "icon_open.ico"));
        }

        private void SetSerialOpenFlag(int logicalPort, bool value)
        {
            switch (logicalPort)
            {
                case 1: opn_p1 = value; break;
                case 2: opn_p2 = value; break;
                case 3: opn_p3 = value; break;
                case 4: opn_p4 = value; break;
            }
        }


        private void SerialPort_DataReceived1(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port1, 1, true, false, false, false, opn_p1, checkBox1, Color.Red, mes1, port1Enabled);
        }
        private void SerialPort_DataReceived2(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port2, 2, false, true, false, false, opn_p2, checkBox2, Color.Blue, mes2, port2Enabled);
        }
        private void SerialPort_DataReceived3(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port3, 3, false, false, true, false, opn_p3, checkBox3, Color.Orange, mes3, port3Enabled);
        }
        private void SerialPort_DataReceived4(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port4, 4, false, false, false, true, opn_p4, checkBox4, Color.Green, mes4, port4Enabled);
        }
        public void ScanScripts()
        {
            Directory.CreateDirectory("./scripts");
            string[] paths = Directory.GetFiles("./scripts");
            int i = 0;
            scripts = new Script[paths.Length];
            foreach (string path in paths)
            {
                string name = Path.GetFileName(path);
                scripts[i] = new Script();
                scripts[i].name = name;
                scripts[i].path = path;
                string[] lines = File.ReadAllLines(path);

                if (lines.Length > 0) scripts[i].key = lines[0];
                if (lines.Length > 1) scripts[i].trigger = int.Parse(lines[1]);
                if (lines.Length > 2) scripts[i].p1 = bool.Parse(lines[2]);
                if (lines.Length > 3) scripts[i].p2 = bool.Parse(lines[3]);
                if (lines.Length > 4) scripts[i].p3 = bool.Parse(lines[4]);
                if (lines.Length > 5) scripts[i].p4 = bool.Parse(lines[5]);

                if (scripts[i].trigger == 1)
                {
                    if (lines.Length > 6) scripts[i].keypress = lines[6];
                    if (lines.Length > 7) scripts[i].active = bool.Parse(lines[7]);
                }
                else if (scripts[i].trigger == 2)
                {
                    if (lines.Length > 6) scripts[i].reply = lines[6];
                    if (lines.Length > 7) scripts[i].p1_w = bool.Parse(lines[7]);
                    if (lines.Length > 8) scripts[i].p2_w = bool.Parse(lines[8]);
                    if (lines.Length > 9) scripts[i].p3_w = bool.Parse(lines[9]);
                    if (lines.Length > 10) scripts[i].p4_w = bool.Parse(lines[10]);
                    if (lines.Length > 11) scripts[i].active = bool.Parse(lines[11]);

                    if (lines.Length > 12 && int.TryParse(lines[12], out int le))
                    {
                        scripts[i].lineEndingIndex = le;
                    }
                    else
                    {
                        scripts[i].lineEndingIndex = 1; // Default a NL per vecchi script
                    }
                }
                i++;
            }
        }
        public void scriptcheck(string data, bool p1, bool p2, bool p3, bool p4)
        {
            if (is_running)
            {
                foreach (Script script in scripts)
                {
                    if ((script.active && ((data.Contains(script.key)) || ((script.key == "===") && data.StartsWith("===")) || (script.key == "*")) && (((p1 == script.p1 && script.p1 == true)) || (p2 == script.p2 && script.p2 == true) || (p3 == script.p3 && script.p3 == true) || (p4 == script.p4 && script.p4 == true))))
                        if (script.trigger == 1)
                        {
                            string keypressCopy = script.keypress;
                            Thread inputThread = new Thread(() =>
                            {
                                try { scr_KeyPress(keypressCopy, data); }
                                catch (Exception ex) { Debug.WriteLine("Script: " + ex.Message); }
                            });
                            inputThread.IsBackground = true;
                            inputThread.Start();
                        }
                        else if (script.trigger == 2)
                            Reply(script);
                }
            }
        }

        public void scr_KeyPress(string car, string data)
        {
            int time = 0;
            bool raw = false;
            bool press = false;
            bool release = false;
            char singlekey = '\0';
            if (car == "===")
            {
                car = data;
                car = car.Substring(3);
            }
            VirtualKeyCode keypress = new VirtualKeyCode();
            if (car[0] == '*')
            {
                car = car.Substring(1);
                raw = true;
            }
            if (car[0] == '%')
            {
                int i = 1;
                while (car[i] != '%')
                {
                    time = ((time * 10) + (car[i] - '0'));
                    i++;
                }
                car = car.Substring(i + 1);
            }
            else if (car[0] == '+')
            {
                press = true;
                car = car.Substring(1);
            }
            else if (car[0] == '-')
            {
                release = true;
                car = car.Substring(1);
            }
            if (car.Length == 1)
            {
                singlekey = car[0];
                keypress = (VirtualKeyCode)car[0];
            }
            if (raw)
            {
                int raw_key = Convert.ToInt32(car, 16);
                keypress = (VirtualKeyCode)raw_key;
            }
            if (singlekey != '\0' || raw)
            {
                if (press)
                    new InputSimulator().Keyboard.KeyDown(keypress);
                else if (release)
                    new InputSimulator().Keyboard.KeyUp(keypress);
                else if (time != 0)
                {
                    new InputSimulator().Keyboard.KeyDown(keypress);
                    wait(time);
                    new InputSimulator().Keyboard.KeyUp(keypress);
                }
                else
                    new InputSimulator().Keyboard.KeyPress(keypress);
            }
            else
            {
                if (time == 0)
                {
                    if (car[0] != '!')
                        new InputSimulator().Keyboard.TextEntry(car);
                    else if (car == "!LMB")
                        new InputSimulator().Mouse.LeftButtonClick();
                    else if (car == "!RMB")
                        new InputSimulator().Mouse.RightButtonClick();
                    else if (car == "!MMB")
                        new InputSimulator().Mouse.MiddleButtonClick();
                }
                else
                {
                    if (car[0] != '!')
                    {
                        Stopwatch sw = new Stopwatch();
                        TimeSpan targetTime = TimeSpan.FromMilliseconds(time);
                        sw.Start();
                        while (sw.Elapsed < targetTime)
                        {
                            new InputSimulator().Keyboard.TextEntry(car);
                            Thread.Sleep(10);
                        }
                        sw.Stop();
                        sw.Reset();

                    }
                    else if (car == "!LMB")
                    {
                        new InputSimulator().Mouse.LeftButtonDown();
                        wait(time);
                        new InputSimulator().Mouse.LeftButtonUp();
                    }
                    else if (car == "!RMB")
                    {
                        new InputSimulator().Mouse.RightButtonDown();
                        wait(time);
                        new InputSimulator().Mouse.RightButtonUp();
                    }
                    else if (car == "!MMB")
                    {
                        new InputSimulator().Mouse.MiddleButtonDown();
                        wait(time);
                        new InputSimulator().Mouse.MiddleButtonUp();
                    }
                }
            }
        }
        public void Reply(Script c_script)
        {
            aggiungi_a_textbox2(c_script.reply, c_script.name, Color.Brown);

            string lineEnding = "";
            switch (c_script.lineEndingIndex)
            {
                case 0: lineEnding = ""; break;       // None
                case 1: lineEnding = "\n"; break;     // NL
                case 2: lineEnding = "\r"; break;     // CR
                case 3: lineEnding = "\r\n"; break;   // Both
                default: lineEnding = "\n"; break;
            }

            string replyData = c_script.reply + lineEnding;
            SendToLogicalPort(1, opn_p1, c_script.p1_w, port1, replyData);
            SendToLogicalPort(2, opn_p2, c_script.p2_w, port2, replyData);
            SendToLogicalPort(3, opn_p3, c_script.p3_w, port3, replyData);
            SendToLogicalPort(4, opn_p4, c_script.p4_w, port4, replyData);
        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (is_locked)
            {
                is_locked = false;
                toolTip1.SetToolTip(button12, "AutoScrolling off");
                button12.Image = Properties.Resources.Unlock;
                textBox2.ScrollBars = RichTextBoxScrollBars.Both;
                //                textBox2.Enabled = true;

            }
            else
            {
                is_locked = true;
                button12.Image = Properties.Resources.Lock;
                toolTip1.SetToolTip(button12, "AutoScrolling on");
                textBox2.ScrollBars = RichTextBoxScrollBars.None;
                //               textBox2.Enabled = false;
            }
            textBox2.SelectionStart = textBox2.Text.Length;
            textBox2.SelectionLength = 0;
            textBox2.ScrollToCaret();
        }

        private void button13_Click(object sender, EventArgs e)
        {
            textBox2.Text = "";
        }
        public void wait(int time_ms)
        {
            Stopwatch sw = new Stopwatch();
            TimeSpan targetTime = TimeSpan.FromMilliseconds(time_ms);
            sw.Start();
            while (sw.Elapsed < targetTime)
            {
                Thread.Sleep(1);
            }
        }


        public void Serial_port_data2(
        SerialPort port, int num,
        bool po1, bool po2, bool po3, bool po4,
        bool opn, CheckBox checkbox, Color color,
        StringBuilder sb, bool log)
        {
            // Runs on the serial port thread: no UI here. Lines are split and
            // queued with the receive timestamp.
            string chunk;
            try { chunk = port.ReadExisting(); }
            catch (Exception) { return; }

            string data = sb.Length == 0 ? chunk : sb.ToString() + chunk;
            sb.Clear();

            string portName = port.PortName;
            var batch = new List<string>();

            int start = 0;
            for (int i = 0; i < data.Length; i++)
            {
                char c = data[i];
                if (c != '\n' && c != '\r')
                    continue;

                if (i > start)
                {
                    string mess = data.Substring(start, i - start).Trim();

                    if (mess.Length > 0 && opn && !string.IsNullOrWhiteSpace(portName))
                        batch.Add(mess);
                }

                start = i + 1;
            }

            EnqueueBatch(batch, portName, num, color);

            if (start < data.Length)
            {
                // Incomplete line: wait for the rest (with a safety limit).
                if (data.Length - start <= 30000)
                    sb.Append(data, start, data.Length - start);
            }
        }


        public static List<double> ParseNumbers(string s)
        {
            var numbers = new List<double>();
            int i = 0;

            while (i < s.Length)
            {

                while (i < s.Length && !char.IsDigit(s[i]) && s[i] != '-' && s[i] != '.')
                    i++;

                int start = i;
                int dotCount = 0;

                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == '-'))
                {
                    if (s[i] == '.')
                    {
                        dotCount++;
                        if (dotCount > 1) break;
                    }
                    i++;
                }

                if (start < i &&
                    double.TryParse(s.Substring(start, i - start),
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out double val))
                {
                    numbers.Add(val);
                }
            }

            return numbers;
        }


        private void write_logs(bool po1, bool po2, bool po3, bool po4, string line, string usr, DateTime when)
        {
            string multilog_line = $"{usr}: {line}";
            if (time_log)
            {
                string timestamp = when.ToString("HH:mm:ss.fff");
                multilog_line = $"{timestamp} | {usr}: {line}";
                line = $"{timestamp} | {line}";
            }
            if (po1 && checkBox8.Checked)
            {
                _writer1?.WriteLine(line);
            }
            if (po2 && checkBox7.Checked)
            {
                _writer2?.WriteLine(line);
            }
            if (po3 && checkBox6.Checked)
            {
                _writer3?.WriteLine(line);
            }
            if (po4 && checkBox5.Checked)
            {
                _writer4?.WriteLine(line);
            }
            if (multilog)
            {
                _writer5?.WriteLine(multilog_line);
            }
        }


        private bool _stopRequested = false;


        private void button14_Click(object sender, EventArgs e)
        {
            if (logging == false)
            {
                if (settings.log_message1 != "")
                    send_log_message(settings.log_message1);


                checkBox8.Enabled = false;
                checkBox7.Enabled = false;
                checkBox6.Enabled = false;
                checkBox5.Enabled = false;
                checkBox9.Enabled = false;
                logging = true;
                button14.Text = "Logging...";


                _logFilePath1 = null;
                _logFilePath2 = null;
                _logFilePath3 = null;
                _logFilePath4 = null;
                _logFilePath5 = null;



                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string logsDir = Path.Combine(baseDir, "logs");
                Directory.CreateDirectory(logsDir);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string sessionDir = Path.Combine(logsDir, timestamp);
                Directory.CreateDirectory(sessionDir);
                int check_n = 0;
                if (checkBox8.Checked) //first serial port
                {
                    check_n++;
                    _logFilePath1 = Path.Combine(sessionDir, "log1.txt");
                    _writer1 = new StreamWriter(
                        new FileStream(
                            _logFilePath1,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.Read
                        )
                     );
                    _writer1.AutoFlush = false;
                }
                if (checkBox7.Checked) //second serial port 
                {
                    check_n++;
                    _logFilePath2 = Path.Combine(sessionDir, "log2.txt");
                    _writer2 = new StreamWriter(
                        new FileStream(
                            _logFilePath2,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.Read
                        )
                     );
                    _writer2.AutoFlush = false;
                }
                if (checkBox6.Checked) //third serial port 
                {
                    check_n++;
                    _logFilePath3 = Path.Combine(sessionDir, "log3.txt");
                    _writer3 = new StreamWriter(
                        new FileStream(
                            _logFilePath3,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.Read
                        )
                     );
                    _writer3.AutoFlush = false;
                }
                if (checkBox5.Checked) //third serial port 
                {
                    check_n++;
                    _logFilePath4 = Path.Combine(sessionDir, "log4.txt");
                    _writer4 = new StreamWriter(
                        new FileStream(
                            _logFilePath4,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.Read
                        )
                     );
                    _writer4.AutoFlush = false;
                }
                if (check_n > 1)
                {
                    multilog = true;
                    _logFilePath5 = Path.Combine(sessionDir, "log_mixed.txt");
                    _writer5 = new StreamWriter(
                        new FileStream(
                            _logFilePath5,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.Read
                        )
                     );
                    _writer5.AutoFlush = false;
                }
                else
                    multilog = false;
            }
            else
            {
                if (_stopRequested)
                    return;

                _stopRequested = true;

                button14.Text = "Waiting...";

                // IMPORTANT:
                // logging stays TRUE for the whole response
                logging = true;

                // Command to the microcontroller:
                // "send me the log"
                if (settings.log_message2 != "")
                    send_log_message(settings.log_message2);

                // If we don't need to wait for the end,
                // we can close right away.
                if (!settings.log_check)
                {
                    _stopRequested = false;
                    logging = false;

                    checkBox8.Enabled = true;
                    checkBox7.Enabled = true;
                    checkBox6.Enabled = true;
                    checkBox5.Enabled = true;
                    checkBox9.Enabled = true;

                    button14.Text = "Start Logging";

                    _writer1?.Close();
                    _writer1 = null;

                    _writer2?.Close();
                    _writer2 = null;

                    _writer3?.Close();
                    _writer3 = null;

                    _writer4?.Close();
                    _writer4 = null;

                    _writer5?.Close();
                    _writer5 = null;
                    if (checkBox10.Checked)
                        OpenLogForms();
                    return;
                }

                _lastLogMessageTick = Environment.TickCount64;
                _startWatchdog();
            }
        }

        private void StopLogging()
        {
            if (!_stopRequested)
                return;

            _stopRequested = false;
            logging = false;

            _watchdogCts?.Cancel();
            _watchdogCts = null;

            checkBox8.Enabled = true;
            checkBox7.Enabled = true;
            checkBox6.Enabled = true;
            checkBox5.Enabled = true;
            checkBox9.Enabled = true;

            button14.Text = "Start Logging";

            _writer1?.Flush();
            _writer1?.Close();
            _writer1 = null;

            _writer2?.Flush();
            _writer2?.Close();
            _writer2 = null;

            _writer3?.Flush();
            _writer3?.Close();
            _writer3 = null;

            _writer4?.Flush();
            _writer4?.Close();
            _writer4 = null;

            _writer5?.Flush();
            _writer5?.Close();
            _writer5 = null;

            OpenLogForms();
        }


        private async void _startWatchdog()
        {
            _watchdogCts?.Cancel();
            _watchdogCts = new CancellationTokenSource();

            CancellationToken token = _watchdogCts.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(500, token);

                    if (!_stopRequested)
                        return;

                    if (Environment.TickCount64 - _lastLogMessageTick >= 1000)
                    {
                        StopLogging();
                        return;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Watchdog cancelled: no problem
            }
        }

        private void OpenLogForms()
        {
            string[] logPaths =
            {
            _logFilePath1,
            _logFilePath2,
            _logFilePath3,
            _logFilePath4
        };

            foreach (string path in logPaths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Log form = new Log(path);
                    form.Show();
                }
            }
        }

        private void button15_Click(object sender, EventArgs e)
        {
            if (time_log)
            {
                time_log = false;
                button15.Image = Properties.Resources.Time_16x_cross2;
                toolTip1.SetToolTip(button15, "Timestamps off");
            }
            else
            {
                time_log = true;
                button15.Image = Properties.Resources.Time_color_16x;
                toolTip1.SetToolTip(button15, "Timestamps on");
            }
        }

        private void button16_Click(object sender, EventArgs e)
        {
            if (comboBox2.Text != "" || checkBox13.Checked)
                launch_graphs((int)numericUpDown1.Value, GetGraphPortName(1));
        }

        private void button17_Click(object sender, EventArgs e)
        {
            if (comboBox1.Text != "" || checkBox12.Checked)
                launch_graphs((int)numericUpDown2.Value, GetGraphPortName(2));
        }

        private void button18_Click(object sender, EventArgs e)
        {
            if (comboBox3.Text != "" || checkBox11.Checked)
                launch_graphs((int)numericUpDown3.Value, GetGraphPortName(3));
        }

        private void button19_Click(object sender, EventArgs e)
        {
            if (comboBox4.Text != "" || checkBox14.Checked)
                launch_graphs((int)numericUpDown4.Value, GetGraphPortName(4));
        }

        // The key must be identical both when opening the graphs and
        // when a TCP message arrives. If the ComboBox is empty but TCP is active,
        // we use a stable key for the logical port.
        private string GetGraphPortName(int logicalPort)
        {
            string selectedSerialPort = logicalPort switch
            {
                1 => comboBox2.Text,
                2 => comboBox1.Text,
                3 => comboBox3.Text,
                4 => comboBox4.Text,
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(selectedSerialPort))
                return selectedSerialPort.Trim();

            bool tcpEnabled = logicalPort switch
            {
                1 => checkBox13.Checked,
                2 => checkBox12.Checked,
                3 => checkBox11.Checked,
                4 => checkBox14.Checked,
                _ => false
            };

            return tcpEnabled ? $"TCP{logicalPort}" : string.Empty;
        }
        private void launch_graphs(int n_grafici, string nome_porta)
        {
            if (openGraphs.ContainsKey(nome_porta))
            {
                foreach (var form in openGraphs[nome_porta])
                {
                    form.Close();
                }
                openGraphs[nome_porta].Clear();
            }
            else
            {
                openGraphs[nome_porta] = new List<Form4>();
            }
            for (int i = 1; i <= n_grafici; i++)
            {
                var form = new Form4(this);
                form.Text = $"{nome_porta} {i}";  // window name
                form.Show();
                openGraphs[nome_porta].Add(form);
            }
        }

        private void comboBox2_Click(object sender, EventArgs e)
        {
            Searchports(1);
        }

        private void comboBox1_Click(object sender, EventArgs e)
        {
            Searchports(2);
        }

        private void comboBox3_Click(object sender, EventArgs e)
        {
            Searchports(3);
        }

        private void comboBox4_Click(object sender, EventArgs e)
        {
            Searchports(4);
        }

        private void comboBox2_MouseClick(object sender, MouseEventArgs e)
        {
        }

        public void merge_graphs1(string name, Form4 currentform)
        {
            if (!looking_for_merge)
            {
                merging_form = currentform;
                looking_for_merge = true;
            }
            else
            if (looking_for_merge)
            {
                CreateParasiticForm(merging_form, currentform);
                looking_for_merge = false;

            }

        }
        public void CreateParasiticForm(Form4 f1, Form4 f2)
        {
            var slaves = new List<Form4>();

            void Collect(Form4 f)
            {
                if (f.isMaster)
                {
                    foreach (var childEntry in f.children.Values)
                    {
                        if (childEntry.child != null)
                            slaves.Add(childEntry.child);
                    }
                }
                else
                {
                    slaves.Add(f);
                }
            }

            Collect(f1);
            Collect(f2);

            slaves = slaves.Distinct().ToList();


            var slaveNames = slaves.Select(s => s.Text).ToList();
            var master = new Form4(this, true)
            {
                Text = "Merged: " + string.Join(" ", slaveNames)
            };

            for (int i = 0; i < slaves.Count; i++)
            {
                var s = slaves[i];
                var color = GetDistinctColor(i, slaves.Count); // unique, distinguishable color
                var childEntry = new child_form(s, color, master.formsPlot1);
                master.children.Add(s, childEntry);
            }

            if (f1.isMaster) f1.Close();
            if (f2.isMaster) f2.Close();


            foreach (var s in slaves)
            {
                s.MasterForm = master;
                s.reset_merge();
                s.Visible = false;
                s.is_visible = false;
            }

            master.Show();
        }

        private System.Drawing.Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            switch (hi)
            {
                case 0: return System.Drawing.Color.FromArgb(255, v, t, p);
                case 1: return System.Drawing.Color.FromArgb(255, q, v, p);
                case 2: return System.Drawing.Color.FromArgb(255, p, v, t);
                case 3: return System.Drawing.Color.FromArgb(255, p, q, v);
                case 4: return System.Drawing.Color.FromArgb(255, t, p, v);
                default: return System.Drawing.Color.FromArgb(255, v, p, q);
            }
        }

        private System.Drawing.Color GetDistinctColor(int index, int total)
        {
            // Spread the hue evenly across the spectrum
            float hue = (360f / total) * index; // hue from 0 to 360
            float saturation = 0.7f; // high saturation
            float value = 0.9f;      // high brightness

            return ColorFromHSV(hue, saturation, value);
        }




        public void undo_merge()
        {
            looking_for_merge = false;
            merging_form = null;
        }
        volatile bool port1Enabled;
        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            port1Enabled = checkBox8.Checked;
        }
        volatile bool port2Enabled;

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            port2Enabled = checkBox7.Checked;
        }
        volatile bool port3Enabled;
        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            port3Enabled = checkBox6.Checked;
        }
        volatile bool port4Enabled;
        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            port4Enabled = checkBox5.Checked;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (historyIndex != 0 && textBox1.Focused && change_due_to_key == false)
            {
                historyIndex = 0;
            }
            change_due_to_key = false;
        }
        bool change_due_to_key = false;

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true; // avoid beep or default behavior

                if (commandHistory.Count == 0)
                    return;

                historyIndex++;

                if (historyIndex > commandHistory.Count)
                    historyIndex = 1;
                change_due_to_key = true;
                textBox1.Text = commandHistory[commandHistory.Count - historyIndex];
                textBox1.SelectionStart = textBox1.Text.Length;
            }
            else if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;

                if (commandHistory.Count == 0)
                    return;

                if (historyIndex > 0)
                {
                    historyIndex--;

                    if (historyIndex == 0)
                        textBox1.Text = "";
                    else
                    {
                        change_due_to_key = true;
                        textBox1.Text = commandHistory[commandHistory.Count - historyIndex];
                    }

                    textBox1.SelectionStart = textBox1.Text.Length;
                }
            }
        }


        bool stop_text = false;


        // GetPortNames() queries the system and can be slow: keep it off the UI thread.
        private async void remove_ports(object? sender, EventArgs e)
        {
            if (_scanningPorts)
                return;

            _scanningPorts = true;
            try
            {
                string[] availablePorts = await Task.Run(() => SerialPort.GetPortNames());

                if (IsDisposed)
                    return;

                CheckCombo(comboBox1, availablePorts);
                CheckCombo(comboBox2, availablePorts);
                CheckCombo(comboBox3, availablePorts);
                CheckCombo(comboBox4, availablePorts);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("remove_ports: " + ex.Message);
            }
            finally
            {
                _scanningPorts = false;
            }
        }

        private void CheckCombo(ComboBox combo, string[] availablePorts)
        {
            if (combo.SelectedItem != null)
            {
                string selectedPort = combo.SelectedItem.ToString();

                bool exists = availablePorts.Contains(selectedPort);

                if (!exists)
                {
                    combo.SelectedIndex = -1; // optional: deselect
                }
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (opn_p1 == true)
            {
                if (port1.PortName == comboBox2.SelectedItem?.ToString())
                    return;
                port1.Close();
                textBox3.Enabled = true;
                button3.Image = Properties.Resources.AddConnection;
                toolTip1.SetToolTip(button3, "Connect");
                opn_p1 = false;
                if (opn_p4 == false && opn_p2 == false && opn_p3 == false)
                {
                    string iconPath = Path.Combine(exeFolder, "icon_open.ico");
                    this.Icon = new Icon(iconPath);
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (opn_p2 == true)
            {
                if (port2.PortName == comboBox1.SelectedItem?.ToString())
                    return;
                port2.Close();
                textBox5.Enabled = true;
                button5.Image = Properties.Resources.AddConnection;
                toolTip1.SetToolTip(button5, "Connect");
                opn_p2 = false;
                if (opn_p4 == false && opn_p1 == false && opn_p3 == false)
                {
                    string iconPath = Path.Combine(exeFolder, "icon_open.ico");
                    this.Icon = new Icon(iconPath);
                }
            }
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (opn_p3 == true)
            {
                if (port3.PortName == comboBox3.SelectedItem?.ToString())
                    return;
                port3.Close();
                textBox4.Enabled = true;
                button4.Image = Properties.Resources.AddConnection;
                toolTip1.SetToolTip(button4, "Connect");
                opn_p3 = false;
                if (opn_p4 == false && opn_p2 == false && opn_p1 == false)
                {
                    string iconPath = Path.Combine(exeFolder, "icon_open.ico");
                    this.Icon = new Icon(iconPath);
                }
            }
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (opn_p4 == true)
            {
                if (port4.PortName == comboBox4.SelectedItem?.ToString())
                    return;
                port4.Close();
                textBox6.Enabled = true;
                button6.Image = Properties.Resources.AddConnection;
                toolTip1.SetToolTip(button6, "Connect");
                opn_p4 = false;
                if (opn_p1 == false && opn_p2 == false && opn_p3 == false)
                {
                    string iconPath = Path.Combine(exeFolder, "icon_open.ico");
                    this.Icon = new Icon(iconPath);
                }
            }
        }

        private void textBox2_Enter(object sender, EventArgs e)
        {
            if (is_locked)
                this.ActiveControl = null;
            else
                stop_text = true;
        }

        private void textBox2_Leave(object sender, EventArgs e)
        {
            if (!is_locked)
            {
                stop_text = false;
                FlushDisplay();
            }
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            this.ActiveControl = null;
        }

        private void button7_Click_1(object sender, EventArgs e)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logsDir = Path.Combine(baseDir, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = logsDir,
                UseShellExecute = true
            });
        }
        private void LoadSettings()
        {
            if (!File.Exists(settingsPath))
            {
                settings = new AppSettings();
                ApplySettingsToControls();
                return;
            }

            try
            {
                string json = File.ReadAllText(settingsPath);

                settings = JsonSerializer.Deserialize<AppSettings>(json)
                           ?? new AppSettings();

                ApplySettingsToControls();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading settings:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                settings = new AppSettings();
                ApplySettingsToControls();
            }
        }


        private void ApplySettingsToControls()
        {
            _loadingSettings = true;
            try
            {
                // =========================
                // CHECKBOX
                // =========================

                checkBox1.Checked = settings.check1;
                checkBox2.Checked = settings.check2;
                checkBox3.Checked = settings.check3;
                checkBox4.Checked = settings.check4;
                checkBox5.Checked = settings.check5;
                checkBox6.Checked = settings.check6;
                checkBox7.Checked = settings.check7;
                checkBox8.Checked = settings.check8;
                checkBox9.Checked = settings.check9;
                checkBox10.Checked = settings.check10;
                checkBox13.Checked = settings.UseTcp1;
                checkBox12.Checked = settings.UseTcp2;
                checkBox11.Checked = settings.UseTcp3;
                checkBox14.Checked = settings.UseTcp4;

                _tcpLocalPorts[0] = settings.TcpPort1 is >= 1 and <= 65535 ? settings.TcpPort1 : 5001;
                _tcpLocalPorts[1] = settings.TcpPort2 is >= 1 and <= 65535 ? settings.TcpPort2 : 5002;
                _tcpLocalPorts[2] = settings.TcpPort3 is >= 1 and <= 65535 ? settings.TcpPort3 : 5003;
                _tcpLocalPorts[3] = settings.TcpPort4 is >= 1 and <= 65535 ? settings.TcpPort4 : 5004;
                _baudRates[0] = settings.BaudRate1;
                _baudRates[1] = settings.BaudRate2;
                _baudRates[2] = settings.BaudRate3;
                _baudRates[3] = settings.BaudRate4;
                CambiaModalitaPorta(1, settings.UseTcp1);
                CambiaModalitaPorta(2, settings.UseTcp2);
                CambiaModalitaPorta(3, settings.UseTcp3);
                CambiaModalitaPorta(4, settings.UseTcp4);

                // =========================
                // BAUD RATE
                // =========================

                textBox3.Text = settings.BaudRate1;
                textBox5.Text = settings.BaudRate2;
                textBox4.Text = settings.BaudRate3;
                textBox6.Text = settings.BaudRate4;
                TextBox[] modeBoxes = { textBox3, textBox5, textBox4, textBox6 };
                for (int i = 0; i < 4; i++)
                {
                    if (_connectionModes[i] == ConnectionMode.Tcp)
                        modeBoxes[i].Text = _tcpLocalPorts[i].ToString();
                    else
                        modeBoxes[i].Text = _baudRates[i];
                }

                // =========================
                // NUMERIC UP DOWN
                // =========================

                numericUpDown1.Value = settings.graph_num1;
                numericUpDown2.Value = settings.graph_num2;
                numericUpDown3.Value = settings.graph_num3;
                numericUpDown4.Value = settings.graph_num4;


                // =========================
                // COMBOBOX
                // =========================

                if (settings.line_end_index >= 0 &&
                    settings.line_end_index < comboBox6.Items.Count)
                {
                    comboBox6.SelectedIndex = settings.line_end_index;
                }


                // =========================
                // VARIABLES
                // =========================

                time_log = settings.time_button;
                is_locked = settings.locked_button;


                if (!is_locked)
                {
                    toolTip1.SetToolTip(button12, "AutoScrolling off");
                    button12.Image = Properties.Resources.Unlock;
                    textBox2.ScrollBars = RichTextBoxScrollBars.Both;
                    //               textBox2.Enabled = true;

                }
                else
                {
                    button12.Image = Properties.Resources.Lock;
                    toolTip1.SetToolTip(button12, "AutoScrolling on");
                    textBox2.ScrollBars = RichTextBoxScrollBars.None;
                    //               textBox2.Enabled = false;
                }
                textBox2.SelectionStart = textBox2.Text.Length;
                textBox2.SelectionLength = 0;
                textBox2.ScrollToCaret();


                if (!time_log)
                {
                    button15.Image = Properties.Resources.Time_16x_cross2;
                    toolTip1.SetToolTip(button15, "Timestamps off");
                }
                else
                {
                    button15.Image = Properties.Resources.Time_color_16x;
                    toolTip1.SetToolTip(button15, "Timestamps on");
                }

                port1.Parity = ParseParity(settings.Parity1);
                port1.DataBits = settings.DataBits1;
                port1.StopBits = ParseStopBits(settings.StopBits1);

                if (settings.CustomBuffer1)
                    port1.ReadBufferSize = settings.ReadBufferSize1;


                port2.Parity = ParseParity(settings.Parity2);
                port2.DataBits = settings.DataBits2;
                port2.StopBits = ParseStopBits(settings.StopBits2);

                if (settings.CustomBuffer2)
                    port2.ReadBufferSize = settings.ReadBufferSize2;


                port3.Parity = ParseParity(settings.Parity3);
                port3.DataBits = settings.DataBits3;
                port3.StopBits = ParseStopBits(settings.StopBits3);

                if (settings.CustomBuffer3)
                    port3.ReadBufferSize = settings.ReadBufferSize3;


                port4.Parity = ParseParity(settings.Parity4);
                port4.DataBits = settings.DataBits4;
                port4.StopBits = ParseStopBits(settings.StopBits4);

                if (settings.CustomBuffer4)
                    port4.ReadBufferSize = settings.ReadBufferSize4;
            }
            finally
            {
                _loadingSettings = false;
            }

            // If the file contains duplicate ports among enabled TCP modes, fix them automatically.
            for (int logicalPort = 1; logicalPort <= 4; logicalPort++)
            {
                if (_connectionModes[logicalPort - 1] == ConnectionMode.Tcp)
                    EnsureUniqueTcpPort(logicalPort);
            }
        }
        private Parity ParseParity(string value)
        {
            return value switch
            {
                "Odd" => Parity.Odd,
                "Even" => Parity.Even,
                _ => Parity.None
            };
        }
        private StopBits ParseStopBits(string value)
        {
            return value switch
            {
                "None" => StopBits.None,
                "One" => StopBits.One,
                "OnePointFive" => StopBits.OnePointFive,
                "Two" => StopBits.Two,
                _ => StopBits.One
            };
        }

        private void SaveSettings()
        {
            try
            {
                // =========================
                // CHECKBOX
                // =========================

                settings.check1 = checkBox1.Checked;
                settings.check2 = checkBox2.Checked;
                settings.check3 = checkBox3.Checked;
                settings.check4 = checkBox4.Checked;
                settings.check5 = checkBox5.Checked;
                settings.check6 = checkBox6.Checked;
                settings.check7 = checkBox7.Checked;
                settings.check8 = checkBox8.Checked;
                settings.check9 = checkBox9.Checked;

                settings.check10 = checkBox10.Checked;
                // =========================
                // BAUD RATE
                // =========================

                TextBox[] valueBoxes = { textBox3, textBox5, textBox4, textBox6 };
                for (int i = 0; i < 4; i++)
                {
                    if (_connectionModes[i] == ConnectionMode.Serial)
                        _baudRates[i] = valueBoxes[i].Text;
                    else if (int.TryParse(valueBoxes[i].Text, out int tcpPort) && tcpPort >= 1 && tcpPort <= 65535)
                        _tcpLocalPorts[i] = tcpPort;
                }
                settings.BaudRate1 = _baudRates[0];
                settings.BaudRate2 = _baudRates[1];
                settings.BaudRate3 = _baudRates[2];
                settings.BaudRate4 = _baudRates[3];
                settings.UseTcp1 = checkBox13.Checked;
                settings.UseTcp2 = checkBox12.Checked;
                settings.UseTcp3 = checkBox11.Checked;
                settings.UseTcp4 = checkBox14.Checked;
                settings.TcpPort1 = _tcpLocalPorts[0];
                settings.TcpPort2 = _tcpLocalPorts[1];
                settings.TcpPort3 = _tcpLocalPorts[2];
                settings.TcpPort4 = _tcpLocalPorts[3];


                // =========================
                // NUMERIC UP DOWN
                // =========================

                settings.graph_num1 = (int)numericUpDown1.Value;
                settings.graph_num2 = (int)numericUpDown2.Value;
                settings.graph_num3 = (int)numericUpDown3.Value;
                settings.graph_num4 = (int)numericUpDown4.Value;


                // =========================
                // COMBOBOX
                // =========================

                settings.line_end_index = comboBox6.SelectedIndex;


                // =========================
                // VARIABLES
                // =========================

                settings.time_button = time_log;
                settings.locked_button = is_locked;




                // PORTA 1
                settings.Parity1 = getparity(port1);
                settings.DataBits1 = port1.DataBits;
                settings.StopBits1 = getstopbits(port1);
                settings.ReadBufferSize1 = port1.ReadBufferSize;


                // PORTA 2
                settings.Parity2 = getparity(port2);
                settings.DataBits2 = port2.DataBits;
                settings.StopBits2 = getstopbits(port2);
                settings.ReadBufferSize2 = port2.ReadBufferSize;


                // PORTA 3
                settings.Parity3 = getparity(port3);
                settings.DataBits3 = port3.DataBits;
                settings.StopBits3 = getstopbits(port3);
                settings.ReadBufferSize3 = port3.ReadBufferSize;


                // PORTA 4
                settings.Parity4 = getparity(port4);
                settings.DataBits4 = port4.DataBits;
                settings.StopBits4 = getstopbits(port4);
                settings.ReadBufferSize4 = port4.ReadBufferSize;


                // =========================
                // SERIALIZATION
                // =========================

                string json = JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving settings:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _uiTimer?.Stop();
            port_timeout?.Stop();
            _watchdogCts?.Cancel();

            for (int port = 1; port <= 4; port++)
                StopTcpPort(port);

            try { _writer1?.Flush(); _writer2?.Flush(); _writer3?.Flush(); _writer4?.Flush(); _writer5?.Flush(); } catch { }

            SaveSettings();
        }

        private void label5_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void label5_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files =
                (string[])e.Data.GetData(DataFormats.FileDrop);

            // Accept only a single file
            if (files.Length != 1)
                return;

            string path = files[0];

            if (!File.Exists(path))
                return;

            Log form = new Log(path);
            form.Show();
        }

        private void button20_Click(object sender, EventArgs e)
        {
            var form = new Form6(this, settings.log_message1, settings.log_message2, settings.log_check);
            form.Show();
        }
        public void Set_logging_messages(string mes1, string mes2, bool check)
        {
            settings.log_message1 = mes1;
            settings.log_message2 = mes2;
            settings.log_check = check;
        }


        //TCP update


        private void SetPortMode(int logicalPort, ConnectionMode mode)
        {
            int index = logicalPort - 1;

            if (index < 0 || index >= 4)
                throw new ArgumentOutOfRangeException(nameof(logicalPort));

            if (_connectionModes[index] == mode)
                return;

            StopTcpPort(logicalPort);
            _connectionModes[index] = mode;

            if (mode == ConnectionMode.Tcp)
                StartTcpPort(logicalPort);
        }

        private void StartTcpPort(int logicalPort)
        {
            int index = logicalPort - 1;

            try
            {
                var listener = new TcpListener(
                    IPAddress.Any,
                    _tcpLocalPorts[index]);

                listener.Start();

                var cts = new CancellationTokenSource();

                _tcpListeners[index] = listener;
                _tcpCts[index] = cts;

                _ = Task.Run(() =>
                    TcpAcceptLoopAsync(logicalPort, listener, cts.Token));

                aggiungi_a_textbox2(
                    $"TCP listening on port {_tcpLocalPorts[index]}",
                    "System",
                    Color.DarkGreen);
            }
            catch (Exception ex)
            {
                // Keep TCP mode selected so the user can fix
                // the entered port and retry without having to re-check the checkbox.
                aggiungi_a_textbox2(
                    $"Error starting TCP on port {logicalPort}: {ex.Message}",
                    "Error",
                    Color.Red);
            }
        }

        private void StopTcpPort(int logicalPort)
        {
            int index = logicalPort - 1;

            try { _tcpCts[index]?.Cancel(); } catch { }
            try { _tcpListeners[index]?.Stop(); } catch { }
            try { _tcpClients[index]?.Close(); } catch { }

            _tcpCts[index]?.Dispose();

            _tcpCts[index] = null;
            _tcpListeners[index] = null;
            _tcpClients[index] = null;
            _tcpStreams[index] = null;
            _tcpBuffers[index].Clear();
        }

        private async Task TcpAcceptLoopAsync(
            int logicalPort,
            TcpListener listener,
            CancellationToken token)
        {
            int index = logicalPort - 1;

            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;

                try
                {
                    client = await listener.AcceptTcpClientAsync();

                    if (token.IsCancellationRequested)
                    {
                        client.Close();
                        break;
                    }

                    // No Nagle delay and send with a timeout: a write
                    // from the UI thread cannot block the interface for long.
                    client.NoDelay = true;
                    client.SendTimeout = 1000;
                    try { client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); } catch { }

                    _tcpClients[index] = client;
                    _tcpStreams[index] = client.GetStream();
                    _tcpBuffers[index].Clear();

                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                            aggiungi_a_textbox2(
                                $"ESP connected to TCP port {logicalPort}",
                                "System",
                                Color.DarkGreen)));
                    }

                    await TcpReceiveLoopAsync(logicalPort, client, token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested &&
                        !IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                            aggiungi_a_textbox2(
                                $"TCP error on port {logicalPort}: {ex.Message}",
                                "Error",
                                Color.Red)));
                    }
                }
                finally
                {
                    if (ReferenceEquals(_tcpClients[index], client))
                    {
                        _tcpStreams[index] = null;
                        _tcpClients[index] = null;
                        _tcpBuffers[index].Clear();
                    }

                    try { client?.Close(); } catch { }
                }
            }
        }

        private async Task TcpReceiveLoopAsync(
            int logicalPort,
            TcpClient client,
            CancellationToken token)
        {
            int index = logicalPort - 1;
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            while (!token.IsCancellationRequested && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(
                    buffer, 0, buffer.Length, token);

                if (bytesRead == 0)
                    break;

                // The serial code uses ASCII: keep the same format.
                string received = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                var batch = new List<string>();

                foreach (char c in received)
                {
                    if (c == '\r' || c == '\n')
                    {
                        if (_tcpBuffers[index].Length == 0)
                            continue;

                        string message = _tcpBuffers[index].ToString().Trim();
                        _tcpBuffers[index].Clear();

                        if (message.Length > 0)
                            batch.Add(message);
                    }
                    else
                    {
                        _tcpBuffers[index].Append(c);

                        // Prevent a protocol error from filling memory.
                        if (_tcpBuffers[index].Length > 30000)
                            _tcpBuffers[index].Clear();
                    }
                }

                ProcessTcpMessages(logicalPort, batch);
            }
        }



        private void ProcessTcpMessages(int logicalPort, List<string> messages)
        {
            // Called from the TCP receive task: no UI access here.
            // The port name (which requires reading the controls) is resolved
            // on the UI thread in ProcessRx (PortName = null).
            Color color = logicalPort switch
            {
                1 => Color.Red,
                2 => Color.Blue,
                3 => Color.Orange,
                4 => Color.Green,
                _ => Color.Black
            };

            EnqueueBatch(messages, null, logicalPort, color);
        }



        private void SendToLogicalPort(
            int logicalPort,
            bool serialIsOpen,
            bool selected,
            SerialPort serialPort,
            string data)
        {
            if (!selected)
                return;

            int index = logicalPort - 1;

            if (_connectionModes[index] == ConnectionMode.Serial)
            {
                if (!serialIsOpen)
                    return;

                try
                {
                    serialPort.Write(data);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Serial send on port {logicalPort}: {ex.Message}");
                }

                return;
            }

            NetworkStream? stream = _tcpStreams[index];

            if (stream == null || _tcpClients[index]?.Connected != true)
                return;

            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(data);

                lock (_tcpSendLocks[index])
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TCP send on port {logicalPort}: {ex.Message}");
            }
        }

        private void checkBox13_CheckedChanged(object sender, EventArgs e)
        {
            AggiornaModalitaDaCheckBox(1, checkBox13.Checked, label1, textBox3, comboBox2);
            if (!_loadingSettings) SaveSettings();
        }

        private void checkBox12_CheckedChanged(object sender, EventArgs e)
        {
            AggiornaModalitaDaCheckBox(2, checkBox12.Checked, label2, textBox5, comboBox1);
            if (!_loadingSettings) SaveSettings();
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            AggiornaModalitaDaCheckBox(3, checkBox11.Checked, label3, textBox4, comboBox3);
            if (!_loadingSettings) SaveSettings();
        }

        private void checkBox14_CheckedChanged(object sender, EventArgs e)
        {
            AggiornaModalitaDaCheckBox(4, checkBox14.Checked, label4, textBox6, comboBox4);
            if (!_loadingSettings) SaveSettings();
        }

        private void AggiornaModalitaDaCheckBox(int logicalPort, bool usaTcp, Label label, TextBox portOrBaudBox, ComboBox serialPortCombo)
        {
            CambiaModalitaPorta(logicalPort, usaTcp);
            serialPortCombo.Enabled = !usaTcp;
            label.Text = usaTcp ? "port" : "baud";
            portOrBaudBox.Enabled = true;
        }



        // Nuova interfaccia
        private void button21_Click(object sender, EventArgs e)
        {
            var form = new Form7(this, true);
            form.Show();
        }


        // Apri interfaccia in modalità normale
        private void button22_Click(object sender, EventArgs e)
        {
            string interfacesPath = Path.Combine(
                AppContext.BaseDirectory,
                "interfaces"
            );

            Directory.CreateDirectory(interfacesPath);

            using OpenFileDialog dialog = new OpenFileDialog
            {
                InitialDirectory = interfacesPath,
                Filter = "Interfacce SPM (*.spm)|*.spm",
                Title = "Apri interfaccia",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                OpenInterface(dialog.FileName, false);
            }
        }


        // Apri interfaccia in modalità editor
        private void button23_Click(object sender, EventArgs e)
        {
            string interfacesPath = Path.Combine(
                AppContext.BaseDirectory,
                "interfaces"
            );

            Directory.CreateDirectory(interfacesPath);

            using OpenFileDialog dialog = new OpenFileDialog
            {
                InitialDirectory = interfacesPath,
                Filter = "Interfacce SPM (*.spm)|*.spm",
                Title = "Apri interfaccia da modificare",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                OpenInterface(dialog.FileName, true);
            }
        }

        public void OpenInterface(string filePath, bool isedit)
        {
            var form = new Form7(this, isedit);
            form.Text = Path.GetFileNameWithoutExtension(filePath);
            form.LoadDocument(filePath);
            form.Show();
        }

        string current_sequence;

        private bool sequence_running = false;
        private bool sequence_paused = false;

        private readonly ManualResetEventSlim sequencePauseEvent =
            new ManualResetEventSlim(true);

        private async void button24_Click(object sender, EventArgs e)
        {
            // Se la sequenza è già in esecuzione
            if (sequence_running)
            {
                if (!sequence_paused)
                {
                    // PAUSE
                    sequence_paused = true;
                    sequencePauseEvent.Reset();
                    button24.Image = Properties.Resources.Play;
                }
                else
                {
                    // RESUME
                    sequence_paused = false;
                    sequencePauseEvent.Set();
                    button24.Image = Properties.Resources.Pause;
                }

                return;
            }

            // Se non è stata selezionata nessuna sequenza
            if (string.IsNullOrEmpty(current_sequence) ||
                !File.Exists(current_sequence))
            {
                MessageBox.Show("Please select a sequence");
                return;
            }

            // START
            sequence_running = true;
            sequence_paused = false;
            sequencePauseEvent.Set();

            button24.Image = Properties.Resources.Pause;

            try
            {
                await Task.Run(() => ExecuteSequence(current_sequence));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                sequence_running = false;
                sequence_paused = false;
                sequencePauseEvent.Set();

                button24.Image = Properties.Resources.Play;
            }
        }


        private void button25_Click(object sender, EventArgs e)
        {
            string sequencesPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "sequences"
            );

            // Se non esiste, la crea
            if (!Directory.Exists(sequencesPath))
                Directory.CreateDirectory(sequencesPath);

            // Dialog per selezionare il file
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = sequencesPath;
                openFileDialog.Title = "Select a sequence";
                openFileDialog.Filter = "All files (*.*)|*.*";
                openFileDialog.CheckFileExists = true;
                openFileDialog.Multiselect = false;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Assegna il nome del file al testo del bottone
                    button25.Text = Path.GetFileName(openFileDialog.FileName);
                    current_sequence = openFileDialog.FileName;
                }
            }
        }

        private void ExecuteSequence(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                // Aspetta se la sequenza è in pausa
                sequencePauseEvent.Wait();

                string line = lines[i].Trim();

                // Salta righe vuote
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // WAIT COMMAND
                Match waitMatch = Regex.Match(
                    line,
                    @"^\{wait\((\d+)\)\}$",
                    RegexOptions.IgnoreCase
                );

                if (waitMatch.Success)
                {
                    int milliseconds = int.Parse(waitMatch.Groups[1].Value);

                    SequenceDelay(milliseconds);

                    continue;
                }

                // NORMAL COMMAND
                Match commandMatch = Regex.Match(
                    line,
                    @"^\{([^}]+)\}(.*?)\s*\{(NLCR|NL|CR|N)\}\s*$",
                    RegexOptions.IgnoreCase
                );

                if (!commandMatch.Success)
                {
                    Debug.WriteLine($"Invalid sequence line {i + 1}: {line}");
                    continue;
                }

                // Estrae le porte
                string portsString = commandMatch.Groups[1].Value;

                // Estrae il messaggio
                string message = commandMatch.Groups[2].Value.TrimEnd();

                // Estrae il terminatore
                string terminator = commandMatch.Groups[3].Value.ToUpper();

                // Converte il terminatore
                switch (terminator)
                {
                    case "NL":
                        message += "\n";
                        break;

                    case "CR":
                        message += "\r";
                        break;

                    case "NLCR":
                        message += "\r\n";
                        break;

                    case "N":
                        break;
                }

                // Converte le porte in array di interi
                int[] ports = portsString
                    .Split(',')
                    .Select(p => int.Parse(p.Trim()))
                    .ToArray();

                // Invio alle porte selezionate
                sequencePauseEvent.Wait();

                SendSequenceToPorts(ports, message);
            }
        }

        private void SequenceDelay(int milliseconds)
        {
            int remaining = milliseconds;

            while (remaining > 0)
            {
                // Se in pausa, aspetta il resume
                sequencePauseEvent.Wait();

                // Misura solo il tempo effettivamente trascorso
                Stopwatch sw = Stopwatch.StartNew();

                Thread.Sleep(Math.Min(remaining, 10));

                sw.Stop();

                remaining -= (int)sw.ElapsedMilliseconds;
            }
        }

        private void SendSequenceToPorts(int[] ports, string message)
        {
            bool[] targetPorts = new bool[4];

            foreach (int port in ports)
            {
                if (port >= 1 && port <= 4)
                    targetPorts[port - 1] = true;
            }

            if (targetPorts[0])
                SendToLogicalPort(1, opn_p1, true, port1, message);

            if (targetPorts[1])
                SendToLogicalPort(2, opn_p2, true, port2, message);

            if (targetPorts[2])
                SendToLogicalPort(3, opn_p3, true, port3, message);

            if (targetPorts[3])
                SendToLogicalPort(4, opn_p4, true, port4, message);

            // Aggiornamento GUI sul thread principale
            if (textBox2.InvokeRequired)
            {
                textBox2.BeginInvoke(new Action(() =>
                {
                    aggiungi_a_textbox2(message, "Sequence", Color.Purple);
                    LogUserInput(message);
                }));
            }
            else
            {
                aggiungi_a_textbox2(message, "Sequence", Color.Purple);
                LogUserInput(message);
            }
        }

    }




    public class Script
    {
        public string name { get; set; }
        public string key;
        public int trigger;
        public bool p1;
        public bool p2;
        public bool p3;
        public bool p4;
        public string reply;
        public bool p1_w;
        public bool p2_w;
        public bool p3_w;
        public bool p4_w;
        public string keypress;
        public string path;
        public bool active;
        public int lineEndingIndex { get; set; } = 1; // Default a 1 (NL)
    }

    public class Textbox_entry
    {
        public string inputText;
        public string usr;
        public Color color;
        public string line;
        public Textbox_entry(string in_text, string lin, string user, Color clr)
        {
            inputText = in_text;
            line = lin;
            usr = user;
            color = clr;
        }
    }


    public class AppSettings
    {
        public bool check1 { get; set; } = false;
        public bool check2 { get; set; } = false;
        public bool check3 { get; set; } = false;
        public bool check4 { get; set; } = false;
        public bool check5 { get; set; } = false;
        public bool check6 { get; set; } = false;
        public bool check7 { get; set; } = false;
        public bool check8 { get; set; } = false;
        public bool check9 { get; set; } = false;
        public bool check10 { get; set; } = false;

        public bool time_button { get; set; } = false;
        public bool locked_button { get; set; } = false;

        public string BaudRate1 { get; set; } = "9600";
        public string BaudRate2 { get; set; } = "9600";
        public string BaudRate3 { get; set; } = "9600";
        public string BaudRate4 { get; set; } = "9600";

        public bool UseTcp1 { get; set; } = false;
        public bool UseTcp2 { get; set; } = false;
        public bool UseTcp3 { get; set; } = false;
        public bool UseTcp4 { get; set; } = false;
        public int TcpPort1 { get; set; } = 5001;
        public int TcpPort2 { get; set; } = 5002;
        public int TcpPort3 { get; set; } = 5003;
        public int TcpPort4 { get; set; } = 5004;

        public int graph_num1 { get; set; } = 1;
        public int graph_num2 { get; set; } = 1;
        public int graph_num3 { get; set; } = 1;
        public int graph_num4 { get; set; } = 1;

        public int line_end_index { get; set; } = 0;


        // log messages
        public string log_message1 { get; set; } = "";
        public string log_message2 { get; set; } = "";

        public bool log_check { get; set; } = false;

        // PORTA 1
        public string Parity1 { get; set; } = "None";
        public int DataBits1 { get; set; } = 8;
        public string StopBits1 { get; set; } = "One";
        public bool CustomBuffer1 { get; set; } = false;
        public int ReadBufferSize1 { get; set; } = 4096;


        // PORTA 2
        public string Parity2 { get; set; } = "None";
        public int DataBits2 { get; set; } = 8;
        public string StopBits2 { get; set; } = "One";
        public bool CustomBuffer2 { get; set; } = false;
        public int ReadBufferSize2 { get; set; } = 4096;


        // PORTA 3
        public string Parity3 { get; set; } = "None";
        public int DataBits3 { get; set; } = 8;
        public string StopBits3 { get; set; } = "One";
        public bool CustomBuffer3 { get; set; } = false;
        public int ReadBufferSize3 { get; set; } = 4096;


        // PORTA 4
        public string Parity4 { get; set; } = "None";
        public int DataBits4 { get; set; } = 8;
        public string StopBits4 { get; set; } = "One";
        public bool CustomBuffer4 { get; set; } = false;
        public int ReadBufferSize4 { get; set; } = 4096;

    }


}
