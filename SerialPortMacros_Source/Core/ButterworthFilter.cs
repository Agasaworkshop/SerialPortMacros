namespace SerialPortMacros.Core;

/// <summary>
/// Low-pass filter used by the log viewer.
/// Note: despite the name, <see cref="LowPass"/> is a cascade of first-order sections
/// tuned so the whole cascade is exactly -3 dB at the cutoff frequency.
/// </summary>
public static class ButterworthFilter
{
    public static double[] LowPass(double[] input, double sampleRate, double cutoff, int order = 4)
    {
        if (input == null || input.Length == 0)
            return Array.Empty<double>();

        if (cutoff <= 0)
            return (double[])input.Clone();

        if (cutoff >= sampleRate / 2.0)
            return (double[])input.Clone();

        // Implemented as a cascade of sections.
        if (order < 1)
            order = 1;

        // Coefficient "a" chosen so that the cascade of 'order' first-order sections
        // is exactly -3 dB at the 'cutoff' frequency.
        double omega = 2.0 * Math.PI * cutoff / sampleRate;
        double r = Math.Pow(0.5, 1.0 / order);
        double u = (1.0 / r - 1.0) / (2.0 * (1.0 - Math.Cos(omega)));
        double a = ((2.0 * u + 1.0) - Math.Sqrt(4.0 * u + 1.0)) / (2.0 * u);

        double[] output = (double[])input.Clone();

        for (int s = 0; s < order; s++)
        {
            double y = output[0];   // start in steady state on the first sample

            for (int i = 0; i < output.Length; i++)
            {
                y = a * y + (1.0 - a) * output[i];
                output[i] = y;
            }
        }

        return output;
    }
}
