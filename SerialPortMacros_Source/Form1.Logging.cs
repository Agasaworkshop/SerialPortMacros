using System.Diagnostics;
using SerialPortMacros.Core;

namespace SerialPortMacros
{
    // Log sessions to file.
    public partial class Form1
    {
        private bool _stopRequested;
        private long _stopRequestTick;
        private CancellationTokenSource? _watchdogCts;

        private void button14_Click(object sender, EventArgs e)
        {
            if (!_log.IsActive)
                StartLogging();
            else
                RequestStopLogging();
        }

        private void StartLogging()
        {
            if (_settings.LogStartMessage != "")
                SendUserCommand(_settings.LogStartMessage);

            SetLogControlsEnabled(false);
            button14.Text = "Logging...";

            _log.LogUserInput = checkBox9.Checked;
            _log.Start(_ui.Select(u => u.Log.Checked).ToArray());
        }

        private void RequestStopLogging()
        {
            if (_stopRequested)
                return;

            _stopRequested = true;
            button14.Text = "Waiting...";

            // Logging stays active while the micro answers (e.g. "send me the log").
            if (_settings.LogStopMessage != "")
                SendUserCommand(_settings.LogStopMessage);

            if (!_settings.LogWaitForEnd)
            {
                FinishLogging(checkBox10.Checked);
                return;
            }

            // Closes when no data has arrived for 1 second.
            _stopRequestTick = Environment.TickCount64;
            StartWatchdog();
        }

        private void FinishLogging(bool openForms)
        {
            _stopRequested = false;
            _watchdogCts?.Cancel();
            _watchdogCts = null;

            string?[] paths = _log.Stop();

            SetLogControlsEnabled(true);
            button14.Text = "Start Logging";

            if (openForms)
                OpenLogForms(paths);
        }

        private async void StartWatchdog()
        {
            _watchdogCts?.Cancel();
            _watchdogCts = new CancellationTokenSource();
            var token = _watchdogCts.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(500, token);
                    if (!_stopRequested) return;

                    long last = Math.Max(_pipeline.LastMessageTick, _stopRequestTick);
                    if (Environment.TickCount64 - last >= 1000 && _pipeline.PendingCount == 0)
                    {
                        FinishLogging(true);
                        return;
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void SetLogControlsEnabled(bool enabled)
        {
            foreach (var u in _ui) u.Log.Enabled = enabled;
            checkBox9.Enabled = enabled;
        }

        private static void OpenLogForms(IEnumerable<string?> paths)
        {
            foreach (string? path in paths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    new Log(path).Show();
            }
        }

        // Opens the logs folder
        private void button7_Click_1(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = AppPaths.Logs, UseShellExecute = true });
        }

        // Dropping a log file on the label opens the viewer.
        private void label5_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void label5_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length != 1)
                return;

            if (File.Exists(files[0]))
                new Log(files[0]).Show();
        }

        // Log start/stop messages
        private void button20_Click(object sender, EventArgs e)
        {
            new Form6(this, _settings.LogStartMessage, _settings.LogStopMessage, _settings.LogWaitForEnd).Show();
        }

        public void Set_logging_messages(string startMessage, string stopMessage, bool waitForEnd)
        {
            _settings.LogStartMessage = startMessage;
            _settings.LogStopMessage = stopMessage;
            _settings.LogWaitForEnd = waitForEnd;
        }
    }
}
