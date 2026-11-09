using System.Collections;
using SerialPortMacros.Transport;

namespace SerialPortMacros
{
    /// <summary>
    /// Lists the nearby Bluetooth LE devices and lets the user choose one.
    /// The layout is edited in the designer (BleDeviceForm.Designer.cs); this file holds only the logic.
    /// </summary>
    public partial class BleDeviceForm : Form
    {
        private readonly BleScanner _scanner = new();
        private readonly Dictionary<ulong, BleDeviceInfo> _devices = new();
        private readonly Dictionary<ulong, ListViewItem> _items = new();

        /// <summary>Chosen device (0 = none). Valid after DialogResult.OK.</summary>
        public ulong SelectedAddress { get; private set; }
        public string SelectedName { get; private set; } = "";

        public BleDeviceForm()
        {
            InitializeComponent();
            listDevices.ListViewItemSorter = new DeviceComparer();

            // Scanner events arrive on a background thread: move them to the UI thread.
            _scanner.DeviceUpdated += device =>
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(() => OnDeviceUpdated(device)));
            };
            _scanner.Stopped += message =>
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(() => ShowStatus(message, true)));
            };
        }

        private async void BleDeviceForm_Load(object? sender, EventArgs e)
        {
            string? problem = await BleScanner.CheckAdapterAsync();
            if (problem != null)
            {
                ShowStatus(problem, true);
                return;
            }

            try
            {
                _scanner.Start();
                refreshTimer.Start();
                ShowStatus("Scanning...", false);
            }
            catch (Exception ex)
            {
                ShowStatus("Cannot scan: " + ex.Message, true);
            }
        }

        private void BleDeviceForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            refreshTimer.Stop();
            _scanner.Dispose();
        }

        // ------------------------------------------------------------------
        // List
        // ------------------------------------------------------------------

        private void OnDeviceUpdated(BleDeviceInfo device)
        {
            _devices[device.Address] = device;
            UpdateItem(device);
        }

        private bool IsVisible(BleDeviceInfo d) =>
            checkUartOnly.Checked ? d.HasUartService : (d.HasUartService || d.Name.Length > 0);

        private void UpdateItem(BleDeviceInfo d)
        {
            _items.TryGetValue(d.Address, out var item);

            if (!IsVisible(d))
            {
                if (item != null)
                {
                    listDevices.Items.Remove(item);
                    _items.Remove(d.Address);
                }
                return;
            }

            string name = d.Name.Length > 0 ? d.Name : "(no name)";
            string signal = $"{d.Rssi} dBm";
            string uart = d.HasUartService ? "yes" : "";

            if (item == null)
            {
                item = new ListViewItem(new[] { name, d.AddressText, signal, uart }) { Tag = d };
                _items[d.Address] = item;
                listDevices.Items.Add(item);
            }
            else
            {
                item.Tag = d;
                item.SubItems[0].Text = name;
                item.SubItems[2].Text = signal;
                item.SubItems[3].Text = uart;
            }
        }

        private void checkUartOnly_CheckedChanged(object? sender, EventArgs e)
        {
            listDevices.BeginUpdate();
            foreach (var d in _devices.Values)
                UpdateItem(d);
            listDevices.EndUpdate();
            UpdateCount();
        }

        // Once a second: sort (UART devices first, then by signal) and update the counter.
        private void refreshTimer_Tick(object? sender, EventArgs e)
        {
            listDevices.Sort();
            UpdateCount();
        }

        private void UpdateCount()
        {
            int hidden = _devices.Count - listDevices.Items.Count;
            string text = $"Scanning... {listDevices.Items.Count} device(s)";
            if (hidden > 0) text += $", {hidden} hidden";
            ShowStatus(text, false);
        }

        private void ShowStatus(string text, bool isError)
        {
            labelStatus.Text = text;
            labelStatus.ForeColor = isError ? Color.Red : Color.DimGray;
        }

        // ------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------

        private void listDevices_SelectedIndexChanged(object? sender, EventArgs e)
        {
            buttonSelect.Enabled = listDevices.SelectedItems.Count == 1;
        }

        private void buttonSelect_Click(object? sender, EventArgs e)
        {
            if (listDevices.SelectedItems.Count != 1 || listDevices.SelectedItems[0].Tag is not BleDeviceInfo d)
                return;

            SelectedAddress = d.Address;
            SelectedName = d.Name.Length > 0 ? d.Name : d.AddressText;
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Nordic UART devices first, then the strongest signal.</summary>
        private sealed class DeviceComparer : IComparer
        {
            public int Compare(object? x, object? y)
            {
                if ((x as ListViewItem)?.Tag is not BleDeviceInfo a || (y as ListViewItem)?.Tag is not BleDeviceInfo b)
                    return 0;
                int byUart = b.HasUartService.CompareTo(a.HasUartService);
                return byUart != 0 ? byUart : b.Rssi.CompareTo(a.Rssi);
            }
        }
    }
}
