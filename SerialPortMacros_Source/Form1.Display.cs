using System.Runtime.InteropServices;
using System.Text;
using SerialPortMacros.Core;

namespace SerialPortMacros
{
    // Testo a video, input dell'utente e invio dei comandi.
    public partial class Form1
    {
        /// <summary>Con l'autoscroll attivo il testo più vecchio di questa soglia viene scartato.</summary>
        private const int MaxChars = 30000;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;

        private readonly List<DisplayLine> _drained = new();
        private readonly StringBuilder _displaySb = new();

        private bool is_locked = true;      // autoscroll
        private bool time_log = true;       // timestamp
        private bool stop_text;             // l'utente sta selezionando del testo

        // ------------------------------------------------------------------
        // Visualizzazione
        // ------------------------------------------------------------------

        /// <summary>
        /// Aggiunge una riga al testo a video. Da qualunque thread; sul thread UI
        /// viene mostrata subito.
        /// </summary>
        private void ShowLine(string text, string user, Color color)
        {
            _hub.Show(text, user, color);
            if (!InvokeRequired)
                FlushDisplay();
        }

        /// <summary>
        /// Preleva le righe dal <see cref="DisplayBuffer"/> e le scrive nella RichTextBox,
        /// raggruppando le righe consecutive dello stesso colore in un solo AppendText.
        /// </summary>
        private void FlushDisplay()
        {
            if (IsDisposed || stop_text)
                return; // le righe restano nel buffer (che si limita da solo)

            long dropped = _display.DrainTo(_drained);
            if (_drained.Count == 0 && dropped == 0)
                return;

            if (dropped > 0)
                _drained.Insert(0, new DisplayLine(
                    $"··· {dropped} lines not shown (display decimated; logs, macros and graphs received everything) ···",
                    Color.Gray));

            try
            {
                WriteLines(_drained);
            }
            finally
            {
                _drained.Clear();
            }
        }

        private void WriteLines(List<DisplayLine> items)
        {
            int selStart = textBox2.SelectionStart;
            int selLength = textBox2.SelectionLength;

            // Con l'autoscroll il testo oltre MaxChars verrebbe comunque scartato: non scriverlo.
            int startIdx = 0;
            if (is_locked)
            {
                int total = 0;
                for (int k = items.Count - 1; k >= 0; k--)
                {
                    total += items[k].Text.Length + 2;
                    if (total > MaxChars)
                    {
                        startIdx = k + 1;
                        break;
                    }
                }

                if (textBox2.TextLength > MaxChars)
                    textBox2.Text = "";
            }

            bool suspend = is_locked && items.Count - startIdx > 1;
            if (suspend)
                SendMessage(textBox2.Handle, WM_SETREDRAW, 0, 0);

            try
            {
                bool hasText = textBox2.TextLength > 0;
                int i = startIdx;
                while (i < items.Count)
                {
                    Color c = items[i].Color;
                    _displaySb.Clear();

                    while (i < items.Count && items[i].Color == c)
                    {
                        if (hasText) _displaySb.Append(Environment.NewLine);
                        _displaySb.Append(items[i].Text);
                        hasText = true;
                        i++;
                    }

                    textBox2.SelectionStart = textBox2.TextLength;
                    textBox2.SelectionLength = 0;
                    textBox2.SelectionColor = c;
                    textBox2.AppendText(_displaySb.ToString());
                }
            }
            finally
            {
                if (suspend)
                {
                    SendMessage(textBox2.Handle, WM_SETREDRAW, 1, 0);
                    textBox2.Invalidate();
                }
            }

            if (is_locked)
            {
                textBox2.ScrollToCaret();
            }
            else
            {
                textBox2.SelectionStart = selStart;
                textBox2.SelectionLength = selLength;
            }
        }

        private void textBox2_Enter(object sender, EventArgs e)
        {
            if (is_locked)
                ActiveControl = null;
            else
                stop_text = true;
        }

        private void textBox2_Leave(object sender, EventArgs e)
        {
            if (!is_locked)
            {
                stop_text = false;
                FlushDisplay();
            }
        }

        // Autoscroll
        private void button12_Click(object sender, EventArgs e)
        {
            SetAutoScroll(!is_locked);
        }

