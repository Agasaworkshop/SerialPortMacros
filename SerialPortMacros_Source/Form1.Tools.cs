using SerialPortMacros.Core;
using SerialPortMacros.Macros;

namespace SerialPortMacros
{
    // Macro, grafici, sequenze e interfacce.
    public partial class Form1
    {
        // ------------------------------------------------------------------
        // Macro
        // ------------------------------------------------------------------

        // Play / pausa delle macro
        private void button1_Click(object sender, EventArgs e)
        {
            if (!_macros.Running)
            {
                ScanScripts();           // ricarica (e ricompila) le macro prima di partire
                _macros.Running = true;
                button1.Image = Properties.Resources.Pause;
            }
            else
            {
                _macros.Running = false;
                button1.Image = Properties.Resources.Play;
            }
        }

        // Finestra delle macro
        private void button2_Click(object sender, EventArgs e)
        {
            ScanScripts();
            using (var form = new Form3(this))
                form.ShowDialog(this);
            ScanScripts();               // applica subito le modifiche fatte nella finestra
        }

        /// <summary>Rilegge i file macro e carica le macro custom; gli errori vanno a video.</summary>
        public void ScanScripts()
        {
            List<string> errors;
            var previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;   // la compilazione delle macro .cs può richiedere un secondo
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
        // Grafici
        // ------------------------------------------------------------------

        private Form4? _mergingForm;
        public bool looking_for_merge;

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

        public void merge_graphs1(string name, Form4 currentForm)
        {
            if (!looking_for_merge)
            {
                _mergingForm = currentForm;
                looking_for_merge = true;
            }
            else
            {
                if (_mergingForm != null)
                    CreateParasiticForm(_mergingForm, currentForm);
                looking_for_merge = false;
            }
        }

        public void undo_merge()
        {
            looking_for_merge = false;
            _mergingForm = null;
        }

        /// <summary>Crea un grafico "master" che mostra insieme le serie dei grafici uniti.</summary>
        public void CreateParasiticForm(Form4 f1, Form4 f2)
        {
            var slaves = new List<Form4>();

            void Collect(Form4 f)
            {
                if (f.isMaster)
                    slaves.AddRange(f.children.Values.Where(c => c.child != null).Select(c => c.child));
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
                master.children.Add(s, new child_form(s, ColorUtils.Distinct(i, slaves.Count), master.formsPlot1));
            }

            if (f1.isMaster) f1.Close();
            if (f2.isMaster) f2.Close();

            foreach (var s in slaves)
            {
                s.MasterForm = master;
                s.reset_merge();
                s.Visible = false;
                s.is_visible = false;
            }

            master.Show();
        }

        // ------------------------------------------------------------------
        // Interfacce (Form7)
        // ------------------------------------------------------------------

        // Nuova interfaccia
        private void button21_Click(object sender, EventArgs e)
        {
            new Form7(this, true, true).Show();
        }

        // Apri interfaccia
        private void button22_Click(object sender, EventArgs e)
        {
            string? file = PickFile(AppPaths.Interfaces, "Interfacce SPM (*.spm)|*.spm", "Apri interfaccia");
            if (file != null) OpenInterface(file, false);
        }

        // Apri interfaccia in modalità editor
        private void button23_Click(object sender, EventArgs e)
        {
            string? file = PickFile(AppPaths.Interfaces, "Interfacce SPM (*.spm)|*.spm", "Apri interfaccia da modificare");
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
        // Sequenze
        // ------------------------------------------------------------------

        private string? _currentSequence;

        // Play / pausa / riprendi
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

        // Scelta del file di sequenza
        private void button25_Click(object sender, EventArgs e)
        {
            string? file = PickFile(AppPaths.Sequences, "All files (*.*)|*.*", "Select a sequence");
            if (file == null) return;

            _currentSequence = file;
            button25.Text = Path.GetFileName(file);
        }

        /// <summary>Thread della sequenza: invio diretto, testo e log thread-safe.</summary>
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
