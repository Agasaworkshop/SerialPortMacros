using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SerialPortMacros
{
    /// <summary>Configuration dialog of an interface element (opened by Form7).</summary>
    public partial class Form8 : Form
    {
        public string ModuleDirectory { get; set; } = "";
        private readonly string[] _moduleTypes =
        {
            "Button",
            "Toggle",
            "Led",
            "NumericOutput",
            "NumericInput",
            "Label"
        };

        public ModuleConfiguration Configuration { get; private set; } = new ModuleConfiguration();
        public string ImageDirectory { get; set; } = "";

        public Form8()
        {
            InitializeComponent();

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(_moduleTypes);

            if (string.IsNullOrWhiteSpace(textBox1.Text)) textBox1.Text = "";
            if (string.IsNullOrWhiteSpace(textBox2.Text)) textBox2.Text = "";
            if (string.IsNullOrWhiteSpace(textBox3.Text)) textBox3.Text = "";
            if (string.IsNullOrWhiteSpace(textBox4.Text)) textBox4.Text = "";
            if (string.IsNullOrWhiteSpace(textBox5.Text)) textBox5.Text = "";

            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            checkBox1.CheckedChanged += PortCheckBox_CheckedChanged;
            checkBox2.CheckedChanged += PortCheckBox_CheckedChanged;
            checkBox3.CheckedChanged += PortCheckBox_CheckedChanged;
            checkBox4.CheckedChanged += PortCheckBox_CheckedChanged;

            var browseButton = FindControl<Button>("btnBrowseImage");
            if (browseButton != null)
                browseButton.Click += BrowseImageButton_Click;

            var applyButton = FindControl<Button>("btnApply");
            if (applyButton != null)
            {
                applyButton.DialogResult = DialogResult.None;
                applyButton.Click += ApplyButton_Click;
            }

            if (comboBox1.Items.Count > 0 && comboBox1.SelectedIndex < 0)
                comboBox1.SelectedIndex = 0;

            // Filled here, not in Load: Form7 selects the element's saved line ending
            // right after constructing this form, before it is shown.
            InitializeLineEndingCombo();

            UpdateVisibleOptions();
            AcceptButton = applyButton;
        }

        private void InitializeLineEndingCombo()
        {
            lineEndingCombo.Items.Clear();
            lineEndingCombo.Items.Add("Inherit from Form1");
            lineEndingCombo.Items.Add("No line ending");
            lineEndingCombo.Items.Add("New line (\\n)");
            lineEndingCombo.Items.Add("Carriage return (\\r)");
            lineEndingCombo.Items.Add("Both NL & CR (\\r\\n)");
            lineEndingCombo.SelectedIndex = 0;
        }

        public Form8(ModuleConfiguration existingConfiguration) : this()
        {
            if (existingConfiguration == null)
                return;

            Configuration = CloneConfiguration(existingConfiguration);

            int typeIndex = comboBox1.Items.IndexOf(Configuration.ModuleType);
            if (typeIndex >= 0)
                comboBox1.SelectedIndex = typeIndex;

            textBox2.Text = Configuration.VariableName ?? "";
            textBox1.Text = Configuration.SecondImage ?? "";
            textBox4.Text = Configuration.Message1 ?? "";
            textBox3.Text = Configuration.Message2 ?? "";
            textBox5.Text = Configuration.LedCondition ?? "";

            checkBox1.Checked = Configuration.TargetPorts.Contains(1);
            checkBox2.Checked = Configuration.TargetPorts.Contains(2);
            checkBox3.Checked = Configuration.TargetPorts.Contains(3);
            checkBox4.Checked = Configuration.TargetPorts.Contains(4);

            var combo = FindControl<ComboBox>("lineEndingCombo");
            if (combo != null && !string.IsNullOrEmpty(Configuration.CustomLineEnding))
            {
                int idx = combo.Items.IndexOf(Configuration.CustomLineEnding);
                if (idx >= 0) combo.SelectedIndex = idx;
            }

            UpdateVisibleOptions();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateVisibleOptions();
        }

        private void UpdateVisibleOptions()
        {
            string type = SelectedModuleType();

            bool isButton = (type == "Button");
            bool isToggle = (type == "Toggle");
            bool isLed = (type == "Led");
            bool isNumericOutput = (type == "NumericOutput");
            bool isNumericInput = (type == "NumericInput");
            bool isLabel = (type == "Label");

            textBox2.Enabled = isNumericOutput || isNumericInput || isToggle;
            textBox1.Enabled = isToggle || isLed;
            textBox4.Enabled = isButton || isToggle || isLabel; // Also enabled for Label (fixed text)
            textBox3.Enabled = isToggle;
            textBox5.Enabled = isLed || isButton;

            checkBox1.Enabled = isButton || isToggle;
            checkBox2.Enabled = isButton || isToggle;
            checkBox3.Enabled = isButton || isToggle;
            checkBox4.Enabled = isButton || isToggle;

            button1.Enabled = isLed || isToggle;

            lineEndingCombo.Enabled = isButton || isToggle;

            SetLabelEnabled("lblVariableName", isNumericOutput || isNumericInput);
            SetLabelEnabled("lblSecondImage", isToggle || isLed);
            SetLabelEnabled("lblMessage1", isButton || isToggle || isLabel);
            SetLabelEnabled("lblMessage2", isToggle);
            SetLabelEnabled("lblLedCondition", isLed);
        }

        private void BrowseImageButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select the second image";
                dialog.Filter = "PNG Images (*.png)|*.png|All Images|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                string startingDirectory = ImageDirectory;
                if (string.IsNullOrWhiteSpace(startingDirectory) ||
                    !Directory.Exists(startingDirectory))
                {
                    string currentText = textBox1.Text.Trim();
                    if (File.Exists(currentText))
                        startingDirectory = Path.GetDirectoryName(currentText);
                }

                if (!string.IsNullOrWhiteSpace(startingDirectory) && Directory.Exists(startingDirectory))
                    dialog.InitialDirectory = startingDirectory;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (!string.IsNullOrWhiteSpace(ImageDirectory) &&
                        Directory.Exists(ImageDirectory) &&
                        string.Equals(Path.GetDirectoryName(dialog.FileName).TrimEnd(Path.DirectorySeparatorChar),
                                      ImageDirectory.TrimEnd(Path.DirectorySeparatorChar),
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        textBox1.Text = Path.GetFileName(dialog.FileName);
                    }
                    else
                    {
                        textBox1.Text = dialog.FileName;
                    }
                }
            }
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            ModuleConfiguration newConfiguration;
            string validationError;

            if (!TryBuildConfiguration(out newConfiguration, out validationError))
            {
                MessageBox.Show(this, validationError, "Module Configuration",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Configuration = newConfiguration;
            DialogResult = DialogResult.OK;
            Close();
        }

        public bool TryBuildConfiguration(out ModuleConfiguration configuration, out string error)
        {
            configuration = null;
            error = "";

            string type = SelectedModuleType();
            if (string.IsNullOrWhiteSpace(type))
            {
                error = "Please select a module type.";
                return false;
            }

            var combo = FindControl<ComboBox>("lineEndingCombo");
            string lineEndingVal = "";
            if (combo != null && combo.SelectedItem != null)
            {
                string sel = combo.SelectedItem.ToString();
                if (sel != "Inherit from Form1")
                    lineEndingVal = sel;
            }

            var result = new ModuleConfiguration
            {
                ModuleType = type,
                VariableName = textBox2.Text.Trim(),
                SecondImage = textBox1.Text.Trim(),
                Message1 = textBox4.Text,
                Message2 = textBox3.Text,
                LedCondition = textBox5.Text.Trim(),
                TargetPorts = GetSelectedPorts(),
                CustomLineEnding = lineEndingVal
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

        private void PortCheckBox_CheckedChanged(object sender, EventArgs e) { }

        private string SelectedModuleType()
        {
            return comboBox1.SelectedItem == null ? "" : comboBox1.SelectedItem.ToString();
        }

        private T FindControl<T>(string name) where T : Control
        {
            Control[] matches = Controls.Find(name, true);
            return matches.Length > 0 ? matches[0] as T : null;
        }

        private void SetLabelEnabled(string name, bool enabled)
        {
            Label label = FindControl<Label>(name);
            if (label != null)
                label.Enabled = enabled;
        }

        private static ModuleConfiguration CloneConfiguration(ModuleConfiguration source)
        {
            return new ModuleConfiguration
            {
                ModuleType = source.ModuleType ?? "Button",
                VariableName = source.VariableName ?? "",
                SecondImage = source.SecondImage ?? "",
                Message1 = source.Message1 ?? "",
                Message2 = source.Message2 ?? "",
                LedCondition = source.LedCondition ?? "",
                TargetPorts = source.TargetPorts == null
                    ? new List<int>()
                    : source.TargetPorts.Distinct().Where(p => p >= 1 && p <= 4).OrderBy(p => p).ToList(),
                CustomLineEnding = source.CustomLineEnding ?? ""
            };
        }

        private void Form8_Load(object sender, EventArgs e)
        {
        }

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
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                // Does not delete anything on disk: only tells Form7 to remove this element
                DialogResult = DialogResult.Abort;
                Close();
            }
        }
    }

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