using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Globalization;

namespace SerialPortMacros
{
    public partial class Form7 : Form
    {
        private readonly bool _isEditMode;
        private readonly Form1? _form1;
        private InterfaceDocument _document = new();
        private string _currentFilePath = "";
        private int _rotationAngle = 0;

        private string ModulePath => Path.Combine(AppContext.BaseDirectory, "module");

        private PictureBox? _draggedPicture;
        private Point _dragStartMouse;
        private Point _dragStartLocation;
        private bool _isResizing = false;
        private Size _resizeStartSize;
        private System.Windows.Forms.Timer? _refreshTimer;

        public Form7(Form1? form1, bool isEditMode)
        {
            InitializeComponent();
            _form1 = form1;

            panel1.MouseDown += (s, ev) => { this.ActiveControl = null; };

            this.KeyPreview = true;
            this.KeyDown += Form7_KeyDown;
            _isEditMode = isEditMode;

            // Configurazione finestra in base alla modalità
            if (_isEditMode)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.MaximizeBox = true;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.MaximizeBox = false;
            }

            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel1.AutoScroll = true;

            if (moduleBox != null) moduleBox.Visible = isEditMode;
            if (add_button != null) add_button.Visible = isEditMode;
            if (Save_button != null) Save_button.Visible = isEditMode;

            if (!_isEditMode)
            {
                _refreshTimer = new System.Windows.Forms.Timer();
                _refreshTimer.Interval = 250;
                _refreshTimer.Tick += RefreshTimer_Tick;
                _refreshTimer.Start();
            }
        }

        private void Form7_Load(object sender, EventArgs e)
        {
            Directory.CreateDirectory(ModulePath);
            if (moduleBox != null)
            {
                moduleBox.Items.Clear();
                foreach (string directory in Directory.GetDirectories(ModulePath))
                {
                    moduleBox.Items.Add(Path.GetFileName(directory));
                }
                if (moduleBox.Items.Count > 0) moduleBox.SelectedIndex = 0;
            }

            // --- INIZIALIZZAZIONE VARIABILI DI PANNELLO ALL'AVVIO ---
            if (!_isEditMode && _form1 != null)
            {
                foreach (var element in _document.Elements)
                {
                    if (element.ElementType == "NumericInput" && !string.IsNullOrEmpty(element.VariableName))
                    {
                        _form1.PanelVariables[element.VariableName.Trim()] = element.NumericValue;
                    }
                    else if (element.ElementType == "Toggle" && !string.IsNullOrEmpty(element.VariableName))
                    {
                        // Imposta di default a 0 (off) se non esiste già
                        _form1.PanelVariables.GetOrAdd(element.VariableName.Trim(), 0.0);
                    }
                }
            }
        }

        public void LoadDocument(string filePath)
        {
            _currentFilePath = filePath;
            _document = SpmSerializer.Load(filePath);
            if (_document.Width > 200 && _document.Height > 200)
            {
                this.ClientSize = new Size(_document.Width, _document.Height);
            }
            RenderDocument();
        }

        private void Form7_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!_isEditMode || _draggedPicture == null) return;
            if (e.KeyCode == Keys.R)
            {
                _rotationAngle = (_rotationAngle + 90) % 360;
                _draggedPicture.Image?.RotateFlip(RotateFlipType.Rotate90FlipNone);
                _draggedPicture.Refresh();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void add_button_Click(object sender, EventArgs e)
        {
            if (moduleBox == null || moduleBox.SelectedItem == null)
            {
                MessageBox.Show("Seleziona prima un modulo.");
                return;
            }

            string moduleName = moduleBox.SelectedItem.ToString()!;
            string moduleDirectory = Path.Combine(ModulePath, moduleName);

            if (!Directory.Exists(moduleDirectory))
            {
                MessageBox.Show("La cartella del modulo non esiste.");
                return;
            }

            // Apertura diretta del primo PNG come nell'originale
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = moduleDirectory;
            dialog.Filter = "Immagini PNG (*.png)|*.png";
            dialog.Title = "Seleziona un'immagine";

            if (dialog.ShowDialog() != DialogResult.OK) return;

            string selectedPath = Path.GetFullPath(dialog.FileName);
            string expectedDirectory = Path.GetFullPath(moduleDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!selectedPath.StartsWith(expectedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Seleziona un'immagine del modulo.");
                return;
            }

            string imageName = Path.GetFileName(selectedPath);

            var element = new InterfaceElement
            {
                ModuleName = moduleName,
                ImageName = imageName,
                ElementType = "Button",
                X = 20,
                Y = 20,
                Width = 160,
                Height = 120,
                TargetPorts = new bool[] { true, false, false, false },
                StringFormatTemplate = "",
                SecondImageName = "",
                MessageOffToOn = "",
                MessageOnToOff = "",
                VariableName = "",
                ConditionExpression = ""
            };

            _document.Elements.Add(element);
            RenderElement(element);
        }

        private void Save_button_Click(object sender, EventArgs e)
        {
            // Salva le dimensioni attuali della finestra
            _document.Width = this.ClientSize.Width;
            _document.Height = this.ClientSize.Height;

            foreach (Control control in panel1.Controls)
            {
                if (control is PictureBox picture && picture.Tag is InterfaceElement element)
                {
                    element.X = picture.Left;
                    element.Y = picture.Top;
                    element.Width = picture.Width;
                    element.Height = picture.Height;
                }
                else if (control is TextBox tb && tb.Tag is InterfaceElement elInput)
                {
                    elInput.NumericValue = double.TryParse(tb.Text, out double val) ? val : 0;
                    if (_form1 != null && !string.IsNullOrEmpty(elInput.VariableName))
                    {
                        _form1.PanelVariables[elInput.VariableName] = elInput.NumericValue;
                    }
                }
            }

            string interfacesPath = Path.Combine(AppContext.BaseDirectory, "interfaces");
            Directory.CreateDirectory(interfacesPath);

            using SaveFileDialog dialog = new SaveFileDialog();
            dialog.InitialDirectory = interfacesPath;
            dialog.Filter = "Serial Port Macro (*.spm)|*.spm";
            dialog.DefaultExt = "spm";
            dialog.AddExtension = true;
            dialog.Title = "Salva interfaccia";

            if (!string.IsNullOrEmpty(_currentFilePath)) dialog.FileName = Path.GetFileName(_currentFilePath);
            if (dialog.ShowDialog() != DialogResult.OK) return;

            _document.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
            SpmSerializer.Save(dialog.FileName, _document);
            _currentFilePath = dialog.FileName;
            MessageBox.Show("Interfaccia salvata.");
        }

        private void RenderElement(InterfaceElement element)
        {
            string imagePath = Path.Combine(ModulePath, element.ModuleName, element.ImageName);
            Image? imgToUse = null;

            try
            {
                if (File.Exists(imagePath))
                {
                    using (Image loadedImage = Image.FromFile(imagePath))
                    {
                        imgToUse = new Bitmap(loadedImage);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Impossibile caricare l'immagine {imagePath}: {ex.Message}");
            }

            if (imgToUse == null)
            {
                Bitmap fallbackBmp = new Bitmap(Math.Max(32, element.Width), Math.Max(32, element.Height));
                using (Graphics g = Graphics.FromImage(fallbackBmp))
                {
                    g.Clear(Color.LightGray);
                    g.DrawRectangle(Pens.Red, 0, 0, fallbackBmp.Width - 1, fallbackBmp.Height - 1);
                    g.DrawString("Err Img", new Font("Segoe UI", 8), Brushes.Red, 2, 2);
                }
                imgToUse = fallbackBmp;
            }

            PictureBox picture = new PictureBox
            {
                Location = new Point(element.X, element.Y),
                Size = new Size(element.Width, element.Height),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = _isEditMode ? BorderStyle.FixedSingle : BorderStyle.None,
                Tag = element,
                Image = imgToUse
            };

            if (element.Rotation != 0)
            {
                int rotations = (element.Rotation / 90) % 4;
                for (int i = 0; i < rotations; i++) picture.Image?.RotateFlip(RotateFlipType.Rotate90FlipNone);
            }

            panel1.Controls.Add(picture);

            if (_isEditMode)
            {
                picture.Paint += Element_Paint;
                picture.MouseDown += Element_MouseDown;
                picture.MouseMove += Element_MouseMove;
                picture.MouseUp += Element_MouseUp;
                picture.DoubleClick += (s, e) => OpenElementConfig(element, picture);
            }
            else
            {
                if (element.ElementType == "Button")
                {
                    picture.MouseDown += Button_MouseDown;
                    picture.MouseUp += Button_MouseUp;
                }
                else if (element.ElementType == "Toggle")
                {
                    picture.MouseClick += Toggle_MouseClick;
                }
                else if (element.ElementType == "Label")
                {
                    int lblWidth = element.Width;
                    int lblHeight = element.Height;
                    int lblX = element.X;
                    int lblY = element.Y;

                    string labelText = !string.IsNullOrEmpty(element.StringFormatTemplate) ? element.StringFormatTemplate : element.VariableName;

                    // Partiamo da una dimensione proporzionale all'altezza e la riduciamo finché il testo entra nei limiti
                    float fontSize = Math.Max(6f, element.Height * 0.5f);

                    while (fontSize > 4f)
                    {
                        using (Font testFont = new Font("Segoe UI", fontSize, FontStyle.Bold))
                        {
                            // TextRenderer misura esattamente lo stesso motore di rendering delle Label di Windows Forms
                            Size textSize = TextRenderer.MeasureText(labelText, testFont);
                            if (textSize.Width <= lblWidth && textSize.Height <= lblHeight)
                            {
                                break;
                            }
                            fontSize -= 0.5f;
                        }
                    }
                    if (fontSize < 4f) fontSize = 4f;

                    Label staticLbl = new Label
                    {
                        Location = new Point(lblX, lblY),
                        Width = lblWidth,
                        Height = lblHeight,
                        ForeColor = Color.Black,
                        BackColor = Color.Transparent,
                        Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Text = labelText,
                        Tag = element
                    };
                    panel1.Controls.Add(staticLbl);
                    staticLbl.BringToFront();
                }
                else if (element.ElementType == "NumericInput")
                {
                    int tbWidth = Math.Max(40, element.Width - 12);

                    // Definiamo un font proporzionato all'altezza dell'elemento
                    float fontSize = Math.Max(8f, Math.Min(16f, element.Height * 0.45f));

                    TextBox inputTb = new TextBox
                    {
                        Width = tbWidth,
                        Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                        Text = element.NumericValue.ToString(CultureInfo.InvariantCulture),
                        BorderStyle = BorderStyle.None, // Rimuove completamente il riquadro nero/bordo rigido
                        BackColor = Color.White,
                        ForeColor = Color.Black,
                        TextAlign = HorizontalAlignment.Center,
                        Tag = element
                    };

                    // Centramento orizzontale e verticale perfetto basato sull'altezza nativa (PreferredHeight)
                    int tbX = element.X + (element.Width - tbWidth) / 2;
                    int tbY = element.Y + (element.Height - inputTb.PreferredHeight) / 2;
                    inputTb.Location = new Point(tbX, tbY);

                    // Inizializza subito la variabile nel dizionario di Form1 all'avvio
                    if (!_isEditMode && _form1 != null && !string.IsNullOrEmpty(element.VariableName))
                    {
                        _form1.PanelVariables[element.VariableName.Trim()] = element.NumericValue;
                    }

                    inputTb.TextChanged += (s, e) =>
                    {
                        if (double.TryParse(inputTb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                        {
                            element.NumericValue = val;
                            if (_form1 != null && !string.IsNullOrEmpty(element.VariableName))
                            {
                                _form1.PanelVariables[element.VariableName.Trim()] = val;
                            }
                        }
                    };

                    panel1.Controls.Add(inputTb);
                    inputTb.BringToFront();
                }
                else if (element.ElementType == "NumericOutput")
                {
                    int lblWidth = Math.Max(40, element.Width - 16);
                    int lblHeight = Math.Max(20, element.Height / 2);
                    int lblX = element.X + (element.Width - lblWidth) / 2;
                    int lblY = element.Y + (element.Height - lblHeight) / 2;

                    float fontSize = Math.Max(8f, Math.Min(16f, element.Height * 0.4f));

                    Label outputLbl = new Label
                    {
                        Location = new Point(lblX, lblY),
                        Width = lblWidth,
                        Height = lblHeight,
                        Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                        ForeColor = Color.Black,
                        BackColor = Color.White,              // Sfondo bianco pulito stile display
                        BorderStyle = BorderStyle.None,// Bordo sottile e ordinato
                        TextAlign = ContentAlignment.MiddleCenter,
                        Text = "0",
                        Tag = element
                    };

                    panel1.Controls.Add(outputLbl);
                    outputLbl.BringToFront();
                }
            }
        }

        // --- Apertura di Form8 per la modifica parametro con i componenti specificati ---
        private void OpenElementConfig(InterfaceElement element, PictureBox picture)
        {
            using (Form8 form8 = new Form8())
            {
                string moduleDirectory = Path.Combine(ModulePath, element.ModuleName);
                form8.ModuleDirectory = moduleDirectory;

                form8.comboBox1.Items.Clear();
                form8.comboBox1.Items.AddRange(new string[] { "Button", "Toggle", "NumericInput", "NumericOutput", "Led", "Label" });
                form8.comboBox1.SelectedItem = string.IsNullOrEmpty(element.ElementType) ? "Button" : element.ElementType;

                form8.textBox2.Text = element.VariableName;
                form8.textBox1.Text = element.SecondImageName;
                form8.textBox4.Text = element.StringFormatTemplate;
                form8.textBox3.Text = element.MessageOnToOff;
                form8.textBox5.Text = element.ConditionExpression;

                form8.checkBox1.Checked = element.TargetPorts.Length > 0 && element.TargetPorts[0];
                form8.checkBox2.Checked = element.TargetPorts.Length > 1 && element.TargetPorts[1];
                form8.checkBox3.Checked = element.TargetPorts.Length > 2 && element.TargetPorts[2];
                form8.checkBox4.Checked = element.TargetPorts.Length > 3 && element.TargetPorts[3];

                form8.lineEndingCombo.SelectedItem = string.IsNullOrEmpty(element.CustomLineEnding) ? "Eredita da Form1" : element.CustomLineEnding;

                DialogResult result = form8.ShowDialog();
                if (result == DialogResult.Abort)
                {
                    // Rimuove esclusivamente questo singolo elemento basandosi sul suo ID univoco
                    _document.Elements.RemoveAll(el => el.Id == element.Id);
                    RenderDocument();
                    return;
                }

                if (result == DialogResult.OK)
                {
                    string selectedEnding = form8.lineEndingCombo.SelectedItem?.ToString() ?? "";
                    element.CustomLineEnding = (selectedEnding == "Eredita da Form1") ? "" : selectedEnding;
                    element.ElementType = form8.comboBox1.SelectedItem?.ToString() ?? "Button";
                    element.VariableName = form8.textBox2.Text;
                    element.SecondImageName = form8.textBox1.Text;
                    element.StringFormatTemplate = form8.textBox4.Text;
                    element.MessageOnToOff = form8.textBox3.Text;
                    element.ConditionExpression = form8.textBox5.Text;
                    element.TargetPorts = new bool[] {
                form8.checkBox1.Checked,
                form8.checkBox2.Checked,
                form8.checkBox3.Checked,
                form8.checkBox4.Checked
            };
                }
            }
        }

        // --- Logica Bottone: Feedback visivo (scurimento) e invio riga ---
        private void Button_MouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (sender is PictureBox picture && picture.Tag is InterfaceElement element && picture.Image != null)
            {
                try
                {
                    picture.Image = DarkenImage(picture.Image);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Button_MouseDown error: " + ex.Message);
                }
            }
        }

        private void Button_MouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (sender is PictureBox picture && picture.Tag is InterfaceElement element)
            {
                string imagePath = Path.Combine(ModulePath, element.ModuleName, element.ImageName);
                try
                {
                    if (File.Exists(imagePath))
                    {
                        using (Image loadedImage = Image.FromFile(imagePath))
                        {
                            picture.Image = new Bitmap(loadedImage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore caricamento immagine bottone up {imagePath}: {ex.Message}");
                }
                ExecuteButtonAction(element);
            }
        }

        private Image DarkenImage(Image img)
        {
            Bitmap bmp = new Bitmap(img);
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    Color c = bmp.GetPixel(x, y);
                    Color dimmed = Color.FromArgb(c.A, Math.Max(0, c.R - 60), Math.Max(0, c.G - 60), Math.Max(0, c.B - 60));
                    bmp.SetPixel(x, y, dimmed);
                }
            }
            return bmp;
        }

        private void ExecuteButtonAction(InterfaceElement element)
        {
            if (_form1 == null) return;

            if (!string.IsNullOrEmpty(element.ConditionExpression))
            {
                if (!EvaluateCondition(element)) return;
            }

            string formattedMessage = FormatMessageWithVariablesAndSerial(element.StringFormatTemplate);

            if (formattedMessage.Contains("++"))
            {
                string varName = formattedMessage.Replace("++", "").Trim();
                double currentVal = _form1.PanelVariables.GetOrAdd(varName, 0);
                currentVal++;
                _form1.PanelVariables[varName] = currentVal;
                formattedMessage = currentVal.ToString(CultureInfo.InvariantCulture);
            }

            // Passiamo anche il terminatore personalizzato dell'elemento
            _form1.SendCustomMacroLine(element.TargetPorts, formattedMessage, element.CustomLineEnding);
        }

        // --- Logica Toggle Switch ---
        private void Toggle_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (sender is PictureBox picture && picture.Tag is InterfaceElement element && _form1 != null)
            {
                element.IsToggled = !element.IsToggled;

                if (!string.IsNullOrEmpty(element.VariableName))
                {
                    _form1.PanelVariables[element.VariableName.Trim()] = element.IsToggled ? 1.0 : 0.0;
                }

                string targetImg = element.IsToggled ? element.SecondImageName : element.ImageName;
                if (string.IsNullOrEmpty(targetImg)) targetImg = element.ImageName;

                string imagePath = Path.Combine(ModulePath, element.ModuleName, targetImg);
                try
                {
                    if (File.Exists(imagePath))
                    {
                        picture.Image?.Dispose();
                        using (Image loadedImage = Image.FromFile(imagePath))
                        {
                            picture.Image = new Bitmap(loadedImage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore caricamento immagine toggle {imagePath}: {ex.Message}");
                }

                string msgToSend = element.IsToggled ? element.StringFormatTemplate : element.MessageOnToOff;
                _form1.SendCustomMacroLine(element.TargetPorts, msgToSend);
            }
        }

        // --- Timer per Refresh Campi Output e Led ---
        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (_form1 == null || _isEditMode) return;

            foreach (Control control in panel1.Controls)
            {
                if (control is PictureBox picture && picture.Tag is InterfaceElement element)
                {
                    if (element.ElementType == "Led")
                    {
                        bool conditionMet = EvaluateCondition(element);
                        string targetImg = conditionMet ? element.SecondImageName : element.ImageName;
                        if (string.IsNullOrEmpty(targetImg)) targetImg = element.ImageName;

                        string imagePath = Path.Combine(ModulePath, element.ModuleName, targetImg);
                        try
                        {
                            if (File.Exists(imagePath))
                            {
                                using (Image loadedImage = Image.FromFile(imagePath))
                                {
                                    if (picture.Image != null) picture.Image.Dispose();
                                    picture.Image = new Bitmap(loadedImage);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Errore caricamento immagine led {imagePath}: {ex.Message}");
                        }
                    }
                }
                else if (control is Label lbl && lbl.Tag is InterfaceElement elOutput)
                {
                    if (elOutput.ElementType == "NumericOutput" && !string.IsNullOrEmpty(elOutput.VariableName))
                    {
                        string varName = elOutput.VariableName.Trim();
                        string template = varName.StartsWith("{") ? varName : "{" + varName + "}";
                        string evaluated = FormatMessageWithVariablesAndSerial(template);

                        if (evaluated != template)
                        {
                            lbl.Text = evaluated;
                        }
                        else if (_form1.PanelVariables.TryGetValue(varName, out double val))
                        {
                            lbl.Text = val.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (_form1.LatestSerialValues.TryGetValue(varName, out string? sVal))
                        {
                            lbl.Text = sVal ?? "";
                        }
                        else
                        {
                            lbl.Text = "0";
                        }
                    }
                }
            }
        }

        private bool EvaluateCondition(InterfaceElement element)
        {
            if (_form1 == null) return true;

            string expr = FormatMessageWithVariablesAndSerial(element.ConditionExpression).Trim();
            if (string.IsNullOrEmpty(expr))
            {
                // Se è un led senza condizione, di default verifica se è > 0
                if (element.ElementType == "Led")
                {
                    double val = 0;
                    if (!string.IsNullOrEmpty(element.VariableName) && _form1.PanelVariables.ContainsKey(element.VariableName))
                        val = _form1.PanelVariables[element.VariableName];
                    return val > 0;
                }
                return true; // Per i bottoni, se non c'è condizione, l'invio è sempre consentito
            }

            // Gestione operatori di confronto
            string[] ops = { ">=", "<=", "==", ">", "<" };
            string foundOp = "";
            string[] parts = null;

            foreach (var op in ops)
            {
                if (expr.Contains(op))
                {
                    parts = expr.Split(new string[] { op }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        foundOp = op;
                        break;
                    }
                }
            }

            if (parts != null && parts.Length == 2)
            {
                if (double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double left) &&
                    double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double right))
                {
                    return foundOp switch
                    {
                        ">" => left > right,
                        ">=" => left >= right,
                        "<" => left < right,
                        "<=" => left <= right,
                        "==" => Math.Abs(left - right) < 0.00001,
                        _ => false
                    };
                }
            }

            // Se la condizione è una singola variabile booleana/numerica (es. {toggle_generale})
            if (double.TryParse(expr, NumberStyles.Float, CultureInfo.InvariantCulture, out double singleVal))
            {
                return singleVal > 0;
            }

            return true;
        }

        private void Element_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not PictureBox picture) return;
            const int gripSize = 10;
            Rectangle grip = new Rectangle(picture.ClientSize.Width - gripSize, picture.ClientSize.Height - gripSize, gripSize, gripSize);
            e.Graphics.FillRectangle(Brushes.DodgerBlue, grip);
            e.Graphics.DrawRectangle(Pens.White, grip);
        }

        private void Element_MouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_isEditMode || e.Button != MouseButtons.Left || sender is not PictureBox picture) return;
            _draggedPicture = picture;
            _dragStartMouse = panel1.PointToClient(Control.MousePosition);
            _dragStartLocation = picture.Location;
            _resizeStartSize = picture.Size;
            const int gripSize = 14;
            Rectangle grip = new Rectangle(picture.ClientSize.Width - gripSize, picture.ClientSize.Height - gripSize, gripSize, gripSize);
            _isResizing = grip.Contains(e.Location);
            picture.BringToFront();
            picture.Capture = true;
        }

        private void Element_MouseMove(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_isEditMode || _draggedPicture == null) return;
            Point currentMouse = panel1.PointToClient(Control.MousePosition);
            int dx = currentMouse.X - _dragStartMouse.X;
            int dy = currentMouse.Y - _dragStartMouse.Y;

            if (_isResizing)
            {
                int newWidth = Math.Max(32, _resizeStartSize.Width + dx);
                int newHeight = Math.Max(32, _resizeStartSize.Height + dy);
                _draggedPicture.Size = new Size(newWidth, newHeight);
            }
            else
            {
                _draggedPicture.Location = new Point(_dragStartLocation.X + dx, _dragStartLocation.Y + dy);
            }
        }


        private string FormatMessageWithVariablesAndSerial(string template)
        {
            if (_form1 == null) return template;
            string result = template;

            // 1. Sostituzione delle variabili di pannello (es. {mia_variabile})
            foreach (var kvp in _form1.PanelVariables)
            {
                result = result.Replace("{" + kvp.Key + "}", kvp.Value.ToString(CultureInfo.InvariantCulture));
            }

            // 2. Sostituzione dei valori seriali completi (es. {port1})
            foreach (var kvp in _form1.LatestSerialValues)
            {
                result = result.Replace("{" + kvp.Key + "}", kvp.Value);
            }

            // 3. Sostituzione della sintassi puntuale tipo {1P1} (Porta logica 1, 1° numero -> indice 0)
            var regex = new System.Text.RegularExpressions.Regex(@"\{([1-4])P([0-9]+)\}");
            result = regex.Replace(result, match =>
            {
                int portIdx = int.Parse(match.Groups[1].Value);
                int numIdx = int.Parse(match.Groups[2].Value) - 1; // 1-based (1P1 = primo numero = indice 0)

                if (_form1.LatestPortNumbers.TryGetValue(portIdx, out var listNums) && numIdx >= 0 && numIdx < listNums.Count)
                {
                    return listNums[numIdx].ToString(CultureInfo.InvariantCulture);
                }
                return "0";
            });

            return result;
        }

        private void Element_MouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_draggedPicture == null) return;
            _draggedPicture.Capture = false;
            if (_draggedPicture.Tag is InterfaceElement element)
            {
                element.X = _draggedPicture.Left;
                element.Y = _draggedPicture.Top;
                element.Width = _draggedPicture.Width;
                element.Height = _draggedPicture.Height;
            }
            _draggedPicture = null;
            _isResizing = false;
        }

        private void RenderDocument()
        {
            foreach (Control control in panel1.Controls)
            {
                if (control is PictureBox picture)
                {
                    picture.Image?.Dispose();
                    picture.Image = null;
                }
                control.Dispose();
            }
            panel1.Controls.Clear();

            foreach (InterfaceElement element in _document.Elements)
            {
                RenderElement(element);
            }
        }
    }

    public class InterfaceElement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ModuleName { get; set; } = "";
        public string ImageName { get; set; } = "";         // Primo PNG (scelto all'importazione)
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Rotation { get; set; } = 0;

        // Campi di configurazione mappati su Form8
        public string ElementType { get; set; } = "Button"; // comboBox1
        public string VariableName { get; set; } = "";        // textBox2
        public string SecondImageName { get; set; } = "";     // textBox1 (second image per led/toggle)
        public string StringFormatTemplate { get; set; } = ""; // textBox4 (messaggio bottone / off->on)
        public string MessageOnToOff { get; set; } = "";      // textBox3 (on->off)

        public string MessageOffToOn { get; set; } = "";      // textBox3 (off->on)

        public bool[] TargetPorts { get; set; } = { true, false, false, false }; // checkBox1, 2, 3, 4
        public string ConditionExpression { get; set; } = ""; // textBox5 (if condition per led)

        public bool IsToggled { get; set; } = false;
        public double NumericValue { get; set; } = 0;

        public string CustomLineEnding { get; set; } = ""; // Per Form8

    }

    public class InterfaceDocument
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public int Width { get; set; } = 800;   // Larghezza salvata
        public int Height { get; set; } = 600;  // Altezza salvata
        public List<InterfaceElement> Elements { get; set; } = new();
    }

    public static class SpmSerializer
    {
        public static void Save(string filePath, InterfaceDocument document)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(document, options);
            File.WriteAllText(filePath, json);
        }

        public static InterfaceDocument Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<InterfaceDocument>(json) ?? throw new InvalidDataException("Il file SPM non contiene un documento valido.");
        }
    }
}
