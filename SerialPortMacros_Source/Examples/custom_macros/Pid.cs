// PID controller for the system simulated on the ESP32-C3 (Examples/ESP32C3_PlantSim).
//
// Receives the decoded frames:  input.Values = [ n, y, u ]   (u = input seen by the ESP)
// and replies on the same port with "U=<command>\n".
//
// The parameters can be changed live from an interface (Form7) through the
// variables setpoint, Kp, Ki, Kd. The macro also publishes "y", "u" and "e".

public class Pid : ICustomMacro
{
    // ---- Fixed configuration ----
    const double Ts   = 0.01;   // ESP sample period [s]
    const double UMin = -5.0;   // command limits
    const double UMax = 5.0;

    // ---- State (kept in memory between messages) ----
    IMacroContext ctx;
    double integral;
    double prevY;
    int prevN;
    bool first;

    public void Start(IMacroContext context)
    {
        ctx = context;
        integral = 0;
        first = true;

        // Initial values, only if they do not exist yet (so changes made from Form7 are kept).
        ctx.Variables.TryAdd("setpoint", 1.0);
        ctx.Variables.TryAdd("Kp", 1.0);
        ctx.Variables.TryAdd("Ki", 2.0);
        ctx.Variables.TryAdd("Kd", 0.0);

        ctx.Print("PID started");
    }

    public IEnumerable<MacroOutput> Process(MacroInput input)
    {
        if (input.Values.Count < 3)
            yield break;                         // not a frame from the system

        int n = (int)input.Values[0];
        double y = input.Values[1];

        // dt from the ESP counter (more accurate than the USB arrival time).
        // n is a 13-bit wrapping integer: the difference is taken modulo 8192.
        double dt;
        if (first)
        {
            first = false;
            prevN = n;
            prevY = y;
            dt = Ts;
        }
        else
        {
            int steps = ((n - prevN) % 8192 + 8192) % 8192;
            prevN = n;
            if (steps == 0) yield break;         // duplicate sample
            dt = steps * Ts;
        }

        double sp = ctx.Variables.GetOrAdd("setpoint", 1.0);
        double kp = ctx.Variables.GetOrAdd("Kp", 1.0);
        double ki = ctx.Variables.GetOrAdd("Ki", 2.0);
        double kd = ctx.Variables.GetOrAdd("Kd", 0.0);

        double e = sp - y;

        // Derivative on the measurement (avoids a "kick" when the setpoint changes).
        double d = -(y - prevY) / dt;
        prevY = y;

        // Anti-windup: integrate only when the output is not saturated.
        double newIntegral = integral + ki * e * dt;
        double u = kp * e + newIntegral + kd * d;
        if (u >= UMin && u <= UMax)
            integral = newIntegral;
        u = Math.Clamp(u, UMin, UMax);

        ctx.Variables["y"] = y;
        ctx.Variables["u"] = u;
        ctx.Variables["e"] = e;

        yield return MacroOutput.Reply("U=" + u.ToString("0.#####", CultureInfo.InvariantCulture));
    }

    public void Stop()
    {
        ctx?.Print("PID stopped");
    }
}
