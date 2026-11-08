namespace SerialPortMacros
{
    /// <summary>
    /// Log session messages: the command sent when logging starts, the command sent
    /// when it stops, and whether to wait for the device to finish before closing.
    /// </summary>
    public partial class Form6 : Form
    {
        public string message1;
        public string message2;
        public Form1 mainform;

        public Form6(Form1 form1, string initial_message, string final_message, bool check)
        {
            message1 = initial_message;
            message2 = final_message;
            InitializeComponent();
            textBox1.Text = message1;
            textBox2.Text = message2;
            checkBox1.Checked = check;
            mainform = form1;
        }

        // OK
        private void button1_Click(object sender, EventArgs e)
        {
            mainform.Set_logging_messages(textBox1.Text, textBox2.Text, checkBox1.Checked);
            this.Close();
        }

        // Cancel
        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Form6_Load(object sender, EventArgs e)
        {
        }
    }
}
