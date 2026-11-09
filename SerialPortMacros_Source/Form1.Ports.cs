using System.Diagnostics;
using System.IO.Ports;
using SerialPortMacros.Core;
using SerialPortMacros.Transport;

namespace SerialPortMacros
{
    // Port connection (serial / TCP / Bluetooth LE), settings and data format.
    public partial class Form1
    {
        private bool _loadingSettings;
        private bool _scanningPorts;
        private bool _revertingMode;   // set while the mode selector is put back after a cancelled BLE choice

        // ------------------------------------------------------------------
        // Connection
        // ------------------------------------------------------------------

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i >= 0) await ToggleConnectionAsync(i);
        }

        private async Task ToggleConnectionAsync(int i)
        {
            var ch = _hub.Channels[i];
            var ui = _ui[i];
            var ps = _settings.Ports[i];

            switch (ch.Mode)
            {
                case ConnectionMode.Tcp:
                    ToggleTcp(i);
                    break;

                case ConnectionMode.Ble:
                    await ToggleBleAsync(i);
                    break;

                default:
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
                    break;
            }

            UpdateIcon();
        }

        private void ToggleTcp(int i)
        {
            var ch = _hub.Channels[i];
            var ui = _ui[i];
            var ps = _settings.Ports[i];

            if (ch.Tcp.IsListening)
            {
                ch.Tcp.Stop();
                ui.Value.Enabled = true;
                SetConnectButton(i, false, "Connect");
                return;
            }

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

            try
            {
                ch.Tcp.Start(ps.TcpPort);
                ShowLine($"TCP listening on port {ps.TcpPort}", "System", Color.DarkGreen);
                ui.Value.Enabled = false;
                SetConnectButton(i, true, $"TCP listening on port {ps.TcpPort}");
            }
            catch (Exception ex)
            {
                // TCP mode stays selected: the user can fix the port and retry.
                ShowLine($"Error starting TCP on port {i + 1}: {ex.Message}", "Error", Color.Red);
            }
        }

        private async Task ToggleBleAsync(int i)
        {
            var ch = _hub.Channels[i];
            var ui = _ui[i];
            var ps = _settings.Ports[i];

            if (ch.Ble.IsOpen)
            {
                ch.Ble.Disconnect();
                ShowLine($"BLE disconnected from {ps.BleName}", "System", Color.DarkGreen);
                SetConnectButton(i, false, "Connect");
                return;
            }

            if (ps.BleAddress == 0 && !ChooseBleDevice(i))
                return;

            ui.Connect.Enabled = false;
            ui.Mode.Enabled = false;
            toolTip1.SetToolTip(ui.Connect, "Connecting...");
            ShowLine($"Connecting to {ps.BleName} ({BleDeviceInfo.FormatAddress(ps.BleAddress)})...", "System", Color.DarkGreen);
            try
            {
                bool ok = await ch.Ble.ConnectAsync(ps.BleAddress, ps.BleName);
                SetConnectButton(i, ok, ok ? $"Connected to {ps.BleName}" : "Connect");
            }
            finally
            {
                ui.Connect.Enabled = true;
                ui.Mode.Enabled = true;
            }
        }

        private void SetConnectButton(int i, bool connected, string tooltip)
        {
            _ui[i].Connect.Image = connected ? Properties.Resources.Disconnect : Properties.Resources.AddConnection;
            toolTip1.SetToolTip(_ui[i].Connect, tooltip);
        }

        // ------------------------------------------------------------------
        // Serial / TCP / BLE mode
        // ------------------------------------------------------------------

        private ConnectionMode SelectedMode(int i) =>
            _ui[i].Mode.SelectedIndex is >= 0 and <= 2 ? (ConnectionMode)_ui[i].Mode.SelectedIndex : ConnectionMode.Serial;

        private void ModeBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0 || _revertingMode) return;

            var mode = SelectedMode(i);

            // Choosing BLE opens the device list; if nothing is chosen, go back to the previous mode.
            if (mode == ConnectionMode.Ble && !_loadingSettings && _settings.Ports[i].BleAddress == 0 && !ChooseBleDevice(i))
            {
                _revertingMode = true;
                _ui[i].Mode.SelectedIndex = (int)_hub.Channels[i].Mode;
                _revertingMode = false;
                return;
            }

            SetPortMode(i, mode);
            if (!_loadingSettings) SaveSettings();
        }

        private void SetPortMode(int i, ConnectionMode newMode)
        {
            var ch = _hub.Channels[i];
            var ui = _ui[i];
            var ps = _settings.Ports[i];
            var oldMode = ch.Mode;

            // Keep the value of the mode being left.
            if (!_loadingSettings && oldMode != newMode)
            {
                if (oldMode == ConnectionMode.Serial)
                    ps.BaudRate = ui.Value.Text;
                else if (oldMode == ConnectionMode.Tcp && int.TryParse(ui.Value.Text, out int p) && p is >= 1 and <= 65535)
                    ps.TcpPort = p;
            }

            // Close whatever the old mode had open.
            if (oldMode != newMode)
            {
                ch.CloseSerial();
                ch.Tcp.Stop();
                ch.Ble.Disconnect();
            }

            ch.Mode = newMode;
            ps.Mode = newMode;
            ps.UseTcp = newMode == ConnectionMode.Tcp;

            switch (newMode)
            {
                case ConnectionMode.Serial:
                    ui.ValueLabel.Text = "baud";
                    ui.Value.Text = ps.BaudRate;
                    ui.Value.Enabled = true;
                    ui.Com.Enabled = true;
                    if (oldMode == ConnectionMode.Ble)
                        RefreshComPorts(i);   // the list was showing the BLE device
                    break;

                case ConnectionMode.Tcp:
                    ui.ValueLabel.Text = "port";
                    ui.Value.Text = ps.TcpPort.ToString();
                    ui.Value.Enabled = true;
                    ui.Com.Enabled = false;
                    if (oldMode == ConnectionMode.Ble)
                        ui.Com.Items.Clear();
                    if (!_loadingSettings)
                        EnsureUniqueTcpPort(i);
                    break;

                case ConnectionMode.Ble:
                    ui.ValueLabel.Text = "BLE";
                    ui.Value.Text = "";
                    ui.Value.Enabled = false;
                    ui.Com.Enabled = true;   // clicking it opens the device list
                    ShowBleDeviceInCombo(i);
                    break;
            }

            SetConnectButton(i, false, "Connect");
            RefreshPortNames();
            UpdateIcon();
        }

        // ------------------------------------------------------------------
        // Bluetooth LE device
        // ------------------------------------------------------------------

        /// <summary>Opens the device list. Returns true if a device was chosen.</summary>
        private bool ChooseBleDevice(int i)
        {
            using var form = new BleDeviceForm { Text = $"Bluetooth LE device for port {i + 1}" };
            if (form.ShowDialog(this) != DialogResult.OK || form.SelectedAddress == 0)
                return false;

            var ps = _settings.Ports[i];
            var ch = _hub.Channels[i];

            if (ch.Ble.IsOpen && ps.BleAddress != form.SelectedAddress)
            {
                ch.Ble.Disconnect();
                SetConnectButton(i, false, "Connect");
            }

            ps.BleAddress = form.SelectedAddress;
            ps.BleName = form.SelectedName;

            if (ch.Mode == ConnectionMode.Ble)
                ShowBleDeviceInCombo(i);
            RefreshPortNames();
            SaveSettings();
            return true;
        }

        /// <summary>In BLE mode the COM list shows the chosen device.</summary>
        private void ShowBleDeviceInCombo(int i)
        {
            var combo = _ui[i].Com;
            var ps = _settings.Ports[i];

            combo.Items.Clear();
            if (ps.BleAddress != 0)
            {
                combo.Items.Add(string.IsNullOrWhiteSpace(ps.BleName) ? BleDeviceInfo.FormatAddress(ps.BleAddress) : ps.BleName);
                combo.SelectedIndex = 0;
            }
        }

        // ------------------------------------------------------------------
        // TCP port uniqueness
        // ------------------------------------------------------------------

        private int FindAvailableTcpPort(int i, int preferred)
        {
            // A port is "in use" only if another logical port in TCP mode uses it.
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
        // COM list and source names
        // ------------------------------------------------------------------

        private void PortCombo_Click(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0) return;

            if (_hub.Channels[i].Mode == ConnectionMode.Ble)
            {
                _ui[i].Com.DroppedDown = false;
                BeginInvoke(new Action(() => ChooseBleDevice(i)));
            }
            else
            {
                RefreshComPorts(i);
            }
        }

        private void PortCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0) return;

            var ch = _hub.Channels[i];
            if (ch.Mode == ConnectionMode.Serial && ch.SerialOpen &&
                ch.Serial.PortName != _ui[i].Com.SelectedItem?.ToString())
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

        // GetPortNames() queries the system and can be slow: keep it off the UI thread.
        private async void PortScanTimer_Tick(object? sender, EventArgs e)
        {
            if (_scanningPorts) return;

            _scanningPorts = true;
            try
            {
                string[] available = await Task.Run(SerialPort.GetPortNames);
                if (IsDisposed) return;

                for (int i = 0; i < _ui.Length; i++)
                {
                    var ui = _ui[i];
                    if (_hub.Channels[i].Mode == ConnectionMode.Ble)
                        continue;   // the list shows the BLE device, not a COM port

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
        /// Source name of a logical port (1..4): the name shown in the list (COM port or
        /// BLE device), or "TCPn"/"BLEn". It is also the key used for the graphs.
        /// </summary>
        private string GetGraphPortName(int logicalPort)
        {
            var ui = _ui[logicalPort - 1];
            if (!string.IsNullOrWhiteSpace(ui.Com.Text))
                return ui.Com.Text.Trim();

            return _hub.Channels[logicalPort - 1].Mode switch
            {
                ConnectionMode.Tcp => $"TCP{logicalPort}",
                ConnectionMode.Ble => $"BLE{logicalPort}",
                _ => string.Empty
            };
        }

        /// <summary>Tells the channels which name to use for TCP/BLE messages (read here on the UI thread).</summary>
        private void RefreshPortNames()
        {
            for (int i = 0; i < _ui.Length; i++)
                _hub.Channels[i].NetworkDisplayName = GetGraphPortName(i + 1);
        }

        // ------------------------------------------------------------------
        // Port settings (serial parameters + data format)
        // ------------------------------------------------------------------

        private void PortSettingsButton_Click(object? sender, EventArgs e)
        {
            int i = PortIndexOf(sender);
            if (i < 0) return;

            var ch = _hub.Channels[i];
            string title = !string.IsNullOrEmpty(_ui[i].Com.Text) ? _ui[i].Com.Text : $"Port {i + 1}";

            // Serial parameters only apply in serial mode.
            using var form = new Form2(ch.Serial, ch.DataFormat, ch.Mode != ConnectionMode.Serial) { Text = title };
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
