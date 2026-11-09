using System.IO.Ports;
using SerialPortMacros.Core;
using SerialPortMacros.Decoding;

namespace SerialPortMacros
{
    /// <summary>Port settings: serial parameters and received data format.</summary>
    public partial class Form2 : Form
    {
        private readonly SerialPort _port;
        private readonly bool _serialDisabled;

        /// <summary>Chosen data format (valid after DialogResult.OK).</summary>
        public PortDataFormat DataFormat { get; private set; }

        public Form2(SerialPort port, PortDataFormat format, bool serialDisabled)
        {
            InitializeComponent();
            _port = port;
            _serialDisabled = serialDisabled;
            DataFormat = format.Clone();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            FormBorderStyle = FormBorderStyle.FixedSingle;

            Paritybox.Items.AddRange(SerialPortHelpers.ParityNames);
            stopbitsbox.Items.AddRange(SerialPortHelpers.StopBitsNames);

            Paritybox.Text = SerialPortHelpers.ToText(_port.Parity);
            databitsbox.Text = _port.DataBits.ToString();
            stopbitsbox.Text = SerialPortHelpers.ToText(_port.StopBits);
            if (_port.ReadBufferSize != 4096)
            {
                textBox1.Text = _port.ReadBufferSize.ToString();
                checkBox1.Checked = true;
            }

            // Serial parameters have no effect in TCP and BLE mode.
            Paritybox.Enabled = databitsbox.Enabled = stopbitsbox.Enabled =
                textBox1.Enabled = checkBox1.Enabled = !_serialDisabled;

            dataModeBox.Items.AddRange(new object[] { "Text", "Binary" });
            dataModeBox.SelectedIndex = DataFormat.Mode == DataMode.Binary ? 1 : 0;
            UpdateBinaryButton();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!_serialDisabled)
            {
                try
                {
                    _port.Parity = SerialPortHelpers.ParseParity(Paritybox.Text);
                    if (int.TryParse(databitsbox.Text, out int dataBits))
                        _port.DataBits = dataBits;
                    _port.StopBits = SerialPortHelpers.ParseStopBits(stopbitsbox.Text);
                    if (checkBox1.Checked && int.TryParse(textBox1.Text, out int size) && size > 0)
                        _port.ReadBufferSize = size;
                }
                catch (Exception ex)
                {
                    // Some parameters cannot be changed while the port is open.
                    MessageBox.Show(ex.Message, "Serial settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            DataFormat.Mode = dataModeBox.SelectedIndex == 1 ? DataMode.Binary : DataMode.Text;

            if (DataFormat.Mode == DataMode.Binary)
            {
                try
                {
                    BinaryFrameFormat.Parse(DataFormat);
                }
                catch (FormatException ex)
                {
                    MessageBox.Show("Binary format not valid:\n" + ex.Message + "\n\nConfigure it with the \"...\" button.",
                        "Binary format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void dataModeBox_SelectedIndexChanged(object? sender, EventArgs e) => UpdateBinaryButton();

        private void UpdateBinaryButton()
        {
            binaryButton.Enabled = dataModeBox.SelectedIndex == 1;
        }

        private void binaryButton_Click(object? sender, EventArgs e)
        {
            using var form = new BinaryFormatForm(DataFormat);
            if (form.ShowDialog(this) == DialogResult.OK)
                DataFormat = form.Result;
        }
    }
}
