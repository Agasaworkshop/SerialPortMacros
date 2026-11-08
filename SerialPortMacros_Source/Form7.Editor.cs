namespace SerialPortMacros
{
    // Edit mode: adding elements, drag & resize with snap guides, element configuration.
    public partial class Form7
    {
        // ------------------------------------------------------------------
        // Adding and configuring elements
        // ------------------------------------------------------------------

        private void Form7_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!_isEditMode || _draggedPicture == null) return;
            if (e.KeyCode == Keys.R)
            {
                _rotationAngle = (_rotationAngle + 90) % 360;
                if (_draggedPicture.Image != null)
                {
                    // Rotate a copy: the current image may be shared by other elements.
                    var rotated = new Bitmap(_draggedPicture.Image);
                    rotated.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    SetPictureImage(_draggedPicture, rotated);
                }
                _draggedPicture.Refresh();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void add_button_Click(object sender, EventArgs e)
        {
            if (moduleBox == null || moduleBox.SelectedItem == null)
            {
                MessageBox.Show("Select a module first.");
                return;
            }

            string moduleName = moduleBox.SelectedItem.ToString()!;
            string moduleDirectory = Path.Combine(ModulePath, moduleName);

            if (!Directory.Exists(moduleDirectory))
            {
                MessageBox.Show("The module folder does not exist.");
                return;
            }

            // Pick the element's first PNG directly
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = moduleDirectory;
            dialog.Filter = "PNG images (*.png)|*.png";
            dialog.Title = "Select an image";

            if (dialog.ShowDialog() != DialogResult.OK) return;

            string selectedPath = Path.GetFullPath(dialog.FileName);
            string expectedDirectory = Path.GetFullPath(moduleDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!selectedPath.StartsWith(expectedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Select an image from the module folder.");
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
                System.Diagnostics.Debug.WriteLine("Cannot read the image size: " + ex.Message);
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

        // Opens Form8 to edit the element's parameters.
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

                form8.lineEndingCombo.SelectedItem = string.IsNullOrEmpty(element.CustomLineEnding) ? "Inherit from Form1" : element.CustomLineEnding;

                DialogResult result = form8.ShowDialog();
                if (result == DialogResult.Abort)
                {
                    // Removes only this element, identified by its unique Id
                    _document.Elements.RemoveAll(el => el.Id == element.Id);
                    RenderDocument();
                    return;
                }

                if (result == DialogResult.OK)
                {
                    string selectedEnding = form8.lineEndingCombo.SelectedItem?.ToString() ?? "";
                    element.CustomLineEnding = (selectedEnding == "Inherit from Form1") ? "" : selectedEnding;
                    element.ElementType = form8.comboBox1.SelectedItem?.ToString() ?? "Button";
                    element.VariableName = form8.textBox2.Text;
                    element.SecondImageName = form8.textBox1.Text;
                    element.StringFormatTemplate = form8.textBox4.Text;
                    element.MessageOnToOff = form8.textBox3.Text;
                    element.ConditionExpression = form8.textBox5.Text;
                    element.TargetPorts = new bool[]
                    {
                        form8.checkBox1.Checked,
                        form8.checkBox2.Checked,
                        form8.checkBox3.Checked,
                        form8.checkBox4.Checked
                    };
                    RenderDocument();
                }
            }
        }

        // ------------------------------------------------------------------
        // Drag and resize
        // ------------------------------------------------------------------

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

        private void Element_MouseMove(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!_isEditMode || _draggedPicture == null) return;

            Point currentMouse = panel1.PointToClient(Control.MousePosition);

            InterfaceElement? element = _draggedPicture.Tag as InterfaceElement;
            bool controlDown = (Control.ModifierKeys & Keys.Control) != 0;
            bool shiftDown = (Control.ModifierKeys & Keys.Shift) != 0;

            // Detect a change of the Control key state.
            // Taking a new baseline avoids jumps in the size.
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

                // While stretching freely, update the ratio.
                if (shiftDown && isImage)
                {
                    element!.ImageAspectRatio =
                        (double)_draggedPicture.Width / _draggedPicture.Height;
                }

                _draggedPicture.Invalidate();

                // While Shift is held, use an incremental baseline.
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

        private void Element_MouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_draggedPicture == null) return;

            _draggedPicture.Capture = false;

            if (_draggedPicture.Tag is InterfaceElement element)
            {
                // Store the new aspect ratio after resizing.
                // It becomes the reference ratio for the next resizes.
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

        // ------------------------------------------------------------------
        // Snapping
        // ------------------------------------------------------------------

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
                others.SelectMany(p => new[] { p.Left, p.Left + p.Width / 2, p.Right }));

            SnapResult snapY = FindSnapResult(
                new[] { proposedBottom },
                others.SelectMany(p => new[] { p.Top, p.Top + p.Height / 2, p.Bottom }));

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

            // Update the guide coordinates.
            _verticalSnapGuides.Clear();
            _horizontalSnapGuides.Clear();

            if (dx != 0)
                _verticalSnapGuides.Add(snapX.GuidePosition);

            if (dy != 0)
                _horizontalSnapGuides.Add(snapY.GuidePosition);

            RefreshSnapGuides();

            return proposedSize;
        }

        private SnapResult FindSnapResult(IEnumerable<int> movingCoordinates, IEnumerable<int> targetCoordinates)
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

        // ------------------------------------------------------------------
        // Snap guides and painting
        // ------------------------------------------------------------------

        private void RefreshSnapGuides()
        {
            panel1.Invalidate();

            foreach (PictureBox picture in panel1.Controls.OfType<PictureBox>())
                picture.Invalidate();
        }

        private void ClearSnapGuides()
        {
            _verticalSnapGuides.Clear();
            _horizontalSnapGuides.Clear();
            RefreshSnapGuides();
        }

        private void Element_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not PictureBox picture) return;

            // -----------------------------------------
            // ALIGNMENT GUIDES
            // -----------------------------------------

            using Pen guidePen = new Pen(Color.DodgerBlue, 1.5f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };

            guidePen.DashPattern = new float[] { 4f, 3f };

            // Vertical guides: panel coordinates
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

            // Horizontal guides
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
            // RESIZE GRIP
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
    }
}
