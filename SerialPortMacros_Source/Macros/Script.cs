using SerialPortMacros.Core;

namespace SerialPortMacros
{
    /// <summary>
    /// A macro saved in the "scripts" folder. The file format is described in
    /// <see cref="Macros.ScriptStore"/>. Field names are the original ones
    /// (also used by Form3).
    /// </summary>
    public class Script
    {
        public const int TriggerKeypress = 1;
        public const int TriggerReply = 2;
        public const int TriggerCustom = 3;

        public string name { get; set; } = "";
        public string key = "";
        public int trigger;

        // Ports that trigger the macro
        public bool p1;
        public bool p2;
        public bool p3;
        public bool p4;

        // Write back
        public string reply = "";
        public bool p1_w;
        public bool p2_w;
        public bool p3_w;
        public bool p4_w;
        public int lineEndingIndex { get; set; } = 1; // default NL

        // Keypress
        public string keypress = "";

        /// <summary>Custom code: .cs or .dll file in the custom_macros folder, optional ":ClassName".</summary>
        public string customFile = "";

        public string path = "";
        public bool active;

        // ---- Runtime state (not saved) ----

        /// <summary>Loaded instance for custom macros (null if not loaded or failed).</summary>
        internal Macros.ICustomMacro? CustomInstance;
        internal string? LoadError;
        internal bool Started;
        internal long LastErrorTick;

        public bool ListensTo(int logicalPort) => logicalPort switch
        {
            1 => p1,
            2 => p2,
            3 => p3,
            4 => p4,
            _ => false
        };

        public bool[] ReplyTargets => new[] { p1_w, p2_w, p3_w, p4_w };

        public bool Matches(string data)
        {
            string k = key ?? "";
            return k == "*" || (k == "===" && data.StartsWith("===")) || data.Contains(k);
        }

        public string LineEnding => LineEndings.FromIndex(lineEndingIndex, "\n");

        public override string ToString() => name;
    }
}
