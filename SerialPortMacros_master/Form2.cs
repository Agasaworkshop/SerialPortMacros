using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Security.Cryptography.X509Certificates;

namespace SerialPortMacros
{
    public partial class Form2 : Form
    {
        public SerialPort port;
        public Form2(SerialPort ref_port)
        {
            InitializeComponent();
            port = ref_port;
        }
        private void Form2_Load(object sender, EventArgs e)
        {
            Paritybox.Items.Add("Odd");
            Paritybox.Items.Add("Even");
            Paritybox.Items.Add("None");
            stopbitsbox.Items.Add("None");
            stopbitsbox.Items.Add("One");
            stopbitsbox.Items.Add("OnePointFive");
            stopbitsbox.Items.Add("Two");

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            Setparity();
            port.DataBits = int.Parse(databitsbox.Text);
            SetStopBits();
            this.Close();
        }
        private void Setparity(){
            if (Paritybox.Text == "Odd")
                port.Parity = Parity.Odd;
            else if (Paritybox.Text == "Even")
                port.Parity = Parity.Even;
            else if (Paritybox.Text == "None")
                port.Parity = Parity.None;
            return;
        }
        private void SetStopBits(){
            if (stopbitsbox.Text == "None")
                port.StopBits = StopBits.None;
            else if (stopbitsbox.Text == "One")
                port.StopBits = StopBits.One;
            else if (stopbitsbox.Text == "OnePointFive")
                port.StopBits = StopBits.OnePointFive;
            else if (stopbitsbox.Text == "Two")
                port.StopBits = StopBits.Two;
            return;
        }
    }
}
