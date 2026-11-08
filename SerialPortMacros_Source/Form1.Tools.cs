using SerialPortMacros.Core;
using SerialPortMacros.Macros;

namespace SerialPortMacros
{
    // Macros, graphs, sequences and interfaces.
    public partial class Form1
    {
        // ------------------------------------------------------------------
        // Macros
        // ------------------------------------------------------------------

        // Macro play / pause
        private void button1_Click(object sender, EventArgs e)
        {
            if (!_macros.Running)
            {
                ScanScripts();           // reload (and recompile) the macros before starting
                _macros.Running = true;
                button1.Image = Properties.Resources.Pause;
            }
            else
            {
                _macros.Running = false;
                button1.Image = Properties.Resources.Play;
            }
        }

        // Macros window
        private void button2_Click(object sender, EventArgs e)
        {
            // Form3 edits its own copy of the macro files; the engine reloads them afterwards.
            using (var form = new Form3())
                form.ShowDialog(this);
            ScanScripts();               // apply the changes made in the window right away
        }

        /// <summary>Re-reads the macro files and loads the custom macros; errors are shown on screen.</summary>
        public void ScanScripts()
        {
            List<string> errors;
            var previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;   // compiling .cs macros can take a second
            try
            {
                errors = _macros.SetScripts(ScriptStore.LoadAll());
            }
            finally
            {
                Cursor.Current = previous;
            }

            foreach (string error in errors)
                ShowLine(error, "Macro error", Color.Red);
        }

        // ------------------------------------------------------------------
        // Graphs
        // ------------------------------------------------------------------

        private Form4? _mergingForm;
        private bool _lookingForMerge;

        private void GraphButton_Click(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0) return;

            if (_ui[i].Com.Text != "" || _ui[i].Tcp.Checked)
                LaunchGraphs((int)_ui[i].GraphCount.Value, GetGraphPortName(i + 1));
        }

        private void LaunchGraphs(int count, string source)
        {
            if (_graphs.Open.TryGetValue(source, out var existing))
            {
                foreach (var form in existing.ToArray())
                    form.Close();
                existing.Clear();
            }
            else
            {
                existing = new List<Form4>();
                _graphs.Open[source] = existing;
            }

            for (int i = 1; i <= count; i++)
            {
                var form = new Form4(this) { Text = $"{source} {i}" };
                form.Show();
                existing.Add(form);
            }

            _graphs.Publish();
        }

        public void MergeGraph(Form4 currentForm)
        {
            if (!_lookingForMerge)
            {
                _mergingForm = currentForm;
                _lookingForMerge = true;
            }
            else
            {
                if (_mergingForm != null)
                    CreateParasiticForm(_mergingForm, currentForm);
                _lookingForMerge = false;
            }
        }

        public void CancelMerge()
        {
            _lookingForMerge = false;
            _mergingForm = null;
        }

        /// <summary>Creates a "master" graph showing the series of the merged graphs together.</summary>
        public void CreateParasiticForm(Form4 f1, Form4 f2)
        {
            var slaves = new List<Form4>();

            void Collect(Form4 f)
            {
                if (f.IsMaster)
                    slaves.AddRange(f.Children.Values.Where(c => c.Child != null).Select(c => c.Child));
                else
                    slaves.Add(f);
            }

            Collect(f1);
            Collect(f2);
            slaves = slaves.Distinct().ToList();

            var master = new Form4(this, true)
            {
                Text = "Merged: " + string.Join(" ", slaves.Select(s => s.Text))
            };

            for (int i = 0; i < slaves.Count; i++)
            {
                var s = slaves[i];
                master.Children.Add(s, new MergedChild(s, ColorUtils.Distinct(i, slaves.Count), master.formsPlot1));
            }

            if (f1.IsMaster) f1.Close();
            if (f2.IsMaster) f2.Close();

            foreach (var s in slaves)
            {
                s.MasterForm = master;
                s.ResetMerge();
                s.Visible = false;
                s.AcceptsData = false;
            }

            master.Show();
        }

        // ------------------------------------------------------------------
        // Interfaces (Form7)
        // ------------------------------------------------------------------

        // New interface
        private void button21_Click(object sender, EventArgs e)
        {
            new Form7(this, true, true).Show();
        }

        // Open interface
        private void button22_Click(object sender, EventArgs e)
        {
            string? file = PickFile(AppPaths.Interfaces, "SPM interfaces (*.spm)|*.spm", "Open interface");
            if (file != null) OpenInterface(file, false);
        }

        // Open interface in editor mode
        private void button23_Click(object sender, EventArgs e)
        {
            string? file = PickFile(AppPaths.Interfaces, "SPM interfaces (*.spm)|*.spm", "Open interface to edit");
            if (file != null) OpenInterface(file, true);
        }

        public void OpenInterface(string filePath, bool isEdit)
        {
            var form = new Form7(this, isEdit) { Text = Path.GetFileNameWithoutExtension(filePath) };
            form.LoadDocument(filePath);
            form.Show();
        }

        private static string? PickFile(string folder, string filter, string title)
        {
            using var dialog = new OpenFileDialog
            {
                InitialDirectory = folder,
                Filter = filter,
                Title = title,
                CheckFileExists = true,
                Multiselect = false
            };
            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        }

        // ------------------------------------------------------------------
        // Sequences
        // ------------------------------------------------------------------

        private string? _currentSequence;

        // Play / pause / resume
        private async void button24_Click(object sender, EventArgs e)
        {
            if (_sequence.IsRunning)
            {
                if (_sequence.IsPaused)
                {
                    _sequence.Resume();
                    button24.Image = Properties.Resources.Pause;
                }
                else
                {
                    _sequence.Pause();
                    button24.Image = Properties.Resources.Play;
                }
                return;
            }

            if (string.IsNullOrEmpty(_currentSequence) || !File.Exists(_currentSequence))
            {
                MessageBox.Show("Please select a sequence");
                return;
            }

            button24.Image = Properties.Resources.Pause;
            try
            {
                await _sequence.RunAsync(_currentSequence);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                button24.Image = Properties.Resources.Play;
            }
        }

        // Sequence file selection
        private void button25_Click(object sender, EventArgs e)
        {
            string? file = PickFile(AppPaths.Sequences, "All files (*.*)|*.*", "Select a sequence");
            if (file == null) return;

            _currentSequence = file;
            button25.Text = Path.GetFileName(file);
        }

        /// <summary>Sequence thread: direct send, thread-safe text and log.</summary>
        private void OnSequenceCommand(int[] ports, string message)
        {
            foreach (int p in ports)
                _hub.Send(p, message);

            string shown = message.TrimEnd('\r', '\n');
            _hub.Show(shown, "Sequence", Color.Purple);
            _log.WriteSent(shown);
        }
    }
}
