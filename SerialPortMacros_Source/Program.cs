using System.IO.Ports;

namespace SerialPortMacros
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Environment.CurrentDirectory = AppContext.BaseDirectory;

            ApplicationConfiguration.Initialize();

            Form1 mainForm = new Form1();

            mainForm.Shown += (s, e) =>
            {
                if (args.Length > 0 && File.Exists(args[0]) &&
                    Path.GetExtension(args[0]).Equals(".spm", StringComparison.OrdinalIgnoreCase))
                {
                    mainForm.OpenInterface(args[0],false);
                }
            };

            Application.Run(mainForm);
        }
    }
}