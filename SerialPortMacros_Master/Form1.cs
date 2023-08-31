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
        public Script[] scripts;
        public bool is_locked = true;

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
            }
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
            textBox3.Text = "9600";
            port1.Parity = Parity.None;
            port1.DataBits = 8;
            port1.StopBits = StopBits.One;
            port2.BaudRate = 9600;
            textBox5.Text = "9600";
            port2.Parity = Parity.None;
            port2.DataBits = 8;
            port2.StopBits = StopBits.One;
            port3.BaudRate = 9600;
            textBox4.Text = "9600";
            port3.Parity = Parity.None;
            port3.DataBits = 8;
            port3.StopBits = StopBits.One;
            port4.BaudRate = 9600;
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
        private void aggiungi_a_textbox2(string inputText, string usr, Color color)
        {
            textBox2.SelectionStart = textBox2.TextLength;
            textBox2.SelectionLength = 0;
            textBox2.SelectionColor = color;
            if (textBox2.Text == "")
                textBox2.Text = $"{usr}: {inputText}";
            else
                textBox2.AppendText($"{Environment.NewLine}{usr}: {inputText}");
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
                port1.WriteLine(output);
            if (opn_p2 && checkBox2.Checked)
                port2.WriteLine(output);
            if (opn_p3 && checkBox3.Checked)
                port3.WriteLine(output);
            if (opn_p3 && checkBox3.Checked)
                port2.WriteLine(output);
        }
        private async void SerialPort_DataReceived1(object sender, SerialDataReceivedEventArgs e)
        {
            string data = await ReadSerialDataAsync(port1);
            if (!string.IsNullOrEmpty(data))
                this.Invoke((Action)(() =>
                {
                    if (opn_p1 && checkBox1.Checked)
                    {
                        data = RemoveSpecial(data);
                        aggiungi_a_textbox2(data, port1.PortName, Color.Red);
                        scriptcheck(data, true, false, false, false);
                    }
                }));
        }
        private async void SerialPort_DataReceived2(object sender, SerialDataReceivedEventArgs e)
        {
            string data = await ReadSerialDataAsync(port2);
            if (!string.IsNullOrEmpty(data))
                this.Invoke((Action)(() =>
                {
                    if (opn_p2 && checkBox2.Checked)
                        aggiungi_a_textbox2(data, port2.PortName, Color.Blue);
                    scriptcheck(data, false, true, false, false);
                }));
        }
        private async void SerialPort_DataReceived3(object sender, SerialDataReceivedEventArgs e)
        {
            string data = await ReadSerialDataAsync(port3);
            if (!string.IsNullOrEmpty(data))
                this.Invoke((Action)(() =>
                {
                    if (opn_p3 && checkBox3.Checked)
                        aggiungi_a_textbox2(data, port3.PortName, Color.Yellow);
                    scriptcheck(data, false, false, true, false);
                }));
        }
        private async void SerialPort_DataReceived4(object sender, SerialDataReceivedEventArgs e)
        {
            string data = await ReadSerialDataAsync(port4);
            if (!string.IsNullOrEmpty(data))
                this.Invoke((Action)(() =>
                {
                    if (opn_p4 && checkBox4.Checked)
                        aggiungi_a_textbox2(data, port4.PortName, Color.Green);
                    scriptcheck(data, false, false, false, true);
                }));
        }
        private async Task<string> ReadSerialDataAsync(SerialPort serialPort)
        {
            int bytesRead = 0;
            byte[] buffer = new byte[serialPort.BytesToRead];

            try
            {
                bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while reading: " + ex.Message);
            }

            if (bytesRead > 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, bytesRead);
            }

            return null;
        }
        public void ScanScripts()
        {
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
            if (car[0] == '*') {
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
                car = car.Substring(i+1);
            }
            else if (car[0] == '+')
            {
                press = true;
                car = car.Substring(1);
            }
            else if (car[0] == '-'){
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
            if (singlekey != '\0' || raw){
                if (press)
                    new InputSimulator().Keyboard.KeyDown(keypress);
                else if (release)
                    new InputSimulator().Keyboard.KeyUp(keypress);
                else if (time != 0){
                    new InputSimulator().Keyboard.KeyDown(keypress);
                    wait(time);
                    new InputSimulator().Keyboard.KeyUp(keypress);
                }
                else
                    new InputSimulator().Keyboard.KeyPress(keypress);
            }else {
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
            if (c_script.p4_w == true && opn_p4 && checkBox4.Checked)
            {
                port4.WriteLine(c_script.reply);
            }

        }
        public string RemoveSpecial(string data)
        {
            string str_out = data;
            bool removing = true;

            while (removing)
            {
                int ind1 = str_out.IndexOf('\n');
                if (ind1 != -1)
                    str_out = str_out.Remove(ind1, 1);

                int ind2 = str_out.IndexOf('\r');
                if (ind2 != -1)
                    str_out = str_out.Remove(ind2, 1);

                if (ind1 == -1 && ind2 == -1)
                    removing = false;
            }

            return str_out;
        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (is_locked)
            {
                is_locked = false;
                button12.Image = Properties.Resources.Unlock;

            } else
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