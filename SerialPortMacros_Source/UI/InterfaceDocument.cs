using System.Text.Json;

namespace SerialPortMacros
{
    /// <summary>One element of an interface. Property names are the .spm (JSON) keys: do not rename them.</summary>
    public class InterfaceElement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ModuleName { get; set; } = "";
        public string ImageName { get; set; } = "";         // First PNG (chosen when the element is added)
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Rotation { get; set; } = 0;

        // Configuration fields, mapped to the Form8 controls
        public string ElementType { get; set; } = "Button"; // comboBox1
        public string VariableName { get; set; } = "";        // textBox2
        public string SecondImageName { get; set; } = "";     // textBox1 (second image for LED/toggle)
        public string StringFormatTemplate { get; set; } = ""; // textBox4 (button message / off->on)
        public string MessageOnToOff { get; set; } = "";      // textBox3 (on->off)

        public string MessageOffToOn { get; set; } = "";      // textBox3 (off->on)

        public bool[] TargetPorts { get; set; } = { true, false, false, false }; // checkBox1, 2, 3, 4
        public string ConditionExpression { get; set; } = ""; // textBox5 (LED / button condition)

        public bool IsToggled { get; set; } = false;
        public double NumericValue { get; set; } = 0;

        public double ImageAspectRatio { get; set; } = 0;

        public string CustomLineEnding { get; set; } = ""; // Form8 line ending (empty = inherit from the main window)

    }

    /// <summary>An interface (.spm file).</summary>
    public class InterfaceDocument
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public int Width { get; set; } = 800;   // Saved width
        public int Height { get; set; } = 600;  // Saved height
        public List<InterfaceElement> Elements { get; set; } = new();
    }

    /// <summary>Reads and writes .spm files (indented JSON).</summary>
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
            return JsonSerializer.Deserialize<InterfaceDocument>(json) ?? throw new InvalidDataException("The SPM file does not contain a valid document.");
        }
    }
}
