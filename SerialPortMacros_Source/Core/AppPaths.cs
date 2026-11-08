namespace SerialPortMacros.Core;

/// <summary>
/// Cartelle dell'applicazione (tutte relative alla cartella dell'eseguibile).
/// Le cartelle vengono create al primo accesso.
/// </summary>
public static class AppPaths
{
    public static string BaseDir => AppContext.BaseDirectory;

    public static string SettingsFile => Path.Combine(BaseDir, "settings.json");

    public static string Logs => Ensure("logs");
    public static string Scripts => Ensure("scripts");
    public static string Sequences => Ensure("sequences");
    public static string Interfaces => Ensure("interfaces");
    public static string CustomMacros => Ensure("custom_macros");

    public static string IconOpen => Path.Combine(BaseDir, "icon_open.ico");
    public static string IconClosed => Path.Combine(BaseDir, "icon_closed.ico");

    private static string Ensure(string name)
    {
        string path = Path.Combine(BaseDir, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
