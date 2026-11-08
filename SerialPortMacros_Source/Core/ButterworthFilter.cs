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

    /// <summary>Second-order (biquad) low-pass section. Currently not used.</summary>
    private static double[] ApplyLowPassBiquad(double[] input, double sampleRate, double cutoff, double Q)
    {
        double omega = 2.0 * Math.PI * cutoff / sampleRate;

        double sinOmega = Math.Sin(omega);
        double cosOmega = Math.Cos(omega);

        double alpha = sinOmega / (2.0 * Q);

        double b0 = (1.0 - cosOmega) / 2.0;
        double b1 = 1.0 - cosOmega;
        double b2 = (1.0 - cosOmega) / 2.0;
        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cosOmega;
        double a2 = 1.0 - alpha;

        // Normalization
        b0 /= a0;
        b1 /= a0;
        b2 /= a0;
        a1 /= a0;
        a2 /= a0;

        double[] output = new double[input.Length];

        double x1 = input[0];
        double x2 = input[0];
        double y1 = input[0];
        double y2 = input[0];

        for (int i = 0; i < input.Length; i++)
        {
            double x0 = input[i];
            double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

            output[i] = y0;

            x2 = x1;
            x1 = x0;
            y2 = y1;
            y1 = y0;
        }

        return output;
    }
}