        private void SetAutoScroll(bool on)
        {
            is_locked = on;
            button12.Image = on ? Properties.Resources.Lock : Properties.Resources.Unlock;
            toolTip1.SetToolTip(button12, on ? "AutoScrolling on" : "AutoScrolling off");
            textBox2.ScrollBars = on ? RichTextBoxScrollBars.None : RichTextBoxScrollBars.Both;

            textBox2.SelectionStart = textBox2.TextLength;
            textBox2.SelectionLength = 0;
            textBox2.ScrollToCaret();
        }

        // Pulisci
        private void button13_Click(object sender, EventArgs e)
        {
            textBox2.Text = "";
        }

        // Timestamp
        private void button15_Click(object sender, EventArgs e)
        {
            SetTimeStamps(!time_log);
        }

        private void SetTimeStamps(bool on)
        {
            time_log = on;
            _pipeline.TimeStamps = on;
            _log.TimeStamps = on;
            button15.Image = on ? Properties.Resources.Time_color_16x : Properties.Resources.Time_16x_cross2;
            toolTip1.SetToolTip(button15, on ? "Timestamps on" : "Timestamps off");
        }

        // ------------------------------------------------------------------
        // Input dell'utente e cronologia (frecce su/giù)
        // ------------------------------------------------------------------

        private const int MaxHistory = 50;
        private readonly List<string> _commandHistory = new();
        private int _historyIndex;            // 0 = nessuna voce della cronologia attiva
        private bool _changeDueToKey;

        private void textBox1_Keypress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)Keys.Enter)
                return;

            e.Handled = true;
            string input = textBox1.Text;

            if (!string.IsNullOrWhiteSpace(input))
            {
                _commandHistory.Add(input);
                if (_commandHistory.Count > MaxHistory)
                    _commandHistory.RemoveAt(0);
            }

            _historyIndex = 0;
            textBox1.Text = "";
            SendUserCommand(input);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (_historyIndex != 0 && textBox1.Focused && !_changeDueToKey)
                _historyIndex = 0;
            _changeDueToKey = false;
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down)
                return;

            e.SuppressKeyPress = true;
            if (_commandHistory.Count == 0)
                return;

            if (e.KeyCode == Keys.Up)
            {
                _historyIndex++;
                if (_historyIndex > _commandHistory.Count)
                    _historyIndex = 1;
                _changeDueToKey = true;
                textBox1.Text = _commandHistory[^_historyIndex];
            }
            else if (_historyIndex > 0)
            {
                _historyIndex--;
                if (_historyIndex == 0)
                {
                    textBox1.Text = "";
                }
                else
                {
                    _changeDueToKey = true;
                    textBox1.Text = _commandHistory[^_historyIndex];
                }
            }

            textBox1.SelectionStart = textBox1.Text.Length;
        }

        // ------------------------------------------------------------------
        // Invio
        // ------------------------------------------------------------------

        private string SelectedLineEnding => LineEndings.FromIndex(comboBox6.SelectedIndex);

        /// <summary>Comando dell'utente: mostrato, inviato alle porte spuntate e loggato.</summary>
        private void SendUserCommand(string text)
        {
            ShowLine(text, "You", Color.Black);

            string data = text + SelectedLineEnding;
            for (int i = 0; i < _ui.Length; i++)
                if (_ui[i].Send.Checked)
                    _hub.Channels[i].Send(data);

            _log.WriteSent(text);
        }

        /// <summary>
        /// Invio dalle interfacce (Form7). <paramref name="targetPorts"/> ha 4 elementi;
        /// il messaggio può contenere più righe separate da "\n".
        /// </summary>
        public void SendCustomMacroLine(bool[] targetPorts, string message, string customLineEnding = "")
        {
            bool hasTarget = targetPorts != null && targetPorts.Length >= 4 && targetPorts.Take(4).Any(t => t);

            if (!hasTarget)
            {
                // Nessuna porta: traccia solo l'azione (es. toggle di una variabile).
                ShowLine(message, "Macro/Form7 (No Port)", Color.Purple);
                _log.WriteSent(message);
                return;
            }

            string lineEnding = LineEndings.FromName(customLineEnding) ?? SelectedLineEnding;
            string[] parts = message.Split(new[] { "\n", "\\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0) continue;

                _hub.SendTo(targetPorts!, trimmed + lineEnding);
                ShowLine(trimmed, "Macro/Form7", Color.Purple);
                _log.WriteSent(trimmed);

                if (parts.Length > 1) Thread.Sleep(50);
            }
        }
    }
}
