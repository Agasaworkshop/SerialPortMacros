using System.Drawing.Text;
using System.Dynamic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsInput;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using System.Runtime.InteropServices;
using System;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using System.Text.RegularExpressions;
using System.Globalization;

namespace SerialPortMacros
{
    public partial class Form1 : Form
    {
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
        public Dictionary<string, List<Form4>> openGraphs = new Dictionary<string, List<Form4>>();


        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Searchports();
            setports();
            ScanScripts();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "Start")
            {
                button1.Text = "Stop";
                is_running = true;
                ScanScripts();
            }
            else
            {
                button1.Text = "Start";
                is_running = false;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ScanScripts();
            Form3 macro_form = new Form3(this);
            macro_form.ShowDialog();
        }
        private void textBox1_Keypress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                string inputText = textBox1.Text;
                aggiungi_a_textbox2(inputText, "You", Color.Black);
                textBox1.Text = "";
                Sendtoports(inputText);
                if (logging)
                {
                    Task.Run(() => LogUserInput(inputText));
                }
            }
        }
        private void LogUserInput(string inputText)
        {
            string log_input_text = $"You: {inputText}";

            if (time_log)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                log_input_text = $"{timestamp} | You: {inputText}";
            }

            if (!checkBox9.Checked)
                return;

            if (checkBox8.Checked) _writer1.WriteLine(log_input_text);
            if (checkBox7.Checked) _writer2.WriteLine(log_input_text);
            if (checkBox6.Checked) _writer3.WriteLine(log_input_text);
            if (checkBox5.Checked) _writer4.WriteLine(log_input_text);
            if (multilog) _writer5.WriteLine(log_input_text);
        }


        private void button3_Click(object sender, EventArgs e)
        {
            if (opn_p1 == false)
            {
                int baud = int.Parse(textBox3.Text);
                string name = comboBox2.Text;
                if (Openport(baud, port1, name) == 1)
                {
                    button3.Image = Properties.Resources.Disconnect;
                    opn_p1 = true;
                }
            }
            else
            {
                port1.Close();
                button3.Image = Properties.Resources.AddConnection;
                opn_p1 = false;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (opn_p3 == false)
            {
                int baud = int.Parse(textBox4.Text);
                string name = comboBox3.Text;
                if (Openport(baud, port3, name) == 1)
                {
                    button4.Image = Properties.Resources.Disconnect;
                    opn_p3 = true;
                }
            }
            else
            {
                port3.Close();
                button4.Image = Properties.Resources.AddConnection;
                opn_p3 = false;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (opn_p2 == false)
            {
                int baud = int.Parse(textBox5.Text);
                string name = comboBox1.Text;
                if (Openport(baud, port2, name) == 1)
                {
                    button5.Image = Properties.Resources.Disconnect;
                    opn_p2 = true;
                }
            }
            else
            {
                port2.Close();
                button5.Image = Properties.Resources.AddConnection;
                opn_p2 = false;
            }
        }
        private void button6_Click(object sender, EventArgs e)
        {
            if (opn_p4 == false)
            {
                int baud = int.Parse(textBox6.Text);
                string name = comboBox4.Text;
                if (Openport(baud, port4, name) == 1)
                {
                    button6.Image = Properties.Resources.Disconnect;
                    opn_p4 = true;
                }
            }
            else
            {
                port4.Close();
                button6.Image = Properties.Resources.AddConnection;
                opn_p4 = false;
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Searchports();
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
        private void Searchports()
        {
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            comboBox3.Items.Clear();
            comboBox4.Items.Clear();
            string[] availablePorts = SerialPort.GetPortNames();
            foreach (string portName in availablePorts)
            {
                comboBox1.Items.Add(portName);
                comboBox2.Items.Add(portName);
                comboBox3.Items.Add(portName);
                comboBox4.Items.Add(portName);
            }
        }
        const int MaxChars = 500_000;

        private void aggiungi_a_textbox2(string inputText, string usr, Color color)
        {
            string line;
            if (time_log)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                line = $"{timestamp} | {usr}: {inputText}";
            }
            else
                line = $"{usr}: {inputText}";

            // Testo da aggiungere (con newline se serve)
            string toAppend = (textBox2.TextLength == 0)
                ? line
                : Environment.NewLine + line;

            // --- LIMITE DI CARATTERI ---
            int newLength = textBox2.TextLength + toAppend.Length;
            if (newLength > MaxChars)
            {
                int excess = newLength - MaxChars;

                // Rimuove dall'inizio i caratteri in eccesso
                textBox2.Select(0, excess);
                textBox2.SelectedText = "";
            }

            // --- APPEND COLORATO ---
            textBox2.SelectionStart = textBox2.TextLength;
            textBox2.SelectionLength = 0;
            textBox2.SelectionColor = color;

            textBox2.AppendText(toAppend);

            if (is_locked)
                textBox2.ScrollToCaret();
        }


        private void text_color(int start, int end, Color color)
        {
            textBox2.Select(start, end);
            textBox2.SelectionColor = color;
            textBox2.Select(start + end, start + end);
        }
        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        private int Openport(int baud, SerialPort c_port, string name)
        {
            if (baud > 0 && comboBox2.Text != "")
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

            if (opn_p1 && checkBox1.Checked)
                port1.Write(output);
            if (opn_p2 && checkBox2.Checked)
                port2.Write(output);
            if (opn_p3 && checkBox3.Checked)
                port3.Write(output);
            if (opn_p4 && checkBox4.Checked)
                port4.Write(output);
        }
        private async void SerialPort_DataReceived1(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port1, 1, true, false, false, false, opn_p1, checkBox1, Color.Red, mes1, checkBox8.Checked);
        }
        private async void SerialPort_DataReceived2(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port2, 2, false, true, false, false, opn_p2, checkBox2, Color.Blue, mes2, checkBox7.Checked);
        }
        private async void SerialPort_DataReceived3(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port3, 3, false, false, true, false, opn_p3, checkBox3, Color.Yellow, mes3, checkBox6.Checked);
        }
        private async void SerialPort_DataReceived4(object sender, SerialDataReceivedEventArgs e)
        {
            Serial_port_data2(port4, 4, false, false, false, true, opn_p4, checkBox4, Color.Green, mes4, checkBox5.Checked);
        }
        public void ScanScripts()
        {
            Directory.CreateDirectory("./scripts");
            string[] paths = Directory.GetFiles("./scripts");
            int i = 0;
            scripts = new Script[paths.Length];
            foreach (string path in paths)
            {
                string name = path.Substring(10);
                scripts[i] = new Script();
                scripts[i].name = name;
                scripts[i].path = path;
                string[] lines = File.ReadAllLines(path);
                scripts[i].key = lines[0];
                scripts[i].trigger = int.Parse(lines[1]);
                scripts[i].p1 = bool.Parse(lines[2]);
                scripts[i].p2 = bool.Parse(lines[3]);
                scripts[i].p3 = bool.Parse(lines[4]);
                scripts[i].p4 = bool.Parse(lines[5]);
                if (scripts[i].trigger == 1)
                {
                    scripts[i].keypress = lines[6];
                    scripts[i].active = bool.Parse(lines[7]);
                }
                else if (scripts[i].trigger == 2)
                {
                    scripts[i].reply = lines[6];
                    scripts[i].p1_w = bool.Parse(lines[7]);
                    scripts[i].p2_w = bool.Parse(lines[8]);
                    scripts[i].p3_w = bool.Parse(lines[9]);
                    scripts[i].p4_w = bool.Parse(lines[10]);
                    scripts[i].active = bool.Parse(lines[11]);
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
                    if ((script.active && ((script.key == data) || ((script.key == "===") && (data[0] == '=') && (data[1] == '=') && (data[2] == '='))) && ((p1 == script.p1 == true) || (p2 == script.p2 == true) || (p3 == script.p3 == true) || (p4 == script.p4 == true))))
                        if (script.trigger == 1)
                        {
                            Thread inputThread = new Thread(() =>
                            {
                                scr_KeyPress(script.keypress, data);
                            });
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
            if (c_script.p1_w == true && opn_p1 && checkBox1.Checked)
            {
                port1.WriteLine(c_script.reply);
            }
            if (c_script.p2_w == true && opn_p2 && checkBox2.Checked)
            {
                port2.WriteLine(c_script.reply);
            }
            if (c_script.p3_w == true && opn_p3 && checkBox3.Checked)
            {
                port3.WriteLine(c_script.reply);
            }
            if (c_script.p4_w == true && opn_p4 && checkBox4.Checked) ;
        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (is_locked)
            {
                is_locked = false;
                button12.Image = Properties.Resources.Unlock;

            }
            else
            {
                is_locked = true;
                button12.Image = Properties.Resources.Lock;
            }

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
            string data = port.ReadExisting();
            if (string.IsNullOrEmpty(data))
                return;

            sb.Append(data); // accumula tutto

            string buffer = sb.ToString();
            int lastProcessedIndex = 0;

            for (int i = 0; i < buffer.Length; i++)
            {
                // controlla se il carattere è terminatore
                if (buffer[i] == '\n' || buffer[i] == '\r')
                {
                    string mess = buffer.Substring(lastProcessedIndex, i - lastProcessedIndex).Trim();
                    lastProcessedIndex = i + 1; // salta il terminatore

                    if (string.IsNullOrEmpty(mess))
                        continue; // evita stringhe vuote

                    // Invoca UI
                    this.Invoke((Action)(() =>
                    {
                        if (opn)
                        {
                            if (logging)
                                write_logs(po1, po2, po3, po4, mess, port.PortName);

                            aggiungi_a_textbox2(mess, port.PortName, color);
                            scriptcheck(mess, po1, po2, po3, po4);
                        }
                    }));
                    if (opn && openGraphs.ContainsKey(port.PortName))
                    {
                        string messCopy = mess; // copia sicura
                        var forms = openGraphs[port.PortName];

                        // Esegui in un thread separato solo il parsing
                        Task.Run(() =>
                        {
                            var nums = Regex.Matches(messCopy, @"-?\d+(\.\d+)?")
                                            .Cast<Match>()
                                            .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
                                            .ToList();

                            for (int i = 0; i < forms.Count; i++)
                            {
                                if (i < nums.Count)
                                {
                                    forms[i].AddDataPoint(nums[i]);
                                }
                            }
                        });
                    }

                }
            }

            // Rimuove solo la parte processata dal buffer, lascia eventuali dati parziali
            sb.Remove(0, lastProcessedIndex);
        }


        private void write_logs(bool po1, bool po2, bool po3, bool po4, string line, string usr)
        {
            string multilog_line = $"{usr}: {line}";
            if (time_log)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                multilog_line = $"{timestamp} | {usr}: {line}";
                line = $"{timestamp} | {line}";
            }
            if (po1 && checkBox8.Checked)
            {
                _writer1.WriteLine(line);
            }
            if (po2 && checkBox7.Checked)
            {
                _writer2.WriteLine(line);
            }
            if (po3 && checkBox6.Checked)
            {
                _writer3.WriteLine(line);
            }
            if (po4 && checkBox5.Checked)
            {
                _writer4.WriteLine(line);
            }
            if (multilog)
            {
                _writer5.WriteLine(multilog_line);
            }
        }
        private void button14_Click(object sender, EventArgs e)
        {
            if (logging == false)
            {
                checkBox8.Enabled = false;
                checkBox7.Enabled = false;
                checkBox6.Enabled = false;
                checkBox5.Enabled = false;
                checkBox9.Enabled = false;
                logging = true;
                button14.Text = "Logging...";
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
                    _writer1.AutoFlush = true;
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
                    _writer2.AutoFlush = true;
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
                    _writer3.AutoFlush = true;
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
                    _writer4.AutoFlush = true;
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
                    _writer5.AutoFlush = true;
                }
                else
                    multilog = false;
            }
            else
            {
                checkBox8.Enabled = true;
                checkBox7.Enabled = true;
                checkBox6.Enabled = true;
                checkBox5.Enabled = true;
                checkBox9.Enabled = true;
                logging = false;
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
            }
        }

        private void button15_Click(object sender, EventArgs e)
        {
            if (time_log)
            {
                time_log = false;
                button15.Image = Properties.Resources.Time_16x_cross2;
            }
            else
            {
                time_log = true;
                button15.Image = Properties.Resources.Time_color_16x;

            }
        }

        private void button16_Click(object sender, EventArgs e)
        {
            if (comboBox2.Text != "")
                launch_graphs((int)numericUpDown1.Value, comboBox2.Text);
        }

        private void button17_Click(object sender, EventArgs e)
        {
            if (comboBox2.Text != "")
                launch_graphs((int)numericUpDown2.Value, comboBox1.Text);
        }

        private void button18_Click(object sender, EventArgs e)
        {
            if (comboBox2.Text != "")
                launch_graphs((int)numericUpDown3.Value, comboBox3.Text);
        }

        private void button19_Click(object sender, EventArgs e)
        {
            if (comboBox2.Text != "")
                launch_graphs((int)numericUpDown4.Value, comboBox4.Text);
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
                var form = new Form4();
                form.Text = $"{nome_porta} {i}";  // nome della finestra
                form.Show();
                openGraphs[nome_porta].Add(form);
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
    }
}