using System.Diagnostics;
using SerialPortMacros.Core;
using SerialPortMacros.Macros;

namespace SerialPortMacros
{
    /// <summary>
    /// Macro list and editor.
    ///
    /// The window edits its own copy of the macros, read from the files: the objects used
    /// by the macro engine (on the processing thread) are never modified here. Changes are
    /// saved to the files, and Form1 reloads them into the engine when this window closes.
    /// </summary>
    public partial class Form3 : Form
    {
        private const string EffectNameKeypress = "Keypress";
        private const string EffectNameReply = "Write back";
        private const string EffectNameCustom = "Custom code";

        private Script[] _scripts = Array.Empty<Script>();
        private Script? _currentScript;
        private int _lastIndex;
        private bool _isLoading;

        // While the list is loading, selection and check marks change on their own:
        // nothing must be saved in that phase (wrong states would be written to the files).
        private bool _initializing = true;

        public Form3()
        {
            InitializeComponent();

            // Fill the ComboBoxes before binding the DataSource (which raises events).
            comboBox2.Items.Clear();
            comboBox2.Items.AddRange(LineEndings.ShortNames);

            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(new object[] { EffectNameKeypress, EffectNameReply, EffectNameCustom });
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;

            LoadScripts();
        }

        /// <summary>Reads the macro files and binds them to the list.</summary>
        private void LoadScripts()
        {
            _scripts = ScriptStore.LoadAll();
            checkedListBox1.DataSource = _scripts;
            checkedListBox1.DisplayMember = nameof(Script.Name);
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
            ApplySavedCheckStates();
            _lastIndex = checkedListBox1.SelectedIndex;
            _currentScript = checkedListBox1.SelectedItem as Script;
            DisplaySelectedScript();
            _initializing = false;
        }

        private void ApplySavedCheckStates()
        {
            for (int i = 0; i < _scripts.Length; i++)
                checkedListBox1.SetItemCheckState(i, _scripts[i].Active ? CheckState.Checked : CheckState.Unchecked);
        }

        // ------------------------------------------------------------------
        // List events
        // ------------------------------------------------------------------

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_initializing && _lastIndex >= 0 && _lastIndex < checkedListBox1.Items.Count)
                ApplyChanges(checkedListBox1.GetItemCheckState(_lastIndex) == CheckState.Checked);

            _lastIndex = checkedListBox1.SelectedIndex;
            _currentScript = checkedListBox1.SelectedItem as Script;
            DisplaySelectedScript();
        }

        private void CheckedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_initializing || e.Index < 0 || e.Index >= checkedListBox1.Items.Count)
                return;
            if (checkedListBox1.Items[e.Index] is not Script s || string.IsNullOrEmpty(s.FilePath))
                return;

            s.Active = e.NewValue == CheckState.Checked;
            TrySave(s);
        }

        // ------------------------------------------------------------------
        // Saving
        // ------------------------------------------------------------------

        /// <summary>Copies the editor fields into the selected macro and saves it.</summary>
        private void ApplyChanges(bool active)
        {
            if (_currentScript == null || string.IsNullOrEmpty(_currentScript.FilePath)) return;

            var s = _currentScript;
            s.Key = textBox2.Text;
            s.ListenPort1 = checkBox1.Checked;
            s.ListenPort2 = checkBox2.Checked;
            s.ListenPort3 = checkBox3.Checked;
            s.ListenPort4 = checkBox4.Checked;
            s.Active = active;

            switch (comboBox1.Text)
            {
                case EffectNameKeypress:
                    s.Effect = MacroEffect.Keypress;
                    s.KeyPress = textBox4.Text;
                    break;

                case EffectNameReply:
                    s.Effect = MacroEffect.Reply;
                    s.Reply = textBox3.Text;
                    s.ReplyPort1 = checkBox8.Checked;
                    s.ReplyPort2 = checkBox7.Checked;
                    s.ReplyPort3 = checkBox6.Checked;
                    s.ReplyPort4 = checkBox5.Checked;
                    s.LineEndingIndex = comboBox2.SelectedIndex >= 0 ? comboBox2.SelectedIndex : 1;
                    break;

                case EffectNameCustom:
                    s.Effect = MacroEffect.Custom;
                    s.CustomFile = textBox4.Text.Trim();
                    break;
            }

            TrySave(s);
        }

        private static void TrySave(Script s)
        {
            try
            {
                ScriptStore.Save(s);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot save {s.Name}: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }

        // Close (saving the selected macro)
        private void button1_Click(object sender, EventArgs e)
        {
            int index = checkedListBox1.SelectedIndex;
            if (index >= 0)
                ApplyChanges(checkedListBox1.GetItemCheckState(index) == CheckState.Checked);
            Close();
        }

        // New macro file
        private void button3_Click(object sender, EventArgs e)
        {
            // Save the selected macro's changes before reloading the list from the files.
            int selected = checkedListBox1.SelectedIndex;
            if (selected >= 0)
                ApplyChanges(checkedListBox1.GetItemCheckState(selected) == CheckState.Checked);

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
            LoadScripts();
            FinishLoading();
        }

        // Creates/opens the code file of the custom macro
        private void button4_Click(object sender, EventArgs e)
        {
            string spec = textBox4.Text.Trim();
            if (spec.Length == 0)
            {
                spec = (_currentScript != null ? Path.GetFileNameWithoutExtension(_currentScript.Name) : "MyMacro") + ".cs";
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

        // ------------------------------------------------------------------
        // Editor fields
        // ------------------------------------------------------------------

        private void comboBox1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateEffectControls();
            if (_isLoading) return;

            if (comboBox1.Text == EffectNameReply && comboBox2.SelectedIndex == -1)
                comboBox2.SelectedIndex = 1; // default NL
            if (comboBox1.Text != EffectNameReply)
                comboBox2.SelectedIndex = -1;
        }

        private void UpdateEffectControls()
        {
            bool custom = comboBox1.Text == EffectNameCustom;
            label14.Text = custom ? "Code file" : "Keypress";
            toolTip1.SetToolTip(textBox4, custom
                ? "File .cs or .dll in the custom_macros folder (optional \":ClassName\")"
                : "");
            button4.Visible = custom;
            button5.Visible = custom;

            // The "Reply" row (text, destination ports, line ending) applies only to "Write back":
            // a custom macro chooses by itself which port to write to.
            bool reply = comboBox1.Text == EffectNameReply;
            comboBox2.Enabled = reply;
            textBox3.Enabled = reply;
            checkBox5.Enabled = checkBox6.Enabled = checkBox7.Enabled = checkBox8.Enabled = reply;
        }

        /// <summary>Shows the selected macro in the editor fields.</summary>
        private void DisplaySelectedScript()
        {
            _isLoading = true;
            textBox4.Text = "";
            comboBox1.Text = "";
            textBox3.Text = "";
            checkBox8.Checked = checkBox7.Checked = checkBox6.Checked = checkBox5.Checked = false;
            comboBox2.SelectedIndex = -1;

            if (checkedListBox1.SelectedItem is Script s)
            {
                textBox1.Text = s.Name;
                textBox2.Text = s.Key;
                checkBox1.Checked = s.ListenPort1;
                checkBox2.Checked = s.ListenPort2;
                checkBox3.Checked = s.ListenPort3;
                checkBox4.Checked = s.ListenPort4;

                switch (s.Effect)
                {
                    case MacroEffect.Keypress:
                        comboBox1.Text = EffectNameKeypress;
                        textBox4.Text = s.KeyPress;
                        break;

                    case MacroEffect.Reply:
                        comboBox1.Text = EffectNameReply;
                        textBox3.Text = s.Reply;
                        checkBox8.Checked = s.ReplyPort1;
                        checkBox7.Checked = s.ReplyPort2;
                        checkBox6.Checked = s.ReplyPort3;
                        checkBox5.Checked = s.ReplyPort4;
                        comboBox2.SelectedIndex = s.LineEndingIndex >= 0 && s.LineEndingIndex < comboBox2.Items.Count
                            ? s.LineEndingIndex
                            : 1;
                        break;

                    case MacroEffect.Custom:
                        comboBox1.Text = EffectNameCustom;
                        textBox4.Text = s.CustomFile;
                        break;
                }
            }

            UpdateEffectControls();
            _isLoading = false;
        }
    }
}
