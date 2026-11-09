using SerialPortMacros.Core;
using SerialPortMacros.Decoding;
using SerialPortMacros.Transport;

namespace SerialPortMacros
{
    // Reading and writing settings.json.
    public partial class Form1
    {
        private void LoadSettings()
        {
            try
            {
                _settings = AppSettings.Load(AppPaths.SettingsFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _settings = new AppSettings();
                _settings.Normalize();
            }

            ApplySettingsToControls();
        }

        private void ApplySettingsToControls()
        {
            _loadingSettings = true;
            try
            {
                _display.MaxPendingLines = _settings.DisplayMaxPendingLines;

                for (int i = 0; i < _ui.Length; i++)
                {
                    var ps = _settings.Ports[i];
                    var ui = _ui[i];
                    var ch = _hub.Channels[i];

                    ui.Send.Checked = ps.SendEnabled;
                    ui.Log.Checked = ps.LogEnabled;
                    ui.GraphCount.Value = Math.Clamp(ps.GraphCount, (int)ui.GraphCount.Minimum, (int)ui.GraphCount.Maximum);

                    ch.Serial.Parity = SerialPortHelpers.ParseParity(ps.Parity);
                    ch.Serial.DataBits = ps.DataBits;
                    ch.Serial.StopBits = SerialPortHelpers.ParseStopBits(ps.StopBits);
                    if (ps.CustomBuffer && ps.ReadBufferSize > 0)
                        ch.Serial.ReadBufferSize = ps.ReadBufferSize;

                    try
                    {
                        ch.SetDataFormat(ps.DataFormat);
                    }
                    catch (FormatException ex)
                    {
                        ShowLine($"Port {i + 1}: invalid binary format ({ex.Message}), using text mode.", "System", Color.DarkOrange);
                        ps.DataFormat.Mode = DataMode.Text;
                        ch.SetDataFormat(ps.DataFormat);
                    }

                    // Set the mode selector (its event would call SetPortMode: it is called explicitly here).
                    ui.Mode.SelectedIndex = (int)ps.Mode;
                    SetPortMode(i, ps.Mode);
                }

                if (_settings.LineEndIndex >= 0 && _settings.LineEndIndex < comboBox6.Items.Count)
                    comboBox6.SelectedIndex = _settings.LineEndIndex;

                checkBox9.Checked = _settings.LogUserInput;
                checkBox10.Checked = _settings.OpenLogsAfterStop;

                SetTimeStamps(_settings.TimeStamps);
                SetAutoScroll(_settings.AutoScroll);
            }
            finally
            {
                _loadingSettings = false;
            }

            // If the file contains duplicate TCP ports among the active TCP modes, fix them.
            for (int i = 0; i < _ui.Length; i++)
                if (_hub.Channels[i].Mode == ConnectionMode.Tcp)
                    EnsureUniqueTcpPort(i);
        }

        private void SaveSettings()
        {
            try
            {
                for (int i = 0; i < _ui.Length; i++)
                {
                    var ps = _settings.Ports[i];
                    var ui = _ui[i];
                    var ch = _hub.Channels[i];

                    ps.SendEnabled = ui.Send.Checked;
                    ps.LogEnabled = ui.Log.Checked;
                    ps.Mode = ch.Mode;
                    ps.UseTcp = ch.Mode == ConnectionMode.Tcp;
                    ps.GraphCount = (int)ui.GraphCount.Value;

                    if (ch.Mode == ConnectionMode.Serial)
                        ps.BaudRate = ui.Value.Text;
                    else if (int.TryParse(ui.Value.Text, out int tcpPort) && tcpPort is >= 1 and <= 65535)
                        ps.TcpPort = tcpPort;

                    ps.Parity = SerialPortHelpers.ToText(ch.Serial.Parity);
                    ps.DataBits = ch.Serial.DataBits;
                    ps.StopBits = SerialPortHelpers.ToText(ch.Serial.StopBits);
                    ps.ReadBufferSize = ch.Serial.ReadBufferSize;
                    ps.CustomBuffer = ch.Serial.ReadBufferSize != 4096;
                    ps.DataFormat = ch.DataFormat.Clone();
                }

                _settings.LineEndIndex = comboBox6.SelectedIndex;
                _settings.LogUserInput = checkBox9.Checked;
                _settings.OpenLogsAfterStop = checkBox10.Checked;
                _settings.TimeStamps = _timeStamps;
                _settings.AutoScroll = _autoScroll;

                _settings.Save(AppPaths.SettingsFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
