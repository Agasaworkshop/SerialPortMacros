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
            labelFrameFields = new Label();
            textFrameFields = new TextBox();
            labelSamples = new Label();
            numSamples = new NumericUpDown();
            labelSamplesHint = new Label();
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
            ((System.ComponentModel.ISupportInitialize)numSamples).BeginInit();
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
            labelFields.Size = new Size(76, 15);
            labelFields.TabIndex = 1;
            labelFields.Text = "Sample fields";
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
            // labelFrameFields
            //
            labelFrameFields.AutoSize = true;
            labelFrameFields.Location = new Point(12, 212);
            labelFrameFields.Name = "labelFrameFields";
            labelFrameFields.Size = new Size(72, 15);
            labelFrameFields.TabIndex = 3;
            labelFrameFields.Text = "Frame fields";
            //
            // textFrameFields
            //
            textFrameFields.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textFrameFields.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            textFrameFields.Location = new Point(122, 209);
            textFrameFields.Name = "textFrameFields";
            textFrameFields.PlaceholderText = "optional: sent once per frame, before the samples (e.g. seq:u16)";
            textFrameFields.Size = new Size(506, 23);
            textFrameFields.TabIndex = 4;
            textFrameFields.TextChanged += Inputs_Changed;
            //
            // labelSamples
            //
            labelSamples.AutoSize = true;
            labelSamples.Location = new Point(12, 244);
            labelSamples.Name = "labelSamples";
            labelSamples.Size = new Size(82, 15);
            labelSamples.TabIndex = 5;
            labelSamples.Text = "Samples/frame";
            //
            // numSamples
            //
            numSamples.Location = new Point(122, 241);
            numSamples.Maximum = new decimal(new int[] { 4096, 0, 0, 0 });
            numSamples.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numSamples.Name = "numSamples";
            numSamples.Size = new Size(100, 23);
            numSamples.TabIndex = 6;
            numSamples.Value = new decimal(new int[] { 1, 0, 0, 0 });
            numSamples.ValueChanged += Inputs_Changed;
            //
            // labelSamplesHint
            //
            labelSamplesHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelSamplesHint.ForeColor = Color.DimGray;
            labelSamplesHint.Location = new Point(230, 244);
            labelSamplesHint.Name = "labelSamplesHint";
            labelSamplesHint.Size = new Size(398, 15);
            labelSamplesHint.TabIndex = 7;
            labelSamplesHint.Text = "messages in each frame (1 = one message per frame)";
            //
            // labelHeader
            //
            labelHeader.AutoSize = true;
            labelHeader.Location = new Point(12, 276);
            labelHeader.Name = "labelHeader";
            labelHeader.Size = new Size(72, 15);
            labelHeader.TabIndex = 8;
            labelHeader.Text = "Sync header";
            //
            // textHeader
            //
            textHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textHeader.Location = new Point(122, 273);
            textHeader.Name = "textHeader";
            textHeader.PlaceholderText = "e.g. AA 55 (empty = none)";
            textHeader.Size = new Size(506, 23);
            textHeader.TabIndex = 9;
            textHeader.TextChanged += Inputs_Changed;
            //
            // labelOrder
            //
            labelOrder.AutoSize = true;
            labelOrder.Location = new Point(12, 308);
            labelOrder.Name = "labelOrder";
            labelOrder.Size = new Size(63, 15);
            labelOrder.TabIndex = 10;
            labelOrder.Text = "Byte order";
            //
            // comboEndian
            //
            comboEndian.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            comboEndian.DropDownStyle = ComboBoxStyle.DropDownList;
            comboEndian.FormattingEnabled = true;
            comboEndian.Items.AddRange(new object[] { "Little-endian / LSB first (ARM, AVR, ESP32: struct in memory)", "Big-endian / MSB first (network order)" });
            comboEndian.Location = new Point(122, 305);
            comboEndian.Name = "comboEndian";
            comboEndian.Size = new Size(506, 23);
            comboEndian.TabIndex = 11;
            comboEndian.SelectedIndexChanged += Inputs_Changed;
            //
            // labelChecksum
            //
            labelChecksum.AutoSize = true;
            labelChecksum.Location = new Point(12, 340);
            labelChecksum.Name = "labelChecksum";
            labelChecksum.Size = new Size(61, 15);
            labelChecksum.TabIndex = 12;
            labelChecksum.Text = "Checksum";
            //
            // comboChecksum
            //
            comboChecksum.DropDownStyle = ComboBoxStyle.DropDownList;
            comboChecksum.FormattingEnabled = true;
            comboChecksum.Location = new Point(122, 337);
            comboChecksum.Name = "comboChecksum";
            comboChecksum.Size = new Size(200, 23);
            comboChecksum.TabIndex = 13;
            comboChecksum.SelectedIndexChanged += Inputs_Changed;
            //
            // labelInfo
            //
            labelInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelInfo.Location = new Point(122, 369);
            labelInfo.Name = "labelInfo";
            labelInfo.Size = new Size(506, 50);
            labelInfo.TabIndex = 14;
            //
            // labelTest
            //
            labelTest.AutoSize = true;
            labelTest.Location = new Point(12, 430);
            labelTest.Name = "labelTest";
            labelTest.Size = new Size(58, 15);
            labelTest.TabIndex = 15;
            labelTest.Text = "Test (hex)";
            //
            // textTestInput
            //
            textTestInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textTestInput.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            textTestInput.Location = new Point(122, 427);
            textTestInput.Name = "textTestInput";
            textTestInput.PlaceholderText = "test bytes in hex, e.g. AA 55 03 10 27 ...";
            textTestInput.Size = new Size(506, 22);
            textTestInput.TabIndex = 16;
            textTestInput.TextChanged += Inputs_Changed;
            //
            // labelResult
            //
            labelResult.AutoSize = true;
            labelResult.Location = new Point(12, 462);
            labelResult.Name = "labelResult";
            labelResult.Size = new Size(39, 15);
            labelResult.TabIndex = 17;
            labelResult.Text = "Result";
            //
            // textTestOutput
            //
            textTestOutput.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textTestOutput.Font = new Font("Consolas", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            textTestOutput.Location = new Point(122, 459);
            textTestOutput.Multiline = true;
            textTestOutput.Name = "textTestOutput";
            textTestOutput.ReadOnly = true;
            textTestOutput.ScrollBars = ScrollBars.Vertical;
            textTestOutput.Size = new Size(506, 82);
            textTestOutput.TabIndex = 18;
            //
            // buttonOk
            //
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonOk.Location = new Point(442, 555);
            buttonOk.Name = "buttonOk";
            buttonOk.Size = new Size(90, 30);
            buttonOk.TabIndex = 19;
            buttonOk.Text = "OK";
            buttonOk.UseVisualStyleBackColor = true;
            buttonOk.Click += buttonOk_Click;
            //
            // buttonCancel
            //
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(538, 555);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(90, 30);
            buttonCancel.TabIndex = 20;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            //
            // BinaryFormatForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(640, 598);
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
            Controls.Add(labelSamplesHint);
            Controls.Add(numSamples);
            Controls.Add(labelSamples);
            Controls.Add(textFrameFields);
            Controls.Add(labelFrameFields);
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
            ((System.ComponentModel.ISupportInitialize)numSamples).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label labelHelp;
        private Label labelFields;
        private TextBox textFields;
        private Label labelFrameFields;
        private TextBox textFrameFields;
        private Label labelSamples;
        private NumericUpDown numSamples;
        private Label labelSamplesHint;
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
