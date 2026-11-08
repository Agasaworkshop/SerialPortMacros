using SerialPortMacros.Core;

namespace SerialPortMacros
{
    /// <summary>
    /// Una macro salvata nella cartella "scripts". Il formato del file è descritto in
    /// <see cref="Macros.ScriptStore"/>. I nomi dei campi sono quelli originali
    /// (usati anche da Form3).
    /// </summary>
    public class Script
    {
        public const int TriggerKeypress = 1;
        public const int TriggerReply = 2;
        public const int TriggerCustom = 3;

        public string name { get; set; } = "";
        public string key = "";
        public int trigger;

        // Porte che attivano la macro
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

        /// <summary>Custom code: file .cs o .dll nella cartella custom_macros, opzionale ":NomeClasse".</summary>
        public string customFile = "";

        public string path = "";
        public bool active;

        // ---- Stato a runtime (non salvato) ----

        /// <summary>Istanza caricata per le macro custom (null se non caricata o in errore).</summary>
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
