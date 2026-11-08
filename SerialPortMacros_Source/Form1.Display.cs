using System.Runtime.InteropServices;
using System.Text;
using SerialPortMacros.Core;

namespace SerialPortMacros
{
    // On-screen text, user input and command sending.
    public partial class Form1
    {
        /// <summary>With autoscroll on, text older than this limit is discarded.</summary>
        private const int MaxChars = 30000;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;

        private readonly List<DisplayLine> _drained = new();
        private readonly StringBuilder _displaySb = new();

        private bool _autoScroll = true;
        private bool _timeStamps = true;
        private bool _holdDisplay;          // the user is selecting text: do not append

        // ------------------------------------------------------------------
        // Display
        // ------------------------------------------------------------------

        /// <summary>
        /// Adds a line to the on-screen text. From any thread; on the UI thread it
        /// is shown immediately.
        /// </summary>
        private void ShowLine(string text, string user, Color color)
        {
            _hub.Show(text, user, color);
            if (!InvokeRequired)
                FlushDisplay();
        }

        /// <summary>
        /// Takes the lines from the <see cref="DisplayBuffer"/> and writes them to the
        /// RichTextBox, grouping consecutive lines of the same color into one AppendText.
        /// </summary>
        private void FlushDisplay()
        {
            if (IsDisposed || _holdDisplay)
                return; // lines stay in the buffer (which limits itself)

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

            // With autoscroll, text beyond MaxChars would be discarded anyway: do not write it.
            int startIdx = 0;
            if (_autoScroll)
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

            bool suspend = _autoScroll && items.Count - startIdx > 1;
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

            if (_autoScroll)
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
            if (_autoScroll)
                ActiveControl = null;
            else
                _holdDisplay = true;
        }

        private void textBox2_Leave(object sender, EventArgs e)
        {
            if (!_autoScroll)
            {
                _holdDisplay = false;
                FlushDisplay();
            }
        }

        // Autoscroll
        private void button12_Click(object sender, EventArgs e)
        {
            SetAutoScroll(!_autoScroll);
        }

        private void SetAutoScroll(bool on)
        {
            _autoScroll = on;
            button12.Image = on ? Properties.Resources.Lock : Properties.Resources.Unlock;
            toolTip1.SetToolTip(button12, on ? "AutoScrolling on" : "AutoScrolling off");
            textBox2.ScrollBars = on ? RichTextBoxScrollBars.None : RichTextBoxScrollBars.Both;

            textBox2.SelectionStart = textBox2.TextLength;
            textBox2.SelectionLength = 0;
            textBox2.ScrollToCaret();
        }

        // Clear
        private void button13_Click(object sender, EventArgs e)
        {
            textBox2.Text = "";
        }

        // Timestamp
        private void button15_Click(object sender, EventArgs e)
        {
            SetTimeStamps(!_timeStamps);
        }

        private void SetTimeStamps(bool on)
        {
            _timeStamps = on;
            _pipeline.TimeStamps = on;
            _log.TimeStamps = on;
            button15.Image = on ? Properties.Resources.Time_color_16x : Properties.Resources.Time_16x_cross2;
            toolTip1.SetToolTip(button15, on ? "Timestamps on" : "Timestamps off");
        }

        // ------------------------------------------------------------------
        // User input and history (up/down arrows)
        // ------------------------------------------------------------------

        private const int MaxHistory = 50;
        private readonly List<string> _commandHistory = new();
        private int _historyIndex;            // 0 = no history entry active
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
        // Sending
        // ------------------------------------------------------------------

        private string SelectedLineEnding => LineEndings.FromIndex(comboBox6.SelectedIndex);

        /// <summary>User command: shown, sent to the checked ports and logged.</summary>
        private void SendUserCommand(string text)
        {
            ShowLine(text, "You", Color.Black);

            string data = text + SelectedLineEnding;
            for (int i = 0; i < _ui.Length; i++)
                if (_ui[i].Send.Checked)
                    _hub.Channels[i].Send(data);

            _log.WriteSent(text);
        }

        /// <summary>Keeps the multi-line messages of the interfaces in order.</summary>
        private readonly SemaphoreSlim _interfaceSendLock = new(1, 1);

        /// <summary>
        /// Sending from the interfaces (Form7). <paramref name="targetPorts"/> has 4 elements;
        /// the message may contain several lines separated by "\n", sent 50 ms apart.
        /// The pause is awaited, so the UI stays responsive; messages are sent one at a time.
        /// </summary>
        public async Task SendInterfaceMessageAsync(bool[] targetPorts, string message, string customLineEnding = "")
        {
            bool hasTarget = targetPorts != null && targetPorts.Length >= 4 && targetPorts.Take(4).Any(t => t);

            if (!hasTarget)
            {
                // No port: only record the action (e.g. toggling a variable).
                ShowLine(message, "Macro/Form7 (No Port)", Color.Purple);
                _log.WriteSent(message);
                return;
            }

            string lineEnding = LineEndings.FromName(customLineEnding) ?? SelectedLineEnding;
            string[] parts = message.Split(new[] { "\n", "\\n" }, StringSplitOptions.RemoveEmptyEntries);

            await _interfaceSendLock.WaitAsync();
            try
            {
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length == 0) continue;

                    _hub.SendTo(targetPorts!, trimmed + lineEnding);
                    ShowLine(trimmed, "Macro/Form7", Color.Purple);
                    _log.WriteSent(trimmed);

                    if (parts.Length > 1) await Task.Delay(50);
                }
            }
            finally
            {
                _interfaceSendLock.Release();
            }
        }
    }
}
