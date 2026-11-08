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
        private bool _wasShiftDown = false;
        private System.Windows.Forms.Timer? _refreshTimer;
        private bool new_interface;
        private bool _wasControlDown;

        private readonly List<int> _verticalSnapGuides = new List<int>();
        private readonly List<int> _horizontalSnapGuides = new List<int>();

        public Form7(Form1? form1, bool isEditMode, bool is_new = false)
        {
            InitializeComponent();
            _form1 = form1;
            panel1.Paint += Panel1_Paint;

            new_interface = is_new;

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

            int initialWidth = 160;
            int initialHeight = 120;
            double imageAspectRatio = 4.0 / 3.0;

            try
            {
                using (Image importedImage = Image.FromFile(selectedPath))
                {
                    imageAspectRatio = (double)importedImage.Width / importedImage.Height;
                    double scale = Math.Min(160.0 / importedImage.Width, 120.0 / importedImage.Height);
                    initialWidth = Math.Max(32, (int)Math.Round(importedImage.Width * scale));
                    initialHeight = Math.Max(32, (int)Math.Round(importedImage.Height * scale));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Impossibile leggere le dimensioni dell'immagine: " + ex.Message);
            }

            var element = new InterfaceElement
            {
                ModuleName = moduleName,
                ImageName = imageName,
                ElementType = "Button",
                X = 20,
                Y = 20,
                Width = initialWidth,
                Height = initialHeight,
                ImageAspectRatio = imageAspectRatio,
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

            if (new_interface)
            {
                using SaveFileDialog dialog = new SaveFileDialog();
                dialog.InitialDirectory = interfacesPath;
                dialog.Filter = "Serial Port Macro (*.spm)|*.spm";
                dialog.DefaultExt = "spm";
                dialog.AddExtension = true;
                dialog.Title = "Salva interfaccia";

                if (!string.IsNullOrEmpty(_currentFilePath)) dialog.FileName = Path.GetFileName(_currentFilePath);
                if (dialog.ShowDialog() != DialogResult.OK) return;

                _document.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
                _currentFilePath = dialog.FileName;
            }

            _document.Name = Path.GetFileNameWithoutExtension(_currentFilePath);
            SpmSerializer.Save(_currentFilePath, _document);
            this.Close();
        }

        private void RenderElement(InterfaceElement element)
        {
            Image? imgToUse = null;

            // Le Label non hanno bisogno di immagini.
            bool isLabel = element.ElementType == "Label";

            if (!isLabel)
            {
                string imagePath = Path.Combine(
                    ModulePath,
                    element.ModuleName,
                    element.ImageName);

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
                    System.Diagnostics.Debug.WriteLine(
                        $"Impossibile caricare l'immagine {imagePath}: {ex.Message}");
                }

                // Inizializza l'aspect ratio se non è ancora presente.
                if (imgToUse != null && element.ImageAspectRatio <= 0)
                {
                    element.ImageAspectRatio =
                        (double)imgToUse.Width / imgToUse.Height;
                }

                // Immagine di fallback.
                if (imgToUse == null)
                {
                    Bitmap fallbackBmp = new Bitmap(
                        Math.Max(32, element.Width),
                        Math.Max(32, element.Height));

                    using (Graphics g = Graphics.FromImage(fallbackBmp))
                    {
                        g.Clear(Color.LightGray);

                        g.DrawRectangle(
                            Pens.Red,
                            0,
                            0,
                            fallbackBmp.Width - 1,
                            fallbackBmp.Height - 1);

                        using Font errorFont = new Font(
                            "Segoe UI",
                            8);

                        g.DrawString(
                            "Err Img",
                            errorFont,
                            Brushes.Red,
                            2,
                            2);
                    }

                    imgToUse = fallbackBmp;
                }
            }

            // -----------------------------------------
            // PICTUREBOX PRINCIPALE
            // -----------------------------------------

            PictureBox picture = new PictureBox
            {
                Location = new Point(element.X, element.Y),

                Size = new Size(
                    Math.Max(32, element.Width),
                    Math.Max(32, element.Height)),

                SizeMode = PictureBoxSizeMode.StretchImage,

                BorderStyle = _isEditMode
                    ? BorderStyle.FixedSingle
                    : BorderStyle.None,

                BackColor = Color.Transparent,

                Tag = element,

                Image = imgToUse
            };

            // Rotazione immagine.
            if (element.Rotation != 0)
            {
                int rotations = (element.Rotation / 90) % 4;

                for (int i = 0; i < rotations; i++)
                {
                    picture.Image?.RotateFlip(
                        RotateFlipType.Rotate90FlipNone);
                }
            }

            panel1.Controls.Add(picture);

            // -----------------------------------------
            // MODALITA' EDITING
            // -----------------------------------------

            if (_isEditMode)
            {
                picture.Paint += Element_Paint;

                picture.MouseDown += Element_MouseDown;
                picture.MouseMove += Element_MouseMove;
                picture.MouseUp += Element_MouseUp;

                picture.DoubleClick +=
                    (s, e) => OpenElementConfig(element, picture);
            }
            else
            {
                // -------------------------------------
                // BUTTON
                // -------------------------------------

                if (element.ElementType == "Button")
                {
                    picture.MouseDown += Button_MouseDown;
                    picture.MouseUp += Button_MouseUp;
                }

                // -------------------------------------
                // TOGGLE
                // -------------------------------------

                else if (element.ElementType == "Toggle")
                {
                    picture.MouseClick += Toggle_MouseClick;
                }

                // -------------------------------------
                // LABEL
                // -------------------------------------

                else if (element.ElementType == "Label")
                {
                    int lblWidth = element.Width;
                    int lblHeight = element.Height;

                    string labelText =
                        !string.IsNullOrEmpty(element.StringFormatTemplate)
                            ? element.StringFormatTemplate
                            : element.VariableName;

                    float fontSize = CalculateLabelFontSize(
                        labelText,
                        lblWidth,
                        lblHeight);

                    Label staticLbl = new Label
                    {
                        Location = new Point(
                            element.X,
                            element.Y),

                        Width = lblWidth,
                        Height = lblHeight,

                        AutoSize = false,
                        AutoEllipsis = false,

                        Padding = new Padding(2),

                        ForeColor = Color.Black,
                        BackColor = Color.Transparent,

                        Font = new Font(
                            "Segoe UI",
                            fontSize,
                            FontStyle.Bold),

                        TextAlign = ContentAlignment.MiddleCenter,

                        UseCompatibleTextRendering = false,

                        Text = labelText,

                        Tag = element
                    };

                    panel1.Controls.Add(staticLbl);
                    staticLbl.BringToFront();
                }

                // -------------------------------------
                // NUMERIC INPUT
                // -------------------------------------

                else if (element.ElementType == "NumericInput")
                {
                    int tbWidth = Math.Max(40, element.Width - 12);

                    float fontSize = Math.Max(
                        8f,
                        Math.Min(16f, element.Height * 0.45f));

                    TextBox inputTb = new TextBox
                    {
                        Width = tbWidth,

                        Font = new Font(
                            "Segoe UI",
                            fontSize,
                            FontStyle.Bold),

                        Text = element.NumericValue.ToString(
                            CultureInfo.InvariantCulture),

                        BorderStyle = BorderStyle.None,

                        BackColor = Color.White,
                        ForeColor = Color.Black,

                        TextAlign = HorizontalAlignment.Center,

                        Tag = element
                    };

                    int tbX = element.X +
                        (element.Width - tbWidth) / 2;

                    int tbY = element.Y +
                        (element.Height - inputTb.PreferredHeight) / 2;

                    inputTb.Location = new Point(tbX, tbY);

                    if (_form1 != null &&
                        !string.IsNullOrEmpty(element.VariableName))
                    {
                        _form1.PanelVariables[
                            element.VariableName.Trim()
                        ] = element.NumericValue;
                    }

                    inputTb.TextChanged += (s, e) =>
                    {
                        if (double.TryParse(
                            inputTb.Text,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double val))
                        {
                            element.NumericValue = val;

                            if (_form1 != null &&
                                !string.IsNullOrEmpty(element.VariableName))
                            {
                                _form1.PanelVariables[
                                    element.VariableName.Trim()
                                ] = val;
                            }
                        }
                    };

                    panel1.Controls.Add(inputTb);
                    inputTb.BringToFront();
                }

                // -------------------------------------
                // NUMERIC OUTPUT
                // -------------------------------------

                else if (element.ElementType == "NumericOutput")
                {
                    int lblWidth = Math.Max(40, element.Width - 16);
                    int lblHeight = Math.Max(20, element.Height / 2);

                    int lblX = element.X +
                        (element.Width - lblWidth) / 2;

                    int lblY = element.Y +
                        (element.Height - lblHeight) / 2;

                    float fontSize = Math.Max(
                        8f,
                        Math.Min(16f, element.Height * 0.4f));

                    Label outputLbl = new Label
                    {
                        Location = new Point(lblX, lblY),

                        Width = lblWidth,
                        Height = lblHeight,

                        Font = new Font(
                            "Segoe UI",
                            fontSize,
                            FontStyle.Bold),

                        ForeColor = Color.Black,
                        BackColor = Color.White,

                        BorderStyle = BorderStyle.None,

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
                    RenderDocument();
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

        private void Element_MouseMove(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_isEditMode || _draggedPicture == null) return;

            Point currentMouse = panel1.PointToClient(Control.MousePosition);

            InterfaceElement? element = _draggedPicture.Tag as InterfaceElement;
            bool controlDown = (Control.ModifierKeys & Keys.Control) != 0;
            bool shiftDown = (Control.ModifierKeys & Keys.Shift) != 0;

            // Rileva il cambiamento dello stato di Shift.
            // La nuova baseline evita salti nelle dimensioni.
            if (_isResizing && controlDown != _wasControlDown)
            {
                _resizeStartSize = _draggedPicture.Size;
                _dragStartMouse = currentMouse;

                if (element != null && element.ElementType != "Label")
                {
                    element.ImageAspectRatio =
                        (double)_draggedPicture.Width / _draggedPicture.Height;
                }

                _wasControlDown = controlDown;
            }

            int dx = currentMouse.X - _dragStartMouse.X;
            int dy = currentMouse.Y - _dragStartMouse.Y;

            if (_isResizing)
            {
                int newWidth = Math.Max(32, _resizeStartSize.Width + dx);
                int newHeight = Math.Max(32, _resizeStartSize.Height + dy);

                bool isImage = element != null && element.ElementType != "Label";
                bool preserveAspectRatio = isImage && !controlDown;

                if (preserveAspectRatio)
                {
                    double ratio = element!.ImageAspectRatio > 0
                        ? element.ImageAspectRatio
                        : (double)_resizeStartSize.Width / _resizeStartSize.Height;

                    double scaleX = newWidth / (double)_resizeStartSize.Width;
                    double scaleY = newHeight / (double)_resizeStartSize.Height;

                    double scale = Math.Abs(scaleX - 1) >= Math.Abs(scaleY - 1)
                        ? scaleX
                        : scaleY;

                    newWidth = Math.Max(32,
                        (int)Math.Round(_resizeStartSize.Width * scale));

                    newHeight = Math.Max(32,
                        (int)Math.Round(newWidth / ratio));
                }

                _draggedPicture.Size = SnapResize(
                    new Size(newWidth, newHeight),
                    preserveAspectRatio);

                // Durante lo stretch libero aggiorna il rapporto.
                if (shiftDown && isImage)
                {
                    element!.ImageAspectRatio =
                        (double)_draggedPicture.Width / _draggedPicture.Height;
                }

                _draggedPicture.Invalidate();

                // Durante Shift utilizziamo una baseline incrementale.
                if (shiftDown)
                {
                    _resizeStartSize = _draggedPicture.Size;
                    _dragStartMouse = currentMouse;
                }
            }
            else
            {
                Point proposedLocation = new Point(
                    _dragStartLocation.X + dx,
                    _dragStartLocation.Y + dy);

                _draggedPicture.Location = SnapLocation(
                    proposedLocation,
                    _draggedPicture.Size);
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

        private int FindSnapDelta(IEnumerable<int> movingCoordinates, IEnumerable<int> targetCoordinates, int threshold = 7)
        {
            int bestDelta = 0;
            int bestDistance = threshold + 1;

            foreach (int moving in movingCoordinates)
            {
                foreach (int target in targetCoordinates)
                {
                    int delta = target - moving;
                    int distance = Math.Abs(delta);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestDelta = delta;
                    }
                }
            }

            return bestDistance <= threshold ? bestDelta : 0;
        }

        private IEnumerable<PictureBox> GetOtherEditorObjects()
        {
            return panel1.Controls
                .OfType<PictureBox>()
                .Where(p => p != _draggedPicture && p.Tag is InterfaceElement);
        }

        private Point SnapLocation(Point location, Size size)
        {
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                ClearSnapGuides();
                return location;
            }

            Rectangle moving = new Rectangle(location, size);
            var others = GetOtherEditorObjects().ToList();

            SnapResult snapX = FindSnapResult(
                new[] { moving.Left, moving.Left + moving.Width / 2, moving.Right },
                others.SelectMany(p => new[] { p.Left, p.Left + p.Width / 2, p.Right }));

            SnapResult snapY = FindSnapResult(
                new[] { moving.Top, moving.Top + moving.Height / 2, moving.Bottom },
                others.SelectMany(p => new[] { p.Top, p.Top + p.Height / 2, p.Bottom }));

            _verticalSnapGuides.Clear();
            _horizontalSnapGuides.Clear();

            if (snapX.Delta != 0)
                _verticalSnapGuides.Add(snapX.GuidePosition);

            if (snapY.Delta != 0)
                _horizontalSnapGuides.Add(snapY.GuidePosition);

            RefreshSnapGuides();

            return new Point(
                location.X + snapX.Delta,
                location.Y + snapY.Delta);
        }

        private Size SnapResize(Size proposedSize, bool preserveAspectRatio)
        {
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                ClearSnapGuides();
                return proposedSize;
            }

            var others = GetOtherEditorObjects().ToList();

            int proposedRight = _draggedPicture!.Left + proposedSize.Width;
            int proposedBottom = _draggedPicture.Top + proposedSize.Height;

            SnapResult snapX = FindSnapResult(
                new[] { proposedRight },
                others.SelectMany(p => new[]
                {
            p.Left,
            p.Left + p.Width / 2,
            p.Right
                }));

            SnapResult snapY = FindSnapResult(
                new[] { proposedBottom },
                others.SelectMany(p => new[]
                {
            p.Top,
            p.Top + p.Height / 2,
            p.Bottom
                }));

            int dx = snapX.Delta;
            int dy = snapY.Delta;

            if (preserveAspectRatio)
            {
                double ratio = _draggedPicture.Tag is InterfaceElement el &&
                               el.ImageAspectRatio > 0
                    ? el.ImageAspectRatio
                    : (double)_resizeStartSize.Width / _resizeStartSize.Height;

                double scaleX = (proposedSize.Width + dx) /
                                (double)_resizeStartSize.Width;

                double scaleY = (proposedSize.Height + dy) /
                                (double)_resizeStartSize.Height;

                if (dx != 0 && (dy == 0 || Math.Abs(dx) <= Math.Abs(dy)))
                {
                    proposedSize.Width = Math.Max(
                        32,
                        (int)Math.Round(_resizeStartSize.Width * scaleX));

                    proposedSize.Height = Math.Max(
                        32,
                        (int)Math.Round(proposedSize.Width / ratio));
                }
                else if (dy != 0)
                {
                    proposedSize.Height = Math.Max(
                        32,
                        (int)Math.Round(_resizeStartSize.Height * scaleY));

                    proposedSize.Width = Math.Max(
                        32,
                        (int)Math.Round(proposedSize.Height * ratio));
                }
            }
            else
            {
                proposedSize.Width = Math.Max(32, proposedSize.Width + dx);
                proposedSize.Height = Math.Max(32, proposedSize.Height + dy);
            }

            // Aggiorna le coordinate delle guide.
            _verticalSnapGuides.Clear();
            _horizontalSnapGuides.Clear();

            if (dx != 0)
                _verticalSnapGuides.Add(snapX.GuidePosition);

            if (dy != 0)
                _horizontalSnapGuides.Add(snapY.GuidePosition);

            RefreshSnapGuides();

            return proposedSize;
        }

        private void Element_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not PictureBox picture) return;

            // -----------------------------------------
            // GUIDE DI ALLINEAMENTO
            // -----------------------------------------

            using Pen guidePen = new Pen(Color.DodgerBlue, 1.5f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };

            guidePen.DashPattern = new float[] { 4f, 3f };

            // Guide verticali: coordinate del pannello
            foreach (int x in _verticalSnapGuides)
            {
                int localX = x - picture.Left;

                if (localX >= 0 && localX <= picture.Width)
                {
                    e.Graphics.DrawLine(
                        guidePen,
                        localX, 0,
                        localX, picture.Height);
                }
            }

            // Guide orizzontali
            foreach (int y in _horizontalSnapGuides)
            {
                int localY = y - picture.Top;

                if (localY >= 0 && localY <= picture.Height)
                {
                    e.Graphics.DrawLine(
                        guidePen,
                        0, localY,
                        picture.Width, localY);
                }
            }

            // -----------------------------------------
            // LABEL
            // -----------------------------------------

            if (picture.Tag is InterfaceElement element &&
                element.ElementType == "Label")
            {
                string labelText =
                    !string.IsNullOrEmpty(element.StringFormatTemplate)
                        ? element.StringFormatTemplate
                        : element.VariableName;

                float fontSize = CalculateLabelFontSize(
                    labelText,
                    picture.Width,
                    picture.Height);

                using Font font = new Font(
                    "Segoe UI",
                    fontSize,
                    FontStyle.Bold);

                TextRenderer.DrawText(
                    e.Graphics,
                    labelText,
                    font,
                    new Rectangle(
                        2,
                        2,
                        Math.Max(1, picture.Width - 4),
                        Math.Max(1, picture.Height - 4)),
                    Color.Black,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak);
            }

            // -----------------------------------------
            // GRIP DI RIDIMENSIONAMENTO
            // -----------------------------------------

            const int gripSize = 10;

            Rectangle grip = new Rectangle(
                picture.ClientSize.Width - gripSize,
                picture.ClientSize.Height - gripSize,
                gripSize,
                gripSize);

            e.Graphics.FillRectangle(Brushes.DodgerBlue, grip);
            e.Graphics.DrawRectangle(Pens.White, grip);
        }

        private void RefreshSnapGuides()
        {
            panel1.Invalidate();

            foreach (PictureBox picture in panel1.Controls.OfType<PictureBox>())
                picture.Invalidate();
        }

        private static float CalculateLabelFontSize(
    string text, int width, int height)
        {
            float fontSize = Math.Max(6f, height * 0.5f);

            while (fontSize > 4f)
            {
                using (Font testFont = new Font(
                    "Segoe UI", fontSize, FontStyle.Bold))
                {
                    Size textSize = TextRenderer.MeasureText(
                        text ?? string.Empty,
                        testFont);

                    if (textSize.Width <= width &&
                        textSize.Height <= height)
                    {
                        break;
                    }

                    fontSize -= 0.5f;
                }
            }

            if (fontSize < 4f)
                fontSize = 4f;

            return fontSize;
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


        private void Element_MouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_isEditMode || e.Button != MouseButtons.Left || sender is not PictureBox picture) return;
            _draggedPicture = picture;
            _dragStartMouse = panel1.PointToClient(Control.MousePosition);
            _dragStartLocation = picture.Location;
            _resizeStartSize = picture.Size;
            _wasShiftDown = (Control.ModifierKeys & Keys.Shift) != 0;
            _wasControlDown = (Control.ModifierKeys & Keys.Control) != 0;

            const int gripSize = 14;
            Rectangle grip = new Rectangle(picture.ClientSize.Width - gripSize, picture.ClientSize.Height - gripSize, gripSize, gripSize);
            _isResizing = grip.Contains(e.Location);
            picture.BringToFront();
            picture.Capture = true;
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
                // Salva il nuovo aspect ratio dopo il ridimensionamento.
                // Questo diventa il rapporto di riferimento per i resize successivi.
                if (_isResizing &&
                    element.ElementType != "Label" &&
                    _draggedPicture.Height > 0)
                {
                    element.ImageAspectRatio =
                        (double)_draggedPicture.Width / _draggedPicture.Height;
                }

                element.X = _draggedPicture.Left;
                element.Y = _draggedPicture.Top;
                element.Width = _draggedPicture.Width;
                element.Height = _draggedPicture.Height;
            }

            _draggedPicture = null;
            _isResizing = false;

            ClearSnapGuides();
        }

        private void ClearSnapGuides()
        {
            _verticalSnapGuides.Clear();
            _horizontalSnapGuides.Clear();
            RefreshSnapGuides();
        }

        private void Panel1_Paint(object? sender, PaintEventArgs e)
        {
            using Pen guidePen = new Pen(Color.DodgerBlue, 1.5f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };

            guidePen.DashPattern = new float[] { 4f, 3f };

            foreach (int x in _verticalSnapGuides)
            {
                e.Graphics.DrawLine(
                    guidePen,
                    x, 0,
                    x, panel1.ClientSize.Height);
            }

            foreach (int y in _horizontalSnapGuides)
            {
                e.Graphics.DrawLine(
                    guidePen,
                    0, y,
                    panel1.ClientSize.Width, y);
            }
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


        private SnapResult FindSnapResult(
    IEnumerable<int> movingCoordinates,
    IEnumerable<int> targetCoordinates)
        {
            const int snapDistance = 8;

            int bestDelta = 0;
            int bestGuidePosition = 0;
            int minDistance = snapDistance + 1;

            foreach (int moving in movingCoordinates)
            {
                foreach (int target in targetCoordinates)
                {
                    int delta = target - moving;
                    int distance = Math.Abs(delta);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestDelta = delta;
                        bestGuidePosition = target;
                    }
                }
            }

            return new SnapResult(bestDelta, bestGuidePosition);
        }


        private struct SnapResult
        {
            public int Delta;
            public int GuidePosition;

            public SnapResult(int delta, int guidePosition)
            {
                Delta = delta;
                GuidePosition = guidePosition;
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

        public double ImageAspectRatio { get; set; } = 0;

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
