namespace SerialPortMacros
{
    /// <summary>Configuration dialog of an interface element (opened by Form7).</summary>
    public partial class Form8 : Form
    {
        private const string InheritLineEnding = "Inherit from Form1";

        private static readonly string[] ModuleTypes =
        {
            "Button",
            "Toggle",
            "Led",
            "NumericOutput",
            "NumericInput",
            "Label"
        };

        /// <summary>Folder of the element's module: the image browser starts here.</summary>
        public string ModuleDirectory { get; set; } = "";

        /// <summary>The validated configuration (set when the dialog closes with OK).</summary>
        public ModuleConfiguration Configuration { get; private set; } = new ModuleConfiguration();

        public Form8()
        {
            InitializeComponent();

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(ModuleTypes);
            comboBox1.SelectedIndex = 0;

            // Filled here, not in Load: Form7 selects the element's saved line ending
            // right after constructing this form, before it is shown.
            InitializeLineEndingCombo();

            UpdateVisibleOptions();
        }

        private void InitializeLineEndingCombo()
        {
            lineEndingCombo.Items.Clear();
            lineEndingCombo.Items.Add(InheritLineEnding);
            lineEndingCombo.Items.Add("No line ending");
            lineEndingCombo.Items.Add("New line (\\n)");
            lineEndingCombo.Items.Add("Carriage return (\\r)");
            lineEndingCombo.Items.Add("Both NL & CR (\\r\\n)");
            lineEndingCombo.SelectedIndex = 0;
        }

        private void Form8_Load(object sender, EventArgs e)
        {
        }

        // ------------------------------------------------------------------
        // Options shown for each element type
        // ------------------------------------------------------------------

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateVisibleOptions();
        }

        private void UpdateVisibleOptions()
        {
            string type = SelectedModuleType();

            bool isButton = type == "Button";
            bool isToggle = type == "Toggle";
            bool isLed = type == "Led";
            bool isNumericOutput = type == "NumericOutput";
            bool isNumericInput = type == "NumericInput";
            bool isLabel = type == "Label";

            textBox2.Enabled = isNumericOutput || isNumericInput || isToggle;   // variable name
            textBox1.Enabled = isToggle || isLed;                               // second image
            textBox4.Enabled = isButton || isToggle || isLabel;                 // message 1 (also the Label text)
            textBox3.Enabled = isToggle;                                        // message 2
            textBox5.Enabled = isLed || isButton;                               // condition

            checkBox1.Enabled = isButton || isToggle;
            checkBox2.Enabled = isButton || isToggle;
            checkBox3.Enabled = isButton || isToggle;
            checkBox4.Enabled = isButton || isToggle;

            button1.Enabled = isLed || isToggle;                                // browse

            lineEndingCombo.Enabled = isButton || isToggle;
        }

        private string SelectedModuleType() => comboBox1.SelectedItem?.ToString() ?? "";

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        // Browse for the second image (toggle "on" / LED "on")
        private void BrowseImageButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select the second image",
                Filter = "PNG Images (*.png)|*.png|All Images|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            string? startingDirectory = ModuleDirectory;
            if (string.IsNullOrWhiteSpace(startingDirectory) || !Directory.Exists(startingDirectory))
            {
                string currentText = textBox1.Text.Trim();
                if (File.Exists(currentText))
                    startingDirectory = Path.GetDirectoryName(currentText);
            }

            if (!string.IsNullOrWhiteSpace(startingDirectory) && Directory.Exists(startingDirectory))
                dialog.InitialDirectory = startingDirectory;

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            // An image in the module folder is stored by name only, anything else by full path.
            bool inModuleFolder =
                !string.IsNullOrWhiteSpace(ModuleDirectory) &&
                Directory.Exists(ModuleDirectory) &&
                string.Equals(
                    (Path.GetDirectoryName(dialog.FileName) ?? "").TrimEnd(Path.DirectorySeparatorChar),
                    ModuleDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);

            textBox1.Text = inModuleFolder ? Path.GetFileName(dialog.FileName) : dialog.FileName;
        }

        // Save: validate, then close with OK
        private void ApplyButton_Click(object sender, EventArgs e)
        {
            if (!TryBuildConfiguration(out ModuleConfiguration? newConfiguration, out string validationError))
            {
                MessageBox.Show(this, validationError, "Module Configuration",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Configuration = newConfiguration!;
            DialogResult = DialogResult.OK;
            Close();
        }

        // Cancel
        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Are you sure you want to remove this item?",
                "Confirm deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // Does not delete anything on disk: only tells Form7 to remove this element
                DialogResult = DialogResult.Abort;
                Close();
            }
        }

        // ------------------------------------------------------------------
        // Validation
        // ------------------------------------------------------------------

        public bool TryBuildConfiguration(out ModuleConfiguration? configuration, out string error)
        {
            configuration = null;
            error = "";

            string type = SelectedModuleType();
            if (string.IsNullOrWhiteSpace(type))
            {
                error = "Please select a module type.";
                return false;
            }

            string selectedEnding = lineEndingCombo.SelectedItem?.ToString() ?? "";

            var result = new ModuleConfiguration
            {
                ModuleType = type,
                VariableName = textBox2.Text.Trim(),
                SecondImage = textBox1.Text.Trim(),
                Message1 = textBox4.Text,
                Message2 = textBox3.Text,
                LedCondition = textBox5.Text.Trim(),
                TargetPorts = GetSelectedPorts(),
                CustomLineEnding = selectedEnding == InheritLineEnding ? "" : selectedEnding
            };

            if ((type == "NumericOutput" || type == "NumericInput") &&
                string.IsNullOrWhiteSpace(result.VariableName))
            {
                error = "Please enter the variable name for the numeric module.";
                return false;
            }

            if ((type == "Button" || type == "Toggle" || type == "Label") &&
                string.IsNullOrWhiteSpace(result.Message1))
            {
                error = "Please enter message 1 (or the label text).";
                return false;
            }

            if (type == "Toggle" && string.IsNullOrWhiteSpace(result.Message2))
            {
                error = "Please enter message 2 for the ON → OFF transition.";
                return false;
            }

            if ((type == "Toggle" || type == "Led") &&
                string.IsNullOrWhiteSpace(result.SecondImage))
            {
                error = "Please specify the name or path of the second image.";
                return false;
            }

            if (type == "Led" && string.IsNullOrWhiteSpace(result.LedCondition))
            {
                error = "Please enter the LED condition, for example: temperature > 30";
                return false;
            }

            configuration = result;
            return true;
        }

        private List<int> GetSelectedPorts()
        {
            var ports = new List<int>();
            if (checkBox1.Checked) ports.Add(1);
            if (checkBox2.Checked) ports.Add(2);
            if (checkBox3.Checked) ports.Add(3);
            if (checkBox4.Checked) ports.Add(4);
            return ports;
        }
    }

    /// <summary>Validated settings of an interface element, as entered in Form8.</summary>
    public sealed class ModuleConfiguration
    {
        public string ModuleType { get; set; } = "Button";
        public string VariableName { get; set; } = "";
        public string SecondImage { get; set; } = "";
        public string Message1 { get; set; } = "";
        public string Message2 { get; set; } = "";
        public List<int> TargetPorts { get; set; } = new List<int>();
        public string LedCondition { get; set; } = "";
        public string CustomLineEnding { get; set; } = "";
    }
}
