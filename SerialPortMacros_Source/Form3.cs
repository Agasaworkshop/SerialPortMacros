using System.Diagnostics;
using SerialPortMacros.Core;
using SerialPortMacros.Macros;

namespace SerialPortMacros
{
    /// <summary>Macro list and editor.</summary>
    public partial class Form3 : Form
    {
        private const string EffectKeypress = "Keypress";
        private const string EffectReply = "Write back";
        private const string EffectCustom = "Custom code";

        public Form1 form1;
        private bool _isLoading;
        // While the list is loading, selection and check marks change on their own:
        // nothing must be saved in that phase (wrong states would be written to the files).
        private bool _initializing = true;
        private int last_index;
        private Script? c_script;

        public Form3(Form1 form)
        {
            form1 = form;
            InitializeComponent();

            // Fill the ComboBoxes before binding the DataSource (which raises events).
            comboBox2.Items.Clear();
            comboBox2.Items.AddRange(LineEndings.ShortNames);

            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(new object[] { EffectKeypress, EffectReply, EffectCustom });
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;

            BindList();
        }

        private void BindList()
        {
            checkedListBox1.DataSource = form1.scripts;
            checkedListBox1.DisplayMember = "name";
        }

        private void Form3_Load(object sender, EventArgs e)
        {
            FormBorderStyle = FormBorderStyle.FixedSingle;
            FinishLoading();
        }

        /// <summary>Applies the saved check marks and shows the selected macro, then enables saving.</summary>
        private void FinishLoading()
        {
            _initializing = true;
            findactivescripts();
            last_index = checkedListBox1.SelectedIndex;
            c_script = checkedListBox1.SelectedItem as Script;
            displayscript();
            _initializing = false;
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_initializing && last_index >= 0 && last_index < checkedListBox1.Items.Count)
                Applychanges(checkedListBox1.GetItemCheckState(last_index) == CheckState.Checked);

            last_index = checkedListBox1.SelectedIndex;
            c_script = checkedListBox1.SelectedItem as Script;
            displayscript();
        }

        private void CheckedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_initializing || e.Index < 0 || e.Index >= checkedListBox1.Items.Count)
                return;
            if (checkedListBox1.Items[e.Index] is not Script s || string.IsNullOrEmpty(s.path))
                return;

            s.active = e.NewValue == CheckState.Checked;
            TrySave(s);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        // Close (saving the selected macro)
        private void button1_Click(object sender, EventArgs e)
        {
            int index = checkedListBox1.SelectedIndex;
            if (index >= 0)
                Applychanges(checkedListBox1.GetItemCheckState(index) == CheckState.Checked);
            Close();
        }

        private void Applychanges(bool active)
        {
            if (c_script == null || string.IsNullOrEmpty(c_script.path)) return;

            c_script.key = textBox2.Text;
            c_script.p1 = checkBox1.Checked;
            c_script.p2 = checkBox2.Checked;
            c_script.p3 = checkBox3.Checked;
            c_script.p4 = checkBox4.Checked;
            c_script.active = active;

            switch (comboBox1.Text)
            {
                case EffectKeypress:
                    c_script.trigger = Script.TriggerKeypress;
                    c_script.keypress = textBox4.Text;
                    break;

                case EffectReply:
                    c_script.trigger = Script.TriggerReply;
                    c_script.reply = textBox3.Text;
                    c_script.p1_w = checkBox8.Checked;
                    c_script.p2_w = checkBox7.Checked;
                    c_script.p3_w = checkBox6.Checked;
                    c_script.p4_w = checkBox5.Checked;
                    c_script.lineEndingIndex = comboBox2.SelectedIndex >= 0 ? comboBox2.SelectedIndex : 1;
                    break;

                case EffectCustom:
                    c_script.trigger = Script.TriggerCustom;
                    c_script.customFile = textBox4.Text.Trim();
                    break;
            }

            TrySave(c_script);
        }

        private static void TrySave(Script s)
        {
            try
            {
                ScriptStore.Save(s);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot save {s.name}: {ex.Message}");
            }
        }

        void findactivescripts()
        {
            for (int i = 0; i < form1.scripts.Length; i++)
                checkedListBox1.SetItemCheckState(i, form1.scripts[i].active ? CheckState.Checked : CheckState.Unchecked);
        }

        // New macro file
        private void button3_Click(object sender, EventArgs e)
        {
            // Save the selected macro's changes before reloading the list from the files.
            int selected = checkedListBox1.SelectedIndex;
            if (selected >= 0)
                Applychanges(checkedListBox1.GetItemCheckState(selected) == CheckState.Checked);

            if (textBox5.Text != "")
            {
                try
                {
                    if (!ScriptStore.CreateNew(textBox5.Text))
                        MessageBox.Show($"{textBox5.Text} already exists.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred while creating the file: " + ex.Message);
                }
                textBox5.Text = "";
            }

            _initializing = true;
            form1.ScanScripts();
            BindList();
            FinishLoading();
        }

        // Creates/opens the code file of the custom macro
        private void button4_Click(object sender, EventArgs e)
        {
            string spec = textBox4.Text.Trim();
            if (spec.Length == 0)
            {
                spec = (c_script != null ? Path.GetFileNameWithoutExtension(c_script.name) : "MyMacro") + ".cs";
                textBox4.Text = spec;
            }

            try
            {
                string path = CustomMacroTemplates.CreateIfMissing(spec);
                if (!File.Exists(path))
                {
                    MessageBox.Show($"File not found: {path}");
                    return;
                }

                if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo { FileName = Path.GetDirectoryName(path)!, UseShellExecute = true });
                    return;
                }

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
                catch
                {
                    // No program associated with .cs files
                    Process.Start("notepad.exe", $"\"{path}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Picks a code file (.cs or .dll) with a file dialog
        private void button5_Click(object sender, EventArgs e)
        {
            string folder = AppPaths.CustomMacros;

            // Start from the folder of the current file, if it exists.
            string current = textBox4.Text.Trim();
            if (current.Length > 0)
            {
                string dir = Path.GetDirectoryName(CustomMacroLoader.ResolvePath(current, out _)) ?? "";
                if (Directory.Exists(dir)) folder = dir;
            }

            using var dialog = new OpenFileDialog
            {
                Title = "Choose the macro code",
                Filter = "Macro code (*.cs;*.dll)|*.cs;*.dll|C# source (*.cs)|*.cs|Library (*.dll)|*.dll",
                InitialDirectory = folder,
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            textBox4.Text = ToMacroSpec(dialog.FileName);

            // Check right away that the file contains a valid macro.
            string? error = CustomMacroLoader.Check(textBox4.Text);
            if (error != null)
                MessageBox.Show(error, "Macro code", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>File inside custom_macros: name only (stays valid if the program is moved); elsewhere: full path.</summary>
        private static string ToMacroSpec(string path)
        {
            string folder = Path.GetFullPath(AppPaths.CustomMacros).TrimEnd('\\') + "\\";
            string full = Path.GetFullPath(path);
            return full.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? full[folder.Length..] : full;
        }

        private void comboBox1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateEffectControls();
            if (_isLoading) return;

            if (comboBox1.Text == EffectReply && comboBox2.SelectedIndex == -1)
                comboBox2.SelectedIndex = 1; // default NL
            if (comboBox1.Text != EffectReply)
                comboBox2.SelectedIndex = -1;
        }

        private void UpdateEffectControls()
        {
            bool custom = comboBox1.Text == EffectCustom;
            label14.Text = custom ? "Code file" : "Keypress";
            toolTip1.SetToolTip(textBox4, custom
                ? "File .cs or .dll in the custom_macros folder (optional \":ClassName\")"
                : "");
            button4.Visible = custom;
            button5.Visible = custom;

            // The "Reply" row (text, destination ports, line ending) applies only to "Write back":
            // a custom macro chooses by itself which port to write to.
            bool reply = comboBox1.Text == EffectReply;
            comboBox2.Enabled = reply;
            textBox3.Enabled = reply;
            checkBox5.Enabled = checkBox6.Enabled = checkBox7.Enabled = checkBox8.Enabled = reply;
        }

        public void displayscript()
        {
            _isLoading = true;
            textBox4.Text = "";
            comboBox1.Text = "";
            textBox3.Text = "";
            checkBox8.Checked = checkBox7.Checked = checkBox6.Checked = checkBox5.Checked = false;
            comboBox2.SelectedIndex = -1;

            if (checkedListBox1.SelectedItem is Script s)
            {
                textBox1.Text = s.name;
                textBox2.Text = s.key;
                checkBox1.Checked = s.p1;
                checkBox2.Checked = s.p2;
                checkBox3.Checked = s.p3;
                checkBox4.Checked = s.p4;

                switch (s.trigger)
                {
                    case Script.TriggerKeypress:
                        comboBox1.Text = EffectKeypress;
                        textBox4.Text = s.keypress;
                        break;

                    case Script.TriggerReply:
                        comboBox1.Text = EffectReply;
                        textBox3.Text = s.reply;
                        checkBox8.Checked = s.p1_w;
                        checkBox7.Checked = s.p2_w;
                        checkBox6.Checked = s.p3_w;
                        checkBox5.Checked = s.p4_w;
                        comboBox2.SelectedIndex = s.lineEndingIndex >= 0 && s.lineEndingIndex < comboBox2.Items.Count
                            ? s.lineEndingIndex
                            : 1;
                        break;

                    case Script.TriggerCustom:
                        comboBox1.Text = EffectCustom;
                        textBox4.Text = s.customFile;
                        break;
                }
            }

            UpdateEffectControls();
            _isLoading = false;
        }
    }
}
