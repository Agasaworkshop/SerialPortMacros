using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialPortMacros
{
    public partial class Form3 : Form
    {
        public Form1 form1;
        private bool _isLoading = false;

        private int last_index = 0;

        public Form3(Form1 form)
        {
            form1 = form;
            InitializeComponent();

            // 1. Popola i ComboBox SUBITO nel costruttore, prima di associare il DataSource
            comboBox2.Items.Clear();
            comboBox2.Items.Add("None");
            comboBox2.Items.Add("NL");
            comboBox2.Items.Add("CR");
            comboBox2.Items.Add("Both");

            string[] effectlist = { "Keypress", "Write back" };
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(effectlist);
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;

            // 2. Ora è sicuro associare il DataSource (che scatenerà gli eventi trovando i comboBox già pronti)
            checkedListBox1.DataSource = form1.scripts;
            checkedListBox1.DisplayMember = "name";
        }

        private void Form3_Load(object sender, EventArgs e)
        {
            findactivescripts();
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            displayscript();
            _isLoading = false; // Fine caricamento iniziale
            c_script = (Script)checkedListBox1.Items[0];
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

            bool active = checkedListBox1.GetItemCheckState(last_index) == CheckState.Checked;
            Applychanges(active);
            last_index = checkedListBox1.SelectedIndex;
            c_script = (Script)checkedListBox1.SelectedItem; //sets  current selected 
            displayscript();
        }

        private void CheckedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.Index < 0 || e.Index >= checkedListBox1.Items.Count)
                return;

            Script c_script = checkedListBox1.Items[e.Index] as Script;
            if (c_script == null)
                return;

            c_script.active = (e.NewValue == CheckState.Checked);

            var lines = File.ReadAllLines(c_script.path);
            if (lines.Length > 0)
            {
                if (c_script.trigger == 1 && lines.Length > 7)
                {
                    lines[7] = c_script.active.ToString();
                }
                else if (c_script.trigger == 2 && lines.Length > 11)
                {
                    lines[11] = c_script.active.ToString();
                }
                File.WriteAllLines(c_script.path, lines.Where(l => !string.IsNullOrWhiteSpace(l)));
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                int index = checkedListBox1.SelectedIndex;
                bool active = checkedListBox1.GetItemCheckState(index) == CheckState.Checked;
                Applychanges(active);
                this.Close();
            }
            catch (Exception ex)
            {
                this.Close();
            }
        }

        Script c_script;
        private void Applychanges(bool active)
        {

            if (c_script == null) return;
            c_script.key = textBox2.Text;
            c_script.p1 = checkBox1.Checked;
            c_script.p2 = checkBox2.Checked;
            c_script.p3 = checkBox3.Checked;
            c_script.p4 = checkBox4.Checked;
            c_script.active = active;

            List<string> lines = new List<string>
            {
                c_script.key,
                c_script.trigger.ToString(),
                c_script.p1.ToString(),
                c_script.p2.ToString(),
                c_script.p3.ToString(),
                c_script.p4.ToString()
            };

            if (comboBox1.Text == "Keypress")
            {
                c_script.trigger = 1;
                lines[1] = "1";
                c_script.keypress = textBox4.Text;
                lines.Add(c_script.keypress);
                lines.Add(c_script.active.ToString());
            }
            else if (comboBox1.Text == "Write back")
            {
                c_script.trigger = 2;
                lines[1] = "2";
                c_script.reply = textBox3.Text;
                lines.Add(c_script.reply);

                c_script.p1_w = checkBox8.Checked;
                lines.Add(c_script.p1_w.ToString());
                c_script.p2_w = checkBox7.Checked;
                lines.Add(c_script.p2_w.ToString());
                c_script.p3_w = checkBox6.Checked;
                lines.Add(c_script.p3_w.ToString());
                c_script.p4_w = checkBox5.Checked;
                lines.Add(c_script.p4_w.ToString());

                lines.Add(c_script.active.ToString());

                c_script.lineEndingIndex = comboBox2.SelectedIndex >= 0 ? comboBox2.SelectedIndex : 1;
                lines.Add(c_script.lineEndingIndex.ToString());
            }

            File.WriteAllLines(c_script.path, lines);
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
                string filePath = Path.Combine("./scripts", textBox5.Text);
                try
                {
                    string[] newfile = new string[]
                    {
                        "test",
                        "1",
                        "false",
                        "false",
                        "false",
                        "false",
                        "%100%!LMB",
                        "false"
                    };
                    File.WriteAllLines(filePath, newfile);
                }
                catch (IOException ex)
                {
                    Console.WriteLine("An error occurred while creating the file: " + ex.Message);
                }
                textBox5.Text = "";
            }
            form1.ScanScripts();
            checkedListBox1.DataSource = form1.scripts;
            checkedListBox1.DisplayMember = "name";
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            if (comboBox1.Text == "Keypress")
            {
                comboBox2.Enabled = false;
                comboBox2.SelectedIndex = -1;
            }
            else if (comboBox1.Text == "Write back")
            {
                comboBox2.Enabled = true;
                if (comboBox2.SelectedIndex == -1)
                    comboBox2.SelectedIndex = 1; // Default a NL
            }
        }

        public void displayscript()
        {
            _isLoading = true;
            textBox4.Text = "";
            comboBox1.Text = "";
            textBox3.Text = "";
            checkBox8.Checked = false;
            checkBox7.Checked = false;
            checkBox6.Checked = false;
            checkBox5.Checked = false;
            comboBox2.SelectedIndex = -1;
            comboBox2.Enabled = false;

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
                    comboBox2.Enabled = false;
                    comboBox2.SelectedIndex = -1;
                }
                else if (c_script.trigger == 2)
                {
                    comboBox1.Text = "Write back";
                    textBox3.Text = c_script.reply;
                    checkBox8.Checked = c_script.p1_w;
                    checkBox7.Checked = c_script.p2_w;
                    checkBox6.Checked = c_script.p3_w;
                    checkBox5.Checked = c_script.p4_w;
                    comboBox2.Enabled = true;

                    if (c_script.lineEndingIndex >= 0 && c_script.lineEndingIndex < comboBox2.Items.Count)
                    {
                        comboBox2.SelectedIndex = c_script.lineEndingIndex;
                    }
                    else
                    {
                        comboBox2.SelectedIndex = 1; // Default a NL
                    }
                }
            }
            _isLoading = false;
        }
    }
}