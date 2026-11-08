using System.Diagnostics;
using WindowsInput;

namespace SerialPortMacros.Macros;

/// <summary>
/// Keyboard/mouse simulation for "Keypress" macros.
///
///   a          presses the 'a' key         text    types the text
///   *1B        hex virtual-key code (here ESC)
///   +a / -a    key down / up
///   %200%a     holds the key for 200 ms (with a text: repeats it for 200 ms)
///   !LMB !RMB !MMB   mouse click (also with %ms%)
///   ===        uses the text received after "===" as the command
/// </summary>
public static class KeyPressAction
{
    /// <summary>Runs the action on a separate thread (it may contain waits).</summary>
    public static void Run(string spec, string data)
    {
        var thread = new Thread(() =>
        {
            try { Execute(spec, data); }
            catch (Exception ex) { Debug.WriteLine("Keypress: " + ex.Message); }
        })
        {
            IsBackground = true
        };
        thread.Start();
    }

    public static void Execute(string spec, string data)
    {
        int time = 0;
        bool raw = false, press = false, release = false;
        char singleKey = '\0';

        if (spec == "===")
            spec = data.Length > 3 ? data.Substring(3) : "";

        if (string.IsNullOrEmpty(spec))
            return;

        var sim = new InputSimulator();
        VirtualKeyCode key = default;

        if (spec[0] == '*')
        {
            spec = spec.Substring(1);
            raw = true;
        }

        if (spec.Length > 0 && spec[0] == '%')
        {
            int i = 1;
            while (i < spec.Length && spec[i] != '%')
            {
                time = time * 10 + (spec[i] - '0');
                i++;
            }
            spec = i + 1 <= spec.Length ? spec.Substring(i + 1) : "";
        }
        else if (spec.Length > 0 && spec[0] == '+')
        {
            press = true;
            spec = spec.Substring(1);
        }
        else if (spec.Length > 0 && spec[0] == '-')
        {
            release = true;
            spec = spec.Substring(1);
        }

        if (spec.Length == 0)
            return;

        if (spec.Length == 1)
        {
            singleKey = spec[0];
            key = (VirtualKeyCode)spec[0];
        }
        if (raw)
            key = (VirtualKeyCode)Convert.ToInt32(spec, 16);

        if (singleKey != '\0' || raw)
        {
            if (press) sim.Keyboard.KeyDown(key);
            else if (release) sim.Keyboard.KeyUp(key);
            else if (time != 0)
            {
                sim.Keyboard.KeyDown(key);
                Wait(time);
                sim.Keyboard.KeyUp(key);
            }
            else sim.Keyboard.KeyPress(key);
            return;
        }

        if (time == 0)
        {
            if (spec[0] != '!') sim.Keyboard.TextEntry(spec);
            else if (spec == "!LMB") sim.Mouse.LeftButtonClick();
            else if (spec == "!RMB") sim.Mouse.RightButtonClick();
            else if (spec == "!MMB") sim.Mouse.MiddleButtonClick();
            return;
        }

        if (spec[0] != '!')
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < time)
            {
                sim.Keyboard.TextEntry(spec);
                Thread.Sleep(10);
            }
        }
        else if (spec == "!LMB")
        {
            sim.Mouse.LeftButtonDown();
            Wait(time);
            sim.Mouse.LeftButtonUp();
        }
        else if (spec == "!RMB")
        {
            sim.Mouse.RightButtonDown();
            Wait(time);
            sim.Mouse.RightButtonUp();
        }
        else if (spec == "!MMB")
        {
            sim.Mouse.MiddleButtonDown();
            Wait(time);
            sim.Mouse.MiddleButtonUp();
        }
    }

    /// <summary>Precise wait (Thread.Sleep alone has ~15 ms resolution).</summary>
    public static void Wait(int milliseconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < milliseconds)
            Thread.Sleep(1);
    }
}
