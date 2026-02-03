using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialPortMacros
{
    public partial class Form3 : Form
    {
        public Form1 form1;
        public Form3(Form1 form)
        {
            form1 = form;
            InitializeComponent();
            checkedListBox1.DataSource = form1.scripts;
            checkedListBox1.DisplayMember = "name";
        }
        private void Form3_Load(object sender, EventArgs e)
        {
            string[] effectlist = { "Keypress", "Write back" };
            comboBox1.Items.AddRange(effectlist);
            findactivescripts();
            displayscript();
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            displayscript();
        }
        private void CheckedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            string[] newfile;
            Script c_script = new Script();
            int ind = comboBox1.SelectedIndex;
            if (ind == -1)
                return;
            c_script = (Script)checkedListBox1.SelectedItem;
            form1.wait(100);
            if (checkedListBox1.GetItemCheckState(ind) == CheckState.Checked)
            {
                c_script.active = false;
            }
            else
                c_script.active = true;
            newfile = File.ReadAllLines(c_script.path);
            int len = newfile.Length - 1;
            newfile[len] = c_script.active.ToString();
            newfile = newfile.Where(str => !string.IsNullOrWhiteSpace(str)).ToArray();
            File.WriteAllLines(c_script.path, newfile);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                Applychanges();
            }
            catch { }
        }
        private void Applychanges()
        {
            string[] newfile = new string[12];
            Script c_script = (Script)checkedListBox1.SelectedItem;
            int index = checkedListBox1.SelectedIndex;
            bool active = false;
            if (checkedListBox1.GetItemCheckState(index) == CheckState.Checked)
            {
                active = true;
            }
            newfile[0] = textBox2.Text;
            c_script.key = textBox2.Text;
            newfile[2] = checkBox1.Checked.ToString();
            c_script.p1 = checkBox1.Checked;
            newfile[3] = checkBox2.Checked.ToString();
            c_script.p2 = checkBox2.Checked;
            newfile[4] = checkBox3.Checked.ToString();
            c_script.p3 = checkBox3.Checked;
            newfile[5] = checkBox4.Checked.ToString();
            c_script.p4 = checkBox4.Checked;
            if (comboBox1.Text == "Keypress")
            {
                newfile[1] = "1";
                c_script.trigger = 1;
                newfile[6] = textBox4.Text;
                c_script.keypress = textBox4.Text;
                newfile[7] = active.ToString();
                c_script.active = active;
            }
            else if (comboBox1.Text == "Write back")
            {
                newfile[1] = "2";
                c_script.trigger = 2;
                newfile[6] = textBox3.Text;
                c_script.reply = textBox3.Text;
                newfile[7] = checkBox8.Checked.ToString();
                c_script.p1_w = checkBox8.Checked;
                newfile[8] = checkBox7.Checked.ToString();
                c_script.p2_w = checkBox7.Checked;
                newfile[9] = checkBox6.Checked.ToString();
                c_script.p3_w = checkBox6.Checked;
                newfile[10] = checkBox5.Checked.ToString();
                c_script.p4_w = checkBox5.Checked;
                newfile[11] = active.ToString();
                c_script.active = active;
            }
            if (c_script != null)
            {
                newfile = newfile.Where(str => !string.IsNullOrWhiteSpace(str)).ToArray();
                File.WriteAllLines(c_script.path, newfile);
            }
        }
        void findactivescripts()
        {
            int num = (form1.scripts.Length);
            for (int i = 0; i < num; i++)
            {
                if (form1.scripts[i].active)
                {
                    checkedListBox1.SetItemCheckState(i, CheckState.Checked);
                }
                else
                    checkedListBox1.SetItemCheckState(i, CheckState.Unchecked);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (textBox5.Text != "")
            {
                FileStream FOUT;
                string filePath = $"./scripts/{textBox5.Text}";
                try
                {
                    using (FOUT = File.Create(filePath)) ;
                }
                catch (IOException ex)
                {
                    Console.WriteLine("An error occurred while creating the file: " + ex.Message);
                }
                string[] newfile = new string[12];
                newfile[0] = "test";
                newfile[1] = "1";
                newfile[2] = "false";
                newfile[3] = "false";
                newfile[4] = "false";
                newfile[5] = "false";
                newfile[6] = "%100%!LMB";
                newfile[7] = "false";
                newfile = newfile.Where(str => !string.IsNullOrWhiteSpace(str)).ToArray();
                File.WriteAllLines(filePath, newfile);
                textBox5.Text = "";
            }
            form1.ScanScripts();
            checkedListBox1.DataSource = form1.scripts;
            checkedListBox1.DisplayMember = "name";
        }
        public void displayscript()
        {
            textBox4.Text = "";
            comboBox1.Text = "";
            textBox3.Text = "";
            checkBox8.Checked = false;
            checkBox7.Checked = false;
            checkBox6.Checked = false;
            checkBox5.Checked = false;

            Script c_script = (Script)checkedListBox1.SelectedItem;
            if (c_script != null)
            {
                textBox1.Text = c_script.name;
                textBox2.Text = c_script.key;
                checkBox1.Checked = c_script.p1;
                checkBox2.Checked = c_script.p2;
                checkBox3.Checked = c_script.p3;
                checkBox4.Checked = c_script.p4;
                if (c_script.trigger == 1)
                {
                    comboBox1.Text = "Keypress";
                    textBox4.Text = c_script.keypress;
                }

                else if (c_script.trigger == 2)
                {
                    comboBox1.Text = "Write back";
                    textBox3.Text = c_script.reply;
                    checkBox8.Checked = c_script.p1_w;
                    checkBox7.Checked = c_script.p2_w;
                    checkBox6.Checked = c_script.p3_w;
                    checkBox5.Checked = c_script.p4_w;
                }
            }
        }
    }
}

