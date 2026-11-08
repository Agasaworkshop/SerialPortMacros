using SerialPortMacros.Core;

namespace SerialPortMacros
{
    /// <summary>What a macro does when it is triggered. The value is stored in the macro file.</summary>
    public enum MacroEffect
    {
        None = 0,
        Keypress = 1,
        Reply = 2,
        Custom = 3
    }

    /// <summary>
    /// A macro saved in the "scripts" folder. The file format is described in
    /// <see cref="Macros.ScriptStore"/>.
    /// </summary>
    public class Script
    {
        /// <summary>File name, shown in the macro list.</summary>
        public string Name { get; set; } = "";

        /// <summary>Full path of the macro file.</summary>
        public string FilePath { get; set; } = "";

        public bool Active { get; set; }

        /// <summary>Text that triggers the macro ("*" = any message, "===" = lines starting with ===).</summary>
        public string Key { get; set; } = "";

        public MacroEffect Effect { get; set; }

        // Ports whose messages trigger the macro
        public bool ListenPort1 { get; set; }
        public bool ListenPort2 { get; set; }
        public bool ListenPort3 { get; set; }
        public bool ListenPort4 { get; set; }

        // Write back
        public string Reply { get; set; } = "";
        public bool ReplyPort1 { get; set; }
        public bool ReplyPort2 { get; set; }
        public bool ReplyPort3 { get; set; }
        public bool ReplyPort4 { get; set; }
        public int LineEndingIndex { get; set; } = 1; // default NL

        // Keypress
        public string KeyPress { get; set; } = "";

        /// <summary>Custom code: .cs or .dll file in the custom_macros folder, optional ":ClassName".</summary>
        public string CustomFile { get; set; } = "";

        // ---- Runtime state (not saved) ----

        /// <summary>Loaded instance for custom macros (null if not loaded or failed).</summary>
        internal Macros.ICustomMacro? CustomInstance;
        internal string? LoadError;
        internal bool Started;
        internal long LastErrorTick;

        public bool ListensTo(int logicalPort) => logicalPort switch
        {
            1 => ListenPort1,
            2 => ListenPort2,
            3 => ListenPort3,
            4 => ListenPort4,
            _ => false
        };

        public bool[] ReplyTargets => new[] { ReplyPort1, ReplyPort2, ReplyPort3, ReplyPort4 };

        public bool Matches(string data)
        {
            string k = Key ?? "";
            return k == "*" || (k == "===" && data.StartsWith("===")) || data.Contains(k);
        }

        public string LineEnding => LineEndings.FromIndex(LineEndingIndex, "\n");

        public override string ToString() => Name;
    }
}
