using SerialPortMacros.Decoding;

namespace SerialPortMacros
{
    /// <summary>
    /// Binary format configuration of a port, with a live preview and a decode test.
    /// The layout is edited in the designer (BinaryFormatForm.Designer.cs); this file holds only the logic.
    /// </summary>
    public partial class BinaryFormatForm : Form
    {
        private bool _loading = true;

        /// <summary>Chosen format (valid after DialogResult.OK).</summary>
        public PortDataFormat Result { get; private set; }

        /// <summary>Constructor used only by the Visual Studio designer.</summary>
        public BinaryFormatForm() : this(new PortDataFormat())
        {
        }

        public BinaryFormatForm(PortDataFormat current)
        {
            InitializeComponent();
            Result = current.Clone();

            comboChecksum.Items.AddRange(Enum.GetNames(typeof(ChecksumKind)));

            textFields.Text = Result.Fields;
            textHeader.Text = Result.Header;
            comboEndian.SelectedIndex = Result.LittleEndian ? 0 : 1;
            comboChecksum.SelectedItem = Result.Checksum.ToString();

            _loading = false;
            UpdatePreview();
        }

        private PortDataFormat Current() => new()
        {
            Mode = DataMode.Binary,
            Fields = textFields.Text.Trim(),
            Header = textHeader.Text.Trim(),
            LittleEndian = comboEndian.SelectedIndex != 1,
            Checksum = Enum.TryParse(comboChecksum.SelectedItem as string, out ChecksumKind k) ? k : ChecksumKind.None
        };

        /// <summary>Any input changed: refresh the preview and the test.</summary>
        private void Inputs_Changed(object? sender, EventArgs e)
        {
            if (!_loading)
                UpdatePreview();
        }

        private void UpdatePreview()
        {
            BinaryFrameFormat format;
            try
            {
                format = BinaryFrameFormat.Parse(Current());
                labelInfo.ForeColor = Color.DarkGreen;
                string warn = format.Header.Length == 0
                    ? "\r\nWithout a header the program cannot resynchronize if a byte is lost."
                    : "";
                labelInfo.Text = format.Describe() + warn;
            }
            catch (FormatException ex)
            {
                labelInfo.ForeColor = Color.Red;
                labelInfo.Text = ex.Message;
                textTestOutput.Text = "";
                return;
            }

            if (string.IsNullOrWhiteSpace(textTestInput.Text))
            {
                textTestOutput.Text = "";
                return;
            }

            try
            {
                byte[] bytes = BinaryFrameFormat.ParseHex(textTestInput.Text);
                var decoder = new BinaryFrameDecoder(format);
                var frames = new List<DecodedFrame>();
                decoder.Feed(bytes, frames);

                textTestOutput.Text = frames.Count == 0
                    ? $"No complete frame ({bytes.Length} bytes received, {format.FrameBytes} needed" +
                      (decoder.FramesBad > 0 ? $", {decoder.FramesBad} bad checksums)" : ")")
                    : string.Join(Environment.NewLine, frames.Select(f => f.Text));
            }
            catch (FormatException ex)
            {
                textTestOutput.Text = ex.Message;
            }
        }

        private void buttonOk_Click(object? sender, EventArgs e)
        {
            var cfg = Current();
            try
            {
                BinaryFrameFormat.Parse(cfg);
            }
            catch (FormatException ex)
            {
                MessageBox.Show(ex.Message, "Binary format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = cfg;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
