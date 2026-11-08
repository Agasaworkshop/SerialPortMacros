using System.Globalization;

namespace SerialPortMacros
{
    // Run mode: button/toggle actions, periodic refresh of LEDs and outputs, conditions.
    public partial class Form7
    {
        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        // Button: visual feedback (darkened image) while pressed, line sent on release.
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
                    System.Diagnostics.Debug.WriteLine($"Error loading the button image {imagePath}: {ex.Message}");
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

            // Pass the element's custom line ending too
            _form1.SendCustomMacroLine(element.TargetPorts, formattedMessage, element.CustomLineEnding);
        }

        // ------------------------------------------------------------------
        // Toggles
        // ------------------------------------------------------------------

        // Toggle switch
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
                    System.Diagnostics.Debug.WriteLine($"Error loading the toggle image {imagePath}: {ex.Message}");
                }

                string msgToSend = element.IsToggled ? element.StringFormatTemplate : element.MessageOnToOff;
                _form1.SendCustomMacroLine(element.TargetPorts, msgToSend);
            }
        }

        // ------------------------------------------------------------------
        // Refresh, conditions and variable substitution
        // ------------------------------------------------------------------

        // Timer that refreshes the output fields and the LEDs
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
                            System.Diagnostics.Debug.WriteLine($"Error loading the LED image {imagePath}: {ex.Message}");
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
                // LED without a condition: by default it is on when the variable is > 0
                if (element.ElementType == "Led")
                {
                    double val = 0;
                    if (!string.IsNullOrEmpty(element.VariableName) && _form1.PanelVariables.ContainsKey(element.VariableName))
                        val = _form1.PanelVariables[element.VariableName];
                    return val > 0;
                }
                return true; // For buttons, no condition means sending is always allowed
            }

            // Comparison operators
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

            // The condition is a single boolean/numeric variable (e.g. {main_toggle})
            if (double.TryParse(expr, NumberStyles.Float, CultureInfo.InvariantCulture, out double singleVal))
            {
                return singleVal > 0;
            }

            return true;
        }

        private string FormatMessageWithVariablesAndSerial(string template)
        {
            if (_form1 == null) return template;
            string result = template;

            // 1. Replace the panel variables (e.g. {my_variable})
            foreach (var kvp in _form1.PanelVariables)
            {
                result = result.Replace("{" + kvp.Key + "}", kvp.Value.ToString(CultureInfo.InvariantCulture));
            }

            // 2. Replace the full serial values (e.g. {port1})
            foreach (var kvp in _form1.LatestSerialValues)
            {
                result = result.Replace("{" + kvp.Key + "}", kvp.Value);
            }

            // 3. Replace the {1P1} syntax (logical port 1, first number -> index 0)
            var regex = new System.Text.RegularExpressions.Regex(@"\{([1-4])P([0-9]+)\}");
            result = regex.Replace(result, match =>
            {
                int portIdx = int.Parse(match.Groups[1].Value);
                int numIdx = int.Parse(match.Groups[2].Value) - 1; // 1-based (1P1 = first number = index 0)

                if (_form1.LatestPortNumbers.TryGetValue(portIdx, out var listNums) && numIdx >= 0 && numIdx < listNums.Count)
                {
                    return listNums[numIdx].ToString(CultureInfo.InvariantCulture);
                }
                return "0";
            });

            return result;
        }
    }
}
