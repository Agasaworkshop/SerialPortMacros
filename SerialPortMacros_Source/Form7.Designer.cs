namespace SerialPortMacros
{
    partial class Form7
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
            moduleBox = new ComboBox();
            add_button = new Button();
            Save_button = new Button();
            panel1 = new Panel();
            SuspendLayout();
            // 
            // moduleBox
            // 
            moduleBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            moduleBox.FormattingEnabled = true;
            moduleBox.Location = new Point(15, 410);
            moduleBox.Name = "moduleBox";
            moduleBox.Size = new Size(151, 28);
            moduleBox.TabIndex = 0;
            // 
            // add_button
            // 
            add_button.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            add_button.Location = new Point(172, 410);
            add_button.Name = "add_button";
            add_button.Size = new Size(53, 29);
            add_button.TabIndex = 2;
            add_button.Text = "Add";
            add_button.UseVisualStyleBackColor = true;
            add_button.Click += add_button_Click;
            // 
            // Save_button
            // 
            Save_button.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Save_button.Location = new Point(694, 410);
            Save_button.Name = "Save_button";
            Save_button.Size = new Size(94, 29);
            Save_button.TabIndex = 3;
            Save_button.Text = "Save";
            Save_button.UseVisualStyleBackColor = true;
            Save_button.Click += Save_button_Click;
            // 
            // panel1
            // 
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(802, 404);
            panel1.TabIndex = 4;
            // 
            // Form7
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(panel1);
            Controls.Add(Save_button);
            Controls.Add(add_button);
            Controls.Add(moduleBox);
            Name = "Form7";
            Text = "Form7";
            Load += Form7_Load;
            ResumeLayout(false);
        }

        #endregion

        private ComboBox moduleBox;
        private Button add_button;
        private Button Save_button;
        private Panel panel1;
    }
}