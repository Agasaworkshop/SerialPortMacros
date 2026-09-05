namespace SerialPortMacros
{
    partial class Log
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Log));
            formsPlot1 = new ScottPlot.WinForms.FormsPlot();
            comboBox1 = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            textBox1 = new TextBox();
            dataGridView1 = new DataGridView();
            label4 = new Label();
            numericUpDown1 = new NumericUpDown();
            button1 = new Button();
            toolTip1 = new ToolTip(components);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown1).BeginInit();
            SuspendLayout();
            // 
            // formsPlot1
            // 
            formsPlot1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            formsPlot1.DisplayScale = 1.25F;
            formsPlot1.Location = new Point(12, 12);
            formsPlot1.Name = "formsPlot1";
            formsPlot1.Size = new Size(706, 426);
            formsPlot1.TabIndex = 0;
            // 
            // comboBox1
            // 
            comboBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(724, 315);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(151, 28);
            comboBox1.TabIndex = 1;
            toolTip1.SetToolTip(comboBox1, "Select a vector that will act as x axis, auto uses an evenly spaced vector of values");
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label1.AutoSize = true;
            label1.Location = new Point(724, 292);
            label1.Name = "label1";
            label1.Size = new Size(45, 20);
            label1.TabIndex = 2;
            label1.Text = "x axis";
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label2.AutoSize = true;
            label2.Location = new Point(724, 68);
            label2.Name = "label2";
            label2.Size = new Size(45, 20);
            label2.TabIndex = 3;
            label2.Text = "y axis";
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label3.AutoSize = true;
            label3.Location = new Point(724, 351);
            label3.Name = "label3";
            label3.Size = new Size(106, 20);
            label3.TabIndex = 4;
            label3.Text = "Sampling time";
            // 
            // textBox1
            // 
            textBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            textBox1.Location = new Point(724, 374);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(125, 27);
            textBox1.TabIndex = 5;
            textBox1.Text = "1";
            toolTip1.SetToolTip(textBox1, "Sampling time of the data(only Auto)");
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeColumns = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.GridColor = SystemColors.ButtonHighlight;
            dataGridView1.Location = new Point(724, 91);
            dataGridView1.MultiSelect = false;
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.RowTemplate.Height = 29;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new Size(240, 188);
            dataGridView1.TabIndex = 6;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
            dataGridView1.CurrentCellDirtyStateChanged += dataGridView1_CurrentCellDirtyStateChanged;
            // 
            // label4
            // 
            label4.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label4.AutoSize = true;
            label4.Location = new Point(724, 6);
            label4.Name = "label4";
            label4.Size = new Size(154, 20);
            label4.TabIndex = 7;
            label4.Text = "minimum occurrences";
            // 
            // numericUpDown1
            // 
            numericUpDown1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            numericUpDown1.Location = new Point(728, 29);
            numericUpDown1.Name = "numericUpDown1";
            numericUpDown1.Size = new Size(102, 27);
            numericUpDown1.TabIndex = 8;
            toolTip1.SetToolTip(numericUpDown1, "Minumum number of occurrences of a pattern to determinate the data");
            numericUpDown1.Value = new decimal(new int[] { 5, 0, 0, 0 });
            numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
            numericUpDown1.Leave += numericUpDown1_Leave;
            // 
            // button1
            // 
            button1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button1.Image = (Image)resources.GetObject("button1.Image");
            button1.Location = new Point(937, 409);
            button1.Name = "button1";
            button1.Size = new Size(27, 29);
            button1.TabIndex = 9;
            toolTip1.SetToolTip(button1, "Refresh plot");
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // Log
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(976, 450);
            Controls.Add(button1);
            Controls.Add(numericUpDown1);
            Controls.Add(label4);
            Controls.Add(dataGridView1);
            Controls.Add(textBox1);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBox1);
            Controls.Add(formsPlot1);
            Name = "Log";
            Text = "Log";
            Load += Form5_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private ComboBox comboBox1;
        private Label label1;
        private Label label2;
        private Label label3;
        private TextBox textBox1;
        private DataGridView dataGridView1;
        private Label label4;
        private NumericUpDown numericUpDown1;
        private Button button1;
        private ToolTip toolTip1;
    }
}