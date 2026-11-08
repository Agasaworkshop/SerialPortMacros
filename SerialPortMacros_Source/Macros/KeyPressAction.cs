using System.Diagnostics;
using WindowsInput;

namespace SerialPortMacros.Macros;

/// <summary>
/// Simulazione di tastiera/mouse per le macro "Keypress".
///
///   a          preme il tasto 'a'          testo   scrive il testo
///   *1B        codice virtual-key esadecimale (qui ESC)
///   +a / -a    tasto giù / su
///   %200%a     tiene premuto per 200 ms (con un testo: lo ripete per 200 ms)
///   !LMB !RMB !MMB   click del mouse (anche con %ms%)
///   ===        usa come comando il testo ricevuto dopo "==="
/// </summary>
public static class KeyPressAction
{
    /// <summary>Esegue l'azione su un thread separato (può contenere attese).</summary>
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

    public static void Execute(string car, string data)
    {
        int time = 0;
        bool raw = false, press = false, release = false;
        char singleKey = '\0';

        if (car == "===")
            car = data.Length > 3 ? data.Substring(3) : "";

        if (string.IsNullOrEmpty(car))
            return;

        var sim = new InputSimulator();
        VirtualKeyCode key = default;

        if (car[0] == '*')
        {
            car = car.Substring(1);
            raw = true;
        }

        if (car.Length > 0 && car[0] == '%')
        {
            int i = 1;
            while (i < car.Length && car[i] != '%')
            {
                time = time * 10 + (car[i] - '0');
                i++;
            }
            car = i + 1 <= car.Length ? car.Substring(i + 1) : "";
        }
        else if (car.Length > 0 && car[0] == '+')
        {
            press = true;
            car = car.Substring(1);
        }
        else if (car.Length > 0 && car[0] == '-')
        {
            release = true;
            car = car.Substring(1);
        }

        if (car.Length == 0)
            return;

        if (car.Length == 1)
        {
            singleKey = car[0];
            key = (VirtualKeyCode)car[0];
        }
        if (raw)
            key = (VirtualKeyCode)Convert.ToInt32(car, 16);

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
            if (car[0] != '!') sim.Keyboard.TextEntry(car);
            else if (car == "!LMB") sim.Mouse.LeftButtonClick();
            else if (car == "!RMB") sim.Mouse.RightButtonClick();
            else if (car == "!MMB") sim.Mouse.MiddleButtonClick();
            return;
        }

        if (car[0] != '!')
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < time)
            {
                sim.Keyboard.TextEntry(car);
                Thread.Sleep(10);
            }
        }
        else if (car == "!LMB")
        {
            sim.Mouse.LeftButtonDown();
            Wait(time);
            sim.Mouse.LeftButtonUp();
        }
        else if (car == "!RMB")
        {
            sim.Mouse.RightButtonDown();
            Wait(time);
            sim.Mouse.RightButtonUp();
        }
        else if (car == "!MMB")
        {
            sim.Mouse.MiddleButtonDown();
            Wait(time);
            sim.Mouse.MiddleButtonUp();
        }
    }

    /// <summary>Attesa precisa (Thread.Sleep da solo ha risoluzione ~15 ms).</summary>
    public static void Wait(int milliseconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < milliseconds)
            Thread.Sleep(1);
    }
}
