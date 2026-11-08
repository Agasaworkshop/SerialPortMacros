namespace SerialPortMacros
{
    partial class BinaryFormatForm
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
            labelHelp = new Label();
            labelFields = new Label();
            textFields = new TextBox();
            labelHeader = new Label();
            textHeader = new TextBox();
            labelOrder = new Label();
            comboEndian = new ComboBox();
            labelChecksum = new Label();
            comboChecksum = new ComboBox();
            labelInfo = new Label();
            labelTest = new Label();
            textTestInput = new TextBox();
            labelResult = new Label();
            textTestOutput = new TextBox();
            buttonOk = new Button();
            buttonCancel = new Button();
            SuspendLayout();
            //
            // labelHelp
            //
            labelHelp.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelHelp.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            labelHelp.ForeColor = Color.DimGray;
            labelHelp.Location = new Point(12, 9);
            labelHelp.Name = "labelHelp";
            labelHelp.Size = new Size(616, 75);
            labelHelp.TabIndex = 0;
            labelHelp.Text = "Fields in transmission order: [name:]type[[n]][*scale][+offset]\r\n  u5 = 5-bit unsigned int   i13 = 13-bit signed int   b = 1 bit\r\n  f32 / f16 / f64 = float     x3 = 3 bits to skip\r\n  acc:i16[3] = 3 fields   temp:i16*0.01   p:u10*0.1-40\r\nE.g.: a:u5 b:u13 c:f32 d:i6";
            //
            // labelFields
            //
            labelFields.AutoSize = true;
            labelFields.Location = new Point(12, 92);
            labelFields.Name = "labelFields";
            labelFields.Size = new Size(37, 15);
            labelFields.TabIndex = 1;
            labelFields.Text = "Fields";
            //
            // textFields
            //
            textFields.AcceptsReturn = true;
            textFields.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textFields.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            textFields.Location = new Point(122, 89);
            textFields.Multiline = true;
            textFields.Name = "textFields";
            textFields.ScrollBars = ScrollBars.Vertical;
            textFields.Size = new Size(506, 110);
            textFields.TabIndex = 2;
            textFields.TextChanged += Inputs_Changed;
            //
            // labelHeader
            //
            labelHeader.AutoSize = true;
            labelHeader.Location = new Point(12, 212);
            labelHeader.Name = "labelHeader";
            labelHeader.Size = new Size(72, 15);
            labelHeader.TabIndex = 3;
            labelHeader.Text = "Sync header";
            //
            // textHeader
            //
            textHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textHeader.Location = new Point(122, 209);
            textHeader.Name = "textHeader";
            textHeader.PlaceholderText = "e.g. AA 55 (empty = none)";
            textHeader.Size = new Size(506, 23);
            textHeader.TabIndex = 4;
            textHeader.TextChanged += Inputs_Changed;
            //
            // labelOrder
            //
            labelOrder.AutoSize = true;
            labelOrder.Location = new Point(12, 244);
            labelOrder.Name = "labelOrder";
            labelOrder.Size = new Size(63, 15);
            labelOrder.TabIndex = 5;
            labelOrder.Text = "Byte order";
            //
            // comboEndian
            //
            comboEndian.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            comboEndian.DropDownStyle = ComboBoxStyle.DropDownList;
            comboEndian.FormattingEnabled = true;
            comboEndian.Items.AddRange(new object[] { "Little-endian / LSB first (ARM, AVR, ESP32: struct in memory)", "Big-endian / MSB first (network order)" });
            comboEndian.Location = new Point(122, 241);
            comboEndian.Name = "comboEndian";
            comboEndian.Size = new Size(506, 23);
            comboEndian.TabIndex = 6;
            comboEndian.SelectedIndexChanged += Inputs_Changed;
            //
            // labelChecksum
            //
            labelChecksum.AutoSize = true;
            labelChecksum.Location = new Point(12, 276);
            labelChecksum.Name = "labelChecksum";
            labelChecksum.Size = new Size(61, 15);
            labelChecksum.TabIndex = 7;
            labelChecksum.Text = "Checksum";
            //
            // comboChecksum
            //
            comboChecksum.DropDownStyle = ComboBoxStyle.DropDownList;
            comboChecksum.FormattingEnabled = true;
            comboChecksum.Location = new Point(122, 273);
            comboChecksum.Name = "comboChecksum";
            comboChecksum.Size = new Size(200, 23);
            comboChecksum.TabIndex = 8;
            comboChecksum.SelectedIndexChanged += Inputs_Changed;
            //
            // labelInfo
            //
            labelInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelInfo.Location = new Point(122, 305);
            labelInfo.Name = "labelInfo";
            labelInfo.Size = new Size(506, 38);
            labelInfo.TabIndex = 9;
            //
            // labelTest
            //
            labelTest.AutoSize = true;
            labelTest.Location = new Point(12, 352);
            labelTest.Name = "labelTest";
            labelTest.Size = new Size(58, 15);
            labelTest.TabIndex = 10;
            labelTest.Text = "Test (hex)";
            //
            // textTestInput
            //
            textTestInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textTestInput.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            textTestInput.Location = new Point(122, 349);
            textTestInput.Name = "textTestInput";
            textTestInput.PlaceholderText = "test bytes in hex, e.g. AA 55 03 10 27 ...";
            textTestInput.Size = new Size(506, 22);
            textTestInput.TabIndex = 11;
            textTestInput.TextChanged += Inputs_Changed;
            //
            // labelResult
            //
            labelResult.AutoSize = true;
            labelResult.Location = new Point(12, 384);
            labelResult.Name = "labelResult";
            labelResult.Size = new Size(39, 15);
            labelResult.TabIndex = 12;
            labelResult.Text = "Result";
            //
            // textTestOutput
            //
            textTestOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textTestOutput.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            textTestOutput.Location = new Point(122, 381);
            textTestOutput.Multiline = true;
            textTestOutput.Name = "textTestOutput";
            textTestOutput.ReadOnly = true;
            textTestOutput.ScrollBars = ScrollBars.Vertical;
            textTestOutput.Size = new Size(506, 82);
            textTestOutput.TabIndex = 13;
            //
            // buttonOk
            //
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonOk.Location = new Point(442, 477);
            buttonOk.Name = "buttonOk";
            buttonOk.Size = new Size(90, 30);
            buttonOk.TabIndex = 14;
            buttonOk.Text = "OK";
            buttonOk.UseVisualStyleBackColor = true;
            buttonOk.Click += buttonOk_Click;
            //
            // buttonCancel
            //
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(538, 477);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(90, 30);
            buttonCancel.TabIndex = 15;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            //
            // BinaryFormatForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(640, 520);
            Controls.Add(buttonCancel);
            Controls.Add(buttonOk);
            Controls.Add(textTestOutput);
            Controls.Add(labelResult);
            Controls.Add(textTestInput);
            Controls.Add(labelTest);
            Controls.Add(labelInfo);
            Controls.Add(comboChecksum);
            Controls.Add(labelChecksum);
            Controls.Add(comboEndian);
            Controls.Add(labelOrder);
            Controls.Add(textHeader);
            Controls.Add(labelHeader);
            Controls.Add(textFields);
            Controls.Add(labelFields);
            Controls.Add(labelHelp);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "BinaryFormatForm";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Binary frame format";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelHelp;
        private Label labelFields;
        private TextBox textFields;
        private Label labelHeader;
        private TextBox textHeader;
        private Label labelOrder;
        private ComboBox comboEndian;
        private Label labelChecksum;
        private ComboBox comboChecksum;
        private Label labelInfo;
        private Label labelTest;
        private TextBox textTestInput;
        private Label labelResult;
        private TextBox textTestOutput;
        private Button buttonOk;
        private Button buttonCancel;
    }
}
