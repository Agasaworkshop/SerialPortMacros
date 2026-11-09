using System.Globalization;

namespace SerialPortMacros
{
    /// <summary>
    /// Interface window: a free-form panel of image-based elements (buttons, toggles,
    /// LEDs, numeric inputs/outputs, labels) saved as a .spm file.
    ///
    /// The code is split by area:
    ///   Form7.cs          lifecycle, load/save, rendering of the elements
    ///   Form7.Editor.cs   edit mode: add, drag, resize, snap guides, element configuration
    ///   Form7.Runtime.cs  run mode: button/toggle actions, LED and output refresh, conditions
    /// The document model is in UI/InterfaceDocument.cs.
    /// </summary>
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
        private readonly bool _isNewInterface;
        private bool _wasControlDown;

        // Edit mode: snapping, guides and selection
        private readonly SnapEngine _snap = new();
        private List<SnapGuide> _snapGuides = new();
        private PictureBox? _selectedPicture;

        /// <summary>Module images, read from disk once and shared by the elements.</summary>
        private readonly ModuleImageCache _images = new();

        // ------------------------------------------------------------------
        // Lifecycle and document
        // ------------------------------------------------------------------

        public Form7(Form1? form1, bool isEditMode, bool isNew = false)
        {
            InitializeComponent();
            _form1 = form1;
            panel1.Paint += Panel1_Paint;

            _isNewInterface = isNew;

            panel1.MouseDown += (s, ev) =>
            {
                this.ActiveControl = null;
                SelectPicture(null);   // clicking the empty panel clears the selection
            };

            this.KeyPreview = true;
            this.KeyDown += Form7_KeyDown;
            _isEditMode = isEditMode;

            // Window setup depending on the mode
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

            Disposed += (_, _) => _images.Dispose();
        }

        /// <summary>
        /// Replaces a picture's image. The old image is disposed only if the picture
        /// owned it (shared images from the cache are never disposed here).
        /// </summary>
        private void SetPictureImage(PictureBox picture, Image? image)
        {
            Image? old = picture.Image;
            if (ReferenceEquals(old, image))
                return;

            picture.Image = image;
            if (old != null && !_images.Owns(old))
                old.Dispose();
        }

        private string ImagePath(InterfaceElement element, string imageName) =>
            Path.Combine(ModulePath, element.ModuleName, imageName);

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

            // --- Initialize the panel variables at startup ---
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
                        // Default to 0 (off) if it does not exist yet
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

        private void Save_button_Click(object sender, EventArgs e)
        {
            // Save the current window size
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

            if (_isNewInterface)
            {
                using SaveFileDialog dialog = new SaveFileDialog();
                dialog.InitialDirectory = interfacesPath;
                dialog.Filter = "Serial Port Macro (*.spm)|*.spm";
                dialog.DefaultExt = "spm";
                dialog.AddExtension = true;
                dialog.Title = "Save interface";

                if (!string.IsNullOrEmpty(_currentFilePath)) dialog.FileName = Path.GetFileName(_currentFilePath);
                if (dialog.ShowDialog() != DialogResult.OK) return;

                _document.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
                _currentFilePath = dialog.FileName;
            }

            _document.Name = Path.GetFileNameWithoutExtension(_currentFilePath);
            SpmSerializer.Save(_currentFilePath, _document);
            this.Close();
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void RenderDocument()
        {
            foreach (Control control in panel1.Controls)
            {
                if (control is PictureBox picture)
                    SetPictureImage(picture, null);
                control.Dispose();
            }
            panel1.Controls.Clear();

            foreach (InterfaceElement element in _document.Elements)
            {
                RenderElement(element);
            }
        }

        private void RenderElement(InterfaceElement element)
        {
            Image? imgToUse = null;

            // Labels do not need images.
            bool isLabel = element.ElementType == "Label";

            if (!isLabel)
            {
                imgToUse = _images.Get(ImagePath(element, element.ImageName));

                // Initialize the aspect ratio if it is not set yet.
                if (imgToUse != null && element.ImageAspectRatio <= 0)
                {
                    element.ImageAspectRatio =
                        (double)imgToUse.Width / imgToUse.Height;
                }

                // Fallback image.
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

            // Image rotation. Shared cache images must not be modified: rotate a copy.
            int rotations = (element.Rotation / 90) % 4;
            if (rotations > 0 && imgToUse != null)
            {
                if (_images.Owns(imgToUse))
                    imgToUse = new Bitmap(imgToUse);

                for (int i = 0; i < rotations; i++)
                    imgToUse.RotateFlip(RotateFlipType.Rotate90FlipNone);
            }

            // -----------------------------------------
            // MAIN PICTUREBOX
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

            panel1.Controls.Add(picture);

            // -----------------------------------------
            // EDIT MODE
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

        private static float CalculateLabelFontSize(string text, int width, int height)
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
    }
}
