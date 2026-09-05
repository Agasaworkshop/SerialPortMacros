using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialPortMacros
{
    public partial class Form6 : Form
    {

        public string message1;
        public string message2;
        public Form1 mainform;

        public Form6(Form1 form1, string initial_message, string final_message,bool check)
        {
            message1 = initial_message;
            message2 = final_message;
            InitializeComponent();
            textBox1.Text = message1;
            textBox2.Text = message2;
            checkBox1.Checked = check;
            mainform = form1;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            mainform.Set_logging_messages(textBox1.Text, textBox2.Text, checkBox1.Checked);
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Form6_Load(object sender, EventArgs e)
        {

        }
    }
}
