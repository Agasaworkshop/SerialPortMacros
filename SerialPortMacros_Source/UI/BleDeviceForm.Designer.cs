namespace SerialPortMacros
{
    partial class BleDeviceForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            labelHelp = new Label();
            listDevices = new ListView();
            columnName = new ColumnHeader();
            columnAddress = new ColumnHeader();
            columnSignal = new ColumnHeader();
            columnUart = new ColumnHeader();
            checkUartOnly = new CheckBox();
            labelStatus = new Label();
            buttonSelect = new Button();
            buttonCancel = new Button();
            refreshTimer = new System.Windows.Forms.Timer(components);
            SuspendLayout();
            // 
            // labelHelp
            // 
            labelHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelHelp.Location = new Point(12, 9);
            labelHelp.Name = "labelHelp";
            labelHelp.Size = new Size(536, 47);
            labelHelp.TabIndex = 0;
            labelHelp.Text = "Turn the device on and choose it from the list (double click or Select).\r\nDevices that offer the Nordic UART service are marked in the UART column.";
            // 
            // listDevices
            // 
            listDevices.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listDevices.Columns.AddRange(new ColumnHeader[] { columnName, columnAddress, columnSignal, columnUart });
            listDevices.FullRowSelect = true;
            listDevices.Location = new Point(12, 59);
            listDevices.MultiSelect = false;
            listDevices.Name = "listDevices";
            listDevices.Size = new Size(536, 243);
            listDevices.TabIndex = 1;
            listDevices.UseCompatibleStateImageBehavior = false;
            listDevices.View = View.Details;
            listDevices.SelectedIndexChanged += listDevices_SelectedIndexChanged;
            listDevices.DoubleClick += buttonSelect_Click;
            // 
            // columnName
            // 
            columnName.Text = "Name";
            columnName.Width = 210;
            // 
            // columnAddress
            // 
            columnAddress.Text = "Address";
            columnAddress.Width = 150;
            // 
            // columnSignal
            // 
            columnSignal.Text = "Signal";
            columnSignal.Width = 80;
            // 
            // columnUart
            // 
            columnUart.Text = "UART";
            columnUart.Width = 70;
            // 
            // checkUartOnly
            // 
            checkUartOnly.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            checkUartOnly.AutoSize = true;
            checkUartOnly.Checked = true;
            checkUartOnly.CheckState = CheckState.Checked;
            checkUartOnly.Location = new Point(12, 305);
            checkUartOnly.Name = "checkUartOnly";
            checkUartOnly.Size = new Size(347, 24);
            checkUartOnly.TabIndex = 2;
            checkUartOnly.Text = "Show only devices with the Nordic UART service";
            checkUartOnly.UseVisualStyleBackColor = true;
            checkUartOnly.CheckedChanged += checkUartOnly_CheckedChanged;
            // 
            // labelStatus
            // 
            labelStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelStatus.ForeColor = Color.DimGray;
            labelStatus.Location = new Point(12, 341);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(338, 20);
            labelStatus.TabIndex = 3;
            labelStatus.Text = "Starting...";
            // 
            // buttonSelect
            // 
            buttonSelect.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonSelect.Enabled = false;
            buttonSelect.Location = new Point(362, 334);
            buttonSelect.Name = "buttonSelect";
            buttonSelect.Size = new Size(90, 30);
            buttonSelect.TabIndex = 4;
            buttonSelect.Text = "Select";
            buttonSelect.UseVisualStyleBackColor = true;
            buttonSelect.Click += buttonSelect_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(458, 334);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(90, 30);
            buttonCancel.TabIndex = 5;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            // 
            // refreshTimer
            // 
            refreshTimer.Interval = 1000;
            refreshTimer.Tick += refreshTimer_Tick;
            // 
            // BleDeviceForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(560, 376);
            Controls.Add(buttonCancel);
            Controls.Add(buttonSelect);
            Controls.Add(labelStatus);
            Controls.Add(checkUartOnly);
            Controls.Add(listDevices);
            Controls.Add(labelHelp);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            MinimizeBox = false;
            MinimumSize = new Size(420, 300);
            Name = "BleDeviceForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Bluetooth LE device";
            FormClosed += BleDeviceForm_FormClosed;
            Load += BleDeviceForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelHelp;
        private ListView listDevices;
        private ColumnHeader columnName;
        private ColumnHeader columnAddress;
        private ColumnHeader columnSignal;
        private ColumnHeader columnUart;
        private CheckBox checkUartOnly;
        private Label labelStatus;
        private Button buttonSelect;
        private Button buttonCancel;
        private System.Windows.Forms.Timer refreshTimer;
    }
}
