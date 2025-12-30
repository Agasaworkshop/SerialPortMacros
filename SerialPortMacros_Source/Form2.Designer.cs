namespace SerialPortMacros
{
    partial class Form2
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
            button1 = new Button();
            button2 = new Button();
            label1 = new Label();
            databitsbox = new TextBox();
            label2 = new Label();
            label3 = new Label();
            Paritybox = new ComboBox();
            stopbitsbox = new ComboBox();
            label4 = new Label();
            textBox1 = new TextBox();
            checkBox1 = new CheckBox();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(12, 198);
            button1.Name = "button1";
            button1.Size = new Size(94, 29);
            button1.TabIndex = 0;
            button1.Text = "Cancel";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(120, 198);
            button2.Name = "button2";
            button2.Size = new Size(94, 29);
            button2.TabIndex = 1;
            button2.Text = "Apply";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(3, 9);
            label1.Name = "label1";
            label1.Size = new Size(45, 20);
            label1.TabIndex = 2;
            label1.Text = "Parity";
            // 
            // databitsbox
            // 
            databitsbox.Location = new Point(89, 56);
            databitsbox.Name = "databitsbox";
            databitsbox.Size = new Size(125, 27);
            databitsbox.TabIndex = 6;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(3, 59);
            label2.Name = "label2";
            label2.Size = new Size(69, 20);
            label2.TabIndex = 5;
            label2.Text = "Data bits";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(3, 117);
            label3.Name = "label3";
            label3.Size = new Size(68, 20);
            label3.TabIndex = 7;
            label3.Text = "Stop bits";
            // 
            // Paritybox
            // 
            Paritybox.FormattingEnabled = true;
            Paritybox.Location = new Point(89, 6);
            Paritybox.Name = "Paritybox";
            Paritybox.Size = new Size(125, 28);
            Paritybox.TabIndex = 11;
            // 
            // stopbitsbox
            // 
            stopbitsbox.FormattingEnabled = true;
            stopbitsbox.Location = new Point(89, 117);
            stopbitsbox.Name = "stopbitsbox";
            stopbitsbox.Size = new Size(125, 28);
            stopbitsbox.TabIndex = 12;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(3, 165);
            label4.Name = "label4";
            label4.Size = new Size(103, 20);
            label4.TabIndex = 13;
            label4.Text = "Custom Buffer";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(112, 162);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(82, 27);
            textBox1.TabIndex = 14;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(200, 165);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(18, 17);
            checkBox1.TabIndex = 15;
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // Form2
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(226, 239);
            Controls.Add(checkBox1);
            Controls.Add(textBox1);
            Controls.Add(label4);
            Controls.Add(stopbitsbox);
            Controls.Add(Paritybox);
            Controls.Add(label3);
            Controls.Add(databitsbox);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(button2);
            Controls.Add(button1);
            MaximizeBox = false;
            MaximumSize = new Size(244, 286);
            MinimizeBox = false;
            MinimumSize = new Size(244, 256);
            Name = "Form2";
            Text = "Form2";
            Load += Form2_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private Button button2;
        private Label label1;
        public TextBox databitsbox;
        private Label label2;
        private Label label3;
        public ComboBox Paritybox;
        public ComboBox stopbitsbox;
        private Label label4;
        private TextBox textBox1;
        private CheckBox checkBox1;
    }
}