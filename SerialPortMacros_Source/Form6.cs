namespace SerialPortMacros
{
    /// <summary>
    /// Log session messages: the command sent when logging starts, the command sent
    /// when it stops, and whether to wait for the device to finish before closing.
    /// </summary>
    public partial class Form6 : Form
    {
        private readonly Form1 _mainForm;

        public Form6(Form1 mainForm, string startMessage, string stopMessage, bool waitForEnd)
        {
            InitializeComponent();
            _mainForm = mainForm;
            textBox1.Text = startMessage;
            textBox2.Text = stopMessage;
            checkBox1.Checked = waitForEnd;
        }

        // OK
        private void button1_Click(object sender, EventArgs e)
        {
            _mainForm.SetLoggingMessages(textBox1.Text, textBox2.Text, checkBox1.Checked);
            Close();
        }

        // Cancel
        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Form6_Load(object sender, EventArgs e)
        {
        }
    }
}
