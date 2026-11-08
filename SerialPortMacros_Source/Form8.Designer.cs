namespace SerialPortMacros
{
    partial class Form8
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
            comboBox1 = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            textBox1 = new TextBox();
            button1 = new Button();
            label3 = new Label();
            textBox2 = new TextBox();
            checkBox1 = new CheckBox();
            checkBox2 = new CheckBox();
            checkBox3 = new CheckBox();
            checkBox4 = new CheckBox();
            textBox3 = new TextBox();
            textBox4 = new TextBox();
            label4 = new Label();
            label5 = new Label();
            textBox5 = new TextBox();
            label7 = new Label();
            button2 = new Button();
            button3 = new Button();
            lineEndingCombo = new ComboBox();
            DeleteButton = new Button();
            SuspendLayout();
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(124, 12);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(246, 28);
            comboBox1.TabIndex = 0;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 15);
            label1.Name = "label1";
            label1.Size = new Size(95, 20);
            label1.TabIndex = 1;
            label1.Text = "Module Type";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 49);
            label2.Name = "label2";
            label2.Size = new Size(104, 20);
            label2.TabIndex = 2;
            label2.Text = "Variable name";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(124, 83);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(146, 27);
            textBox1.TabIndex = 3;
            // 
            // button1
            // 
            button1.Location = new Point(276, 83);
            button1.Name = "button1";
            button1.Size = new Size(94, 29);
            button1.TabIndex = 4;
            button1.Text = "Browse";
            button1.UseVisualStyleBackColor = true;
            button1.Click += BrowseImageButton_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 87);
            label3.Name = "label3";
            label3.Size = new Size(104, 20);
            label3.TabIndex = 5;
            label3.Text = "Second image";
            // 
            // textBox2
            // 
            textBox2.Location = new Point(124, 46);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(246, 27);
            textBox2.TabIndex = 3;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(46, 260);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(65, 24);
            checkBox1.TabIndex = 0;
            checkBox1.Text = "Port1";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // checkBox2
            // 
            checkBox2.AutoSize = true;
            checkBox2.Location = new Point(117, 260);
            checkBox2.Name = "checkBox2";
            checkBox2.Size = new Size(65, 24);
            checkBox2.TabIndex = 7;
            checkBox2.Text = "Port2";
            checkBox2.UseVisualStyleBackColor = true;
            // 
            // checkBox3
            // 
            checkBox3.AutoSize = true;
            checkBox3.Location = new Point(188, 260);
            checkBox3.Name = "checkBox3";
            checkBox3.Size = new Size(65, 24);
            checkBox3.TabIndex = 0;
            checkBox3.Text = "Port3";
            checkBox3.UseVisualStyleBackColor = true;
            // 
            // checkBox4
            // 
            checkBox4.AutoSize = true;
            checkBox4.Location = new Point(259, 260);
            checkBox4.Name = "checkBox4";
            checkBox4.Size = new Size(65, 24);
            checkBox4.TabIndex = 0;
            checkBox4.Text = "Port4";
            checkBox4.UseVisualStyleBackColor = true;
            // 
            // textBox3
            // 
            textBox3.Location = new Point(12, 217);
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(358, 27);
            textBox3.TabIndex = 0;
            // 
            // textBox4
            // 
            textBox4.Location = new Point(12, 159);
            textBox4.Name = "textBox4";
            textBox4.Size = new Size(358, 27);
            textBox4.TabIndex = 0;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(12, 136);
            label4.Name = "label4";
            label4.Size = new Size(79, 20);
            label4.TabIndex = 8;
            label4.Text = "Message 1";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(9, 194);
            label5.Name = "label5";
            label5.Size = new Size(79, 20);
            label5.TabIndex = 8;
            label5.Text = "Message 2";
            // 
            // textBox5
            // 
            textBox5.Location = new Point(12, 322);
            textBox5.Name = "textBox5";
            textBox5.Size = new Size(358, 27);
            textBox5.TabIndex = 0;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(9, 299);
            label7.Name = "label7";
            label7.Size = new Size(85, 20);
            label7.TabIndex = 8;
            label7.Text = "If condition";
            // 
            // button2
            // 
            button2.Location = new Point(276, 362);
            button2.Name = "button2";
            button2.Size = new Size(94, 29);
            button2.TabIndex = 10;
            button2.Text = "save";
            button2.UseVisualStyleBackColor = true;
            button2.Click += ApplyButton_Click;
            // 
            // button3
            // 
            button3.Location = new Point(9, 362);
            button3.Name = "button3";
            button3.Size = new Size(94, 29);
            button3.TabIndex = 10;
            button3.Text = "cancel";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // lineEndingCombo
            // 
            lineEndingCombo.FormattingEnabled = true;
            lineEndingCombo.Location = new Point(219, 133);
            lineEndingCombo.Name = "lineEndingCombo";
            lineEndingCombo.Size = new Size(151, 28);
            lineEndingCombo.TabIndex = 11;
            // 
            // DeleteButton
            // 
            DeleteButton.Location = new Point(176, 362);
            DeleteButton.Name = "DeleteButton";
            DeleteButton.Size = new Size(94, 29);
            DeleteButton.TabIndex = 12;
            DeleteButton.Text = "Delete";
            DeleteButton.UseVisualStyleBackColor = true;
            DeleteButton.Click += DeleteButton_Click;
            // 
            // Form8
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(382, 403);
            Controls.Add(DeleteButton);
            Controls.Add(lineEndingCombo);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(label7);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(textBox4);
            Controls.Add(textBox5);
            Controls.Add(textBox3);
            Controls.Add(checkBox4);
            Controls.Add(checkBox3);
            Controls.Add(checkBox2);
            Controls.Add(checkBox1);
            Controls.Add(label3);
            Controls.Add(button1);
            Controls.Add(textBox2);
            Controls.Add(textBox1);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBox1);
            MaximumSize = new Size(400, 450);
            MinimumSize = new Size(400, 450);
            Name = "Form8";
            Text = "Form8";
            Load += Form8_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        public ComboBox comboBox1;
        public Label label1;
        public Label label2;
        public TextBox textBox1;
        public Button button1;
        public Label label3;
        public TextBox textBox2;
        public CheckBox checkBox1;
        public CheckBox checkBox2;
        public CheckBox checkBox3;
        public CheckBox checkBox4;
        public TextBox textBox3;
        public TextBox textBox4;
        public Label label4;
        public Label label5;
        public TextBox textBox5;
        public Label label7;
        public Button button2;
        public Button button3;
        public ComboBox lineEndingCombo;
        private Button DeleteButton;
    }
}