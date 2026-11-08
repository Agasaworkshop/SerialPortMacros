using System.Diagnostics;
using System.IO.Ports;
using SerialPortMacros.Core;
using SerialPortMacros.Transport;

namespace SerialPortMacros
{
    // Connessione delle porte (seriale / TCP), impostazioni e formato dati.
    public partial class Form1
    {
        private bool _loadingSettings;
        private bool _scanningPorts;

        // ------------------------------------------------------------------
        // Connessione
        // ------------------------------------------------------------------

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i >= 0) ToggleConnection(i);
        }

        private void ToggleConnection(int i)
        {
            var ch = _hub.Channels[i];
            var ui = _ui[i];
            var ps = _settings.Ports[i];

            if (ch.Mode == ConnectionMode.Tcp)
            {
                if (ch.Tcp.IsListening)
                {
                    ch.Tcp.Stop();
                    ui.Value.Enabled = true;
                    SetConnectButton(i, false, "Connect");
                }
                else
                {
                    if (!int.TryParse(ui.Value.Text, out int tcpPort) || tcpPort < 1 || tcpPort > 65535)
                    {
                        MessageBox.Show("Enter a valid TCP port (1-65535).", "Invalid TCP port",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    int unique = FindAvailableTcpPort(i, tcpPort);
                    if (unique != tcpPort)
                    {
                        ui.Value.Text = unique.ToString();
                        ShowLine($"TCP port {tcpPort} is already used by an active Wi-Fi connection. Automatically assigned port {unique}.",
                            "System", Color.DarkOrange);
                    }
                    ps.TcpPort = unique;

                    if (ch.SerialOpen) ch.CloseSerial();

                    try
                    {
                        ch.Tcp.Start(ps.TcpPort);
                        ShowLine($"TCP listening on port {ps.TcpPort}", "System", Color.DarkGreen);
                        ui.Value.Enabled = false;
                        SetConnectButton(i, true, $"TCP listening on port {ps.TcpPort}");
                    }
                    catch (Exception ex)
                    {
                        // La modalità TCP resta selezionata: l'utente può correggere la porta e riprovare.
                        ShowLine($"Error starting TCP on port {i + 1}: {ex.Message}", "Error", Color.Red);
                    }
                }
            }
            else
            {
                if (ch.SerialOpen)
                {
                    ch.CloseSerial();
                    ui.Value.Enabled = true;
                    SetConnectButton(i, false, "Connect");
                }
                else
                {
                    if (!int.TryParse(ui.Value.Text, out int baud) || baud <= 0)
                    {
                        MessageBox.Show("Enter a valid baud rate.");
                        return;
                    }
                    if (string.IsNullOrEmpty(ui.Com.Text))
                        return;

                    if (!ch.OpenSerial(ui.Com.Text, baud, out string? error))
                    {
                        ShowLine($"Cannot open {ui.Com.Text}: {error}", "Error", Color.Red);
                        return;
                    }

                    ps.BaudRate = ui.Value.Text;
                    ui.Value.Enabled = false;
                    SetConnectButton(i, true, "Disconnect");
                }
            }

            UpdateIcon();
        }

        private void SetConnectButton(int i, bool connected, string tooltip)
        {
            _ui[i].Connect.Image = connected ? Properties.Resources.Disconnect : Properties.Resources.AddConnection;
            toolTip1.SetToolTip(_ui[i].Connect, tooltip);
        }

        // ------------------------------------------------------------------
        // Modalità seriale / TCP
        // ------------------------------------------------------------------

        private void TcpCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0) return;

            SetPortMode(i, _ui[i].Tcp.Checked);
            if (!_loadingSettings) SaveSettings();
        }

        private void SetPortMode(int i, bool useTcp)
        {
            var ch = _hub.Channels[i];
            var ui = _ui[i];
            var ps = _settings.Ports[i];

            var newMode = useTcp ? ConnectionMode.Tcp : ConnectionMode.Serial;
            var oldMode = ch.Mode;

            // Conserva il valore della modalità che si sta lasciando.
            if (!_loadingSettings && oldMode != newMode)
            {
                if (oldMode == ConnectionMode.Serial)
                    ps.BaudRate = ui.Value.Text;
                else if (int.TryParse(ui.Value.Text, out int p) && p is >= 1 and <= 65535)
                    ps.TcpPort = p;
            }

            if (oldMode != newMode)
            {
                if (newMode == ConnectionMode.Serial) ch.Tcp.Stop();
                else ch.CloseSerial();
            }

            ch.Mode = newMode;
            ui.ValueLabel.Text = useTcp ? "port" : "baud";
            ui.Com.Enabled = !useTcp;
            ui.Value.Enabled = true;
            ui.Value.Text = useTcp ? ps.TcpPort.ToString() : ps.BaudRate;

            if (useTcp && !_loadingSettings)
                EnsureUniqueTcpPort(i);

            SetConnectButton(i, false, "Connect");
            RefreshPortNames();
            UpdateIcon();
        }

        private int FindAvailableTcpPort(int i, int preferred)
        {
            // Una porta è "occupata" solo se un'altra porta logica in modalità TCP la usa.
            bool InUse(int candidate) => Enumerable.Range(0, _ui.Length).Any(j =>
                j != i && _hub.Channels[j].Mode == ConnectionMode.Tcp && _settings.Ports[j].TcpPort == candidate);

            if (preferred is >= 1 and <= 65535 && !InUse(preferred))
                return preferred;

            for (int candidate = 5001; candidate <= 65535; candidate++)
                if (!InUse(candidate)) return candidate;

            throw new InvalidOperationException("No TCP ports available.");
        }

        private void EnsureUniqueTcpPort(int i)
        {
            var ps = _settings.Ports[i];
            int requested = int.TryParse(_ui[i].Value.Text, out int typed) && typed is >= 1 and <= 65535
                ? typed
                : ps.TcpPort;

            int assigned = FindAvailableTcpPort(i, requested);
            ps.TcpPort = assigned;
            _ui[i].Value.Text = assigned.ToString();

            if (assigned != requested)
                ShowLine($"TCP port {requested} was already used by another active Wi-Fi connection. Automatically assigned port {assigned} to logical port {i + 1}.",
                    "System", Color.DarkOrange);
        }

        // ------------------------------------------------------------------
        // Elenco COM e nomi delle sorgenti
        // ------------------------------------------------------------------

        private void PortCombo_Click(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i >= 0) RefreshComPorts(i);
        }

        private void PortCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0) return;

            var ch = _hub.Channels[i];
            if (ch.SerialOpen && ch.Serial.PortName != _ui[i].Com.SelectedItem?.ToString())
            {
                ch.CloseSerial();
                _ui[i].Value.Enabled = true;
                SetConnectButton(i, false, "Connect");
                UpdateIcon();
            }

            RefreshPortNames();
        }

        private void RefreshComPorts(int i)
        {
            var combo = _ui[i].Com;
            string? previous = combo.SelectedItem as string;
            string[] available = SerialPort.GetPortNames();

            combo.Items.Clear();
            combo.Items.AddRange(available);

            if (!string.IsNullOrEmpty(previous) && available.Contains(previous))
                combo.SelectedItem = previous;
        }

        // GetPortNames() interroga il sistema e può essere lento: fuori dal thread UI.
        private async void PortScanTimer_Tick(object? sender, EventArgs e)
        {
            if (_scanningPorts) return;

            _scanningPorts = true;
            try
            {
                string[] available = await Task.Run(SerialPort.GetPortNames);
                if (IsDisposed) return;

                foreach (var ui in _ui)
                {
                    if (ui.Com.SelectedItem != null && !available.Contains(ui.Com.SelectedItem.ToString()))
                        ui.Com.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Port scan: " + ex.Message);
            }
            finally
            {
                _scanningPorts = false;
            }
        }

        /// <summary>
        /// Nome della sorgente per una porta logica (1..4): la COM selezionata, oppure "TCPn"
        /// se la porta è in modalità TCP. È la chiave usata anche per i grafici.
        /// </summary>
        private string GetGraphPortName(int logicalPort)
        {
            var ui = _ui[logicalPort - 1];
            if (!string.IsNullOrWhiteSpace(ui.Com.Text))
                return ui.Com.Text.Trim();

            return ui.Tcp.Checked ? $"TCP{logicalPort}" : string.Empty;
        }

        /// <summary>Comunica ai canali il nome da usare per i messaggi TCP (letto qui sul thread UI).</summary>
        private void RefreshPortNames()
        {
            for (int i = 0; i < _ui.Length; i++)
                _hub.Channels[i].TcpDisplayName = GetGraphPortName(i + 1);
        }

        // ------------------------------------------------------------------
        // Impostazioni della porta (parametri seriali + formato dati)
        // ------------------------------------------------------------------

        private void PortSettingsButton_Click(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0) return;

            var ch = _hub.Channels[i];
            string title = !string.IsNullOrEmpty(_ui[i].Com.Text) ? _ui[i].Com.Text : $"Port {i + 1}";

            using var form = new Form2(ch.Serial, ch.DataFormat, ch.Mode == ConnectionMode.Tcp) { Text = title };
            if (form.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                ch.SetDataFormat(form.DataFormat);
                _settings.Ports[i].DataFormat = form.DataFormat.Clone();
                SaveSettings();
            }
            catch (FormatException ex)
            {
                MessageBox.Show(ex.Message, "Invalid data format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
