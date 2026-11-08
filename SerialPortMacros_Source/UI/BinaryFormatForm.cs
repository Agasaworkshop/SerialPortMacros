using SerialPortMacros.Decoding;

namespace SerialPortMacros
{
    /// <summary>
    /// Configurazione del formato binario di una porta, con anteprima e prova di decodifica.
    /// L'aspetto si modifica dal designer (BinaryFormatForm.Designer.cs); qui c'è solo la logica.
    /// </summary>
    public partial class BinaryFormatForm : Form
    {
        private bool _loading = true;

        /// <summary>Formato scelto (valido dopo DialogResult.OK).</summary>
        public PortDataFormat Result { get; private set; }

        /// <summary>Costruttore usato solo dal designer di Visual Studio.</summary>
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

        /// <summary>Qualunque campo cambiato: ricalcola anteprima e test.</summary>
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
                    ? "\r\nSenza header il programma non può risincronizzarsi se perde un byte."
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
                    ? $"Nessun frame completo ({bytes.Length} byte ricevuti, servono {format.FrameBytes}" +
                      (decoder.FramesBad > 0 ? $", {decoder.FramesBad} checksum errati)" : ")")
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
