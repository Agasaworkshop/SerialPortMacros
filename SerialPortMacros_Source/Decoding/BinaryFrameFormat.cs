using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace SerialPortMacros.Decoding;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataMode
{
    /// <summary>Text lines separated by \r / \n (classic behavior).</summary>
    Text,
    /// <summary>Binary frames made of bit fields.</summary>
    Binary
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChecksumKind
{
    None,
    /// <summary>Sum of the payload bytes, modulo 256.</summary>
    Sum8,
    /// <summary>XOR of the payload bytes.</summary>
    Xor8,
    /// <summary>CRC-8 (polynomial 0x07, init 0x00).</summary>
    Crc8,
    /// <summary>CRC-16/MODBUS (reflected poly 0xA001, init 0xFFFF), sent little-endian.</summary>
    Crc16Modbus
}

/// <summary>
/// Data format configuration stored in the settings for each port.
/// </summary>
public sealed class PortDataFormat
{
    public DataMode Mode { get; set; } = DataMode.Text;

    /// <summary>
    /// Fields of one sample (one message), e.g. "a:u5 b:i13 temp:f32 st:u6".
    /// Repeated <see cref="SamplesPerFrame"/> times in each frame.
    /// </summary>
    public string Fields { get; set; } = "";

    /// <summary>
    /// Optional fields sent once per frame, before the samples (e.g. "seq:u16").
    /// Their values are added at the start of every message produced by the frame.
    /// </summary>
    public string FrameFields { get; set; } = "";

    /// <summary>
    /// Samples (messages) contained in each frame: 1 = one message per frame (classic behavior),
    /// N = a block of N consecutive samples, e.g. 128 ADC readings sent together.
    /// </summary>
    public int SamplesPerFrame { get; set; } = 1;

    /// <summary>Sync header in hex, e.g. "AA 55". Empty = none.</summary>
    public string Header { get; set; } = "";

    /// <summary>
    /// true (default) = little-endian / LSB first, like a struct copied from the memory
    /// of an ARM, AVR or ESP32 micro. false = big-endian / MSB first.
    /// </summary>
    public bool LittleEndian { get; set; } = true;

    public ChecksumKind Checksum { get; set; } = ChecksumKind.None;

    public PortDataFormat Clone() => (PortDataFormat)MemberwiseClone();

    /// <summary>Creates the matching decoder. Throws <see cref="FormatException"/> if the spec is invalid.</summary>
    public IStreamDecoder CreateDecoder() =>
        Mode == DataMode.Binary
            ? new BinaryFrameDecoder(BinaryFrameFormat.Parse(this))
            : new TextLineDecoder();
}

public enum FieldKind { Unsigned, Signed, Float, Skip }

public sealed record BinaryField(string Name, FieldKind Kind, int Bits, double Scale, double Offset)
{
    public bool HasName => !string.IsNullOrEmpty(Name);
    public bool IsScaled => Scale != 1.0 || Offset != 0.0;
}

/// <summary>
/// Layout of a binary frame.
///
/// Field syntax (separated by spaces, commas or semicolons):
///   [name:]type[[n]][*scale][+offset]
///
///   u&lt;bits&gt;  unsigned integer, 1..64 bits        e.g. u5, u13, u32
///   i&lt;bits&gt;  signed integer (two's complement), 2..64 bits  (also s&lt;bits&gt;)
///   b        a single bit (0/1)
///   f16 f32 f64   IEEE-754 float (f = f32)
///   x&lt;bits&gt;  bits to skip (padding)
///   [n]      repeats the field n times, e.g. acc:i16[3]
///   *scale   multiplies the value, e.g. temp:i16*0.01
///   +offset  added after scaling, e.g. p:u10*0.1-40
///
/// Example: "a:u5 b:u13 c:f32 d:i6"  -> 56 bits = 7 payload bytes.
///
/// Block frames: the payload can also be
///   [frame fields, once] [sample fields] x SamplesPerFrame
/// as one continuous bit stream. Each sample becomes a separate message whose values are
/// the frame fields followed by that sample's fields, e.g. frame "seq:u16", sample
/// "env:i16 aud:i16", 128 samples -> 128 messages "seq=.. env=.. aud=..".
/// </summary>
public sealed class BinaryFrameFormat
{
    public const int MaxFrameBytes = 4096;
    public const int MaxSamplesPerFrame = 4096;

    /// <summary>Fields of one sample (repeated <see cref="SamplesPerFrame"/> times).</summary>
    public IReadOnlyList<BinaryField> Fields { get; }

    /// <summary>Fields sent once per frame, before the samples (may be empty).</summary>
    public IReadOnlyList<BinaryField> FrameFields { get; }

    public int SamplesPerFrame { get; }
    public byte[] Header { get; }
    public bool LittleEndian { get; }
    public ChecksumKind Checksum { get; }

    public int FrameFieldBits { get; }
    public int SampleBits { get; }
    public int PayloadBits { get; }
    public int PayloadBytes => (PayloadBits + 7) / 8;
    public int ChecksumBytes => Checksum switch
    {
        ChecksumKind.None => 0,
        ChecksumKind.Crc16Modbus => 2,
        _ => 1
    };
    public int FrameBytes => Header.Length + PayloadBytes + ChecksumBytes;

    /// <summary>Values in each message: frame values followed by sample values.</summary>
    public int ValueCount => _frameValueCount + _sampleValueCount;

    private readonly int _frameValueCount;
    private readonly int _sampleValueCount;
    private readonly BinaryField[] _messageFields;   // frame fields + sample fields, for the text

    private BinaryFrameFormat(List<BinaryField> frameFields, List<BinaryField> fields, int samplesPerFrame,
                              byte[] header, bool littleEndian, ChecksumKind checksum)
    {
        FrameFields = frameFields;
        Fields = fields;
        SamplesPerFrame = samplesPerFrame;
        Header = header;
        LittleEndian = littleEndian;
        Checksum = checksum;
        FrameFieldBits = frameFields.Sum(f => f.Bits);
        SampleBits = fields.Sum(f => f.Bits);
        PayloadBits = FrameFieldBits + SampleBits * samplesPerFrame;
        _frameValueCount = frameFields.Count(f => f.Kind != FieldKind.Skip);
        _sampleValueCount = fields.Count(f => f.Kind != FieldKind.Skip);
        _messageFields = frameFields.Concat(fields).ToArray();
    }

    public static BinaryFrameFormat Parse(PortDataFormat cfg) =>
        Parse(cfg.Fields, cfg.Header, cfg.LittleEndian, cfg.Checksum, cfg.FrameFields, cfg.SamplesPerFrame);

    public static BinaryFrameFormat Parse(string fieldSpec, string headerHex, bool littleEndian, ChecksumKind checksum,
                                          string? frameFieldSpec = null, int samplesPerFrame = 1)
    {
        var fields = ParseFields(fieldSpec ?? "");
        if (fields.Count == 0)
            throw new FormatException("No fields defined.");

        List<BinaryField> frameFields;
        try
        {
            frameFields = ParseFields(frameFieldSpec ?? "");
        }
        catch (FormatException ex)
        {
            throw new FormatException("Frame fields: " + ex.Message);
        }

        if (samplesPerFrame < 1 || samplesPerFrame > MaxSamplesPerFrame)
            throw new FormatException($"Samples per frame must be 1..{MaxSamplesPerFrame}.");

        if (fields.Concat(frameFields).All(f => f.Kind == FieldKind.Skip))
            throw new FormatException("The frame contains only padding fields (x).");

        long bits = frameFields.Sum(f => (long)f.Bits) + fields.Sum(f => (long)f.Bits) * samplesPerFrame;
        if (bits > 8L * MaxFrameBytes)
            throw new FormatException($"Frame too long ({(bits + 7) / 8} bytes, max {MaxFrameBytes}).");

        return new BinaryFrameFormat(frameFields, fields, samplesPerFrame, ParseHex(headerHex ?? ""), littleEndian, checksum);
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append($"Frame: {FrameBytes} bytes");
        sb.Append($" (header {Header.Length} + payload {PayloadBytes}");
        if (ChecksumBytes > 0) sb.Append($" + checksum {ChecksumBytes}");
        sb.Append($"), {PayloadBits} data bits");

        if (SamplesPerFrame == 1 && FrameFields.Count == 0)
        {
            sb.Append($", {ValueCount} values");
        }
        else
        {
            sb.Append($"\r\n{SamplesPerFrame} {(SamplesPerFrame == 1 ? "message" : "messages")} per frame, {ValueCount} values each");
            if (_frameValueCount > 0)
                sb.Append($" ({_frameValueCount} frame + {_sampleValueCount} sample)");
        }

        int pad = PayloadBytes * 8 - PayloadBits;
        if (pad > 0) sb.Append($", {pad} padding bits at the end of the payload");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Decoding
    // ------------------------------------------------------------------

    /// <summary>
    /// Decodes one payload into <see cref="SamplesPerFrame"/> messages (added to
    /// <paramref name="messages"/>), each with <see cref="ValueCount"/> values.
    /// </summary>
    public void Decode(ReadOnlySpan<byte> payload, List<double[]> messages)
    {
        // Frame fields are read once and copied in front of every sample.
        var frameValues = new double[_frameValueCount];
        int bitPos = DecodeFields(payload, 0, FrameFields, frameValues, 0);

        for (int s = 0; s < SamplesPerFrame; s++)
        {
            var values = new double[ValueCount];
            frameValues.CopyTo(values, 0);
            bitPos = DecodeFields(payload, bitPos, Fields, values, _frameValueCount);
            messages.Add(values);
        }
    }

    private int DecodeFields(ReadOnlySpan<byte> payload, int bitPos, IReadOnlyList<BinaryField> fields,
                             double[] values, int v)
    {
        foreach (var f in fields)
        {
            if (f.Kind == FieldKind.Skip)
            {
                bitPos += f.Bits;
                continue;
            }

            ulong raw = ReadBits(payload, bitPos, f.Bits, LittleEndian);
            bitPos += f.Bits;

            double value = f.Kind switch
            {
                FieldKind.Unsigned => raw,
                FieldKind.Signed => SignExtend(raw, f.Bits),
                FieldKind.Float => f.Bits switch
                {
                    16 => (double)BitConverter.Int16BitsToHalf((short)raw),
                    32 => BitConverter.Int32BitsToSingle((int)raw),
                    _ => BitConverter.Int64BitsToDouble((long)raw)
                },
                _ => 0
            };

            values[v++] = value * f.Scale + f.Offset;
        }

        return bitPos;
    }

    /// <summary>
    /// Reads <paramref name="count"/> bits starting at <paramref name="bitPos"/>.
    /// Little-endian: the first stream bit is bit 0 of the first byte and the field's LSB.
    /// Big-endian: the first stream bit is bit 7 of the first byte and the field's MSB.
    /// </summary>
    public static ulong ReadBits(ReadOnlySpan<byte> data, int bitPos, int count, bool littleEndian)
    {
        ulong result = 0;
        for (int i = 0; i < count; i++)
        {
            int p = bitPos + i;
            int b = data[p >> 3];
            int bit = littleEndian ? (b >> (p & 7)) & 1 : (b >> (7 - (p & 7))) & 1;

            if (littleEndian)
                result |= (ulong)bit << i;
            else
                result = (result << 1) | (uint)bit;
        }
        return result;
    }

    private static long SignExtend(ulong raw, int bits)
    {
        if (bits >= 64) return (long)raw;
        int shift = 64 - bits;
        return (long)(raw << shift) >> shift;
    }

    public bool ChecksumOk(ReadOnlySpan<byte> frame)
    {
        if (Checksum == ChecksumKind.None) return true;

        var payload = frame.Slice(Header.Length, PayloadBytes);
        var check = frame.Slice(Header.Length + PayloadBytes, ChecksumBytes);

        switch (Checksum)
        {
            case ChecksumKind.Sum8:
            {
                byte s = 0;
                foreach (byte b in payload) s += b;
                return s == check[0];
            }
            case ChecksumKind.Xor8:
            {
                byte x = 0;
                foreach (byte b in payload) x ^= b;
                return x == check[0];
            }
            case ChecksumKind.Crc8:
                return Crc8(payload) == check[0];
            case ChecksumKind.Crc16Modbus:
            {
                ushort crc = Crc16Modbus(payload);
                return (crc & 0xFF) == check[0] && (crc >> 8) == check[1];
            }
        }
        return true;
    }

    public static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
        }
        return crc;
    }

    public static ushort Crc16Modbus(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return crc;
    }

    // ------------------------------------------------------------------
    // Text
    // ------------------------------------------------------------------

    /// <summary>"a=3 b=-120 c=3.14" (or just the values when the fields have no name).</summary>
    public string FormatValues(double[] values)
    {
        var sb = new StringBuilder(values.Length * 10);
        int v = 0;
        foreach (var f in _messageFields)
        {
            if (f.Kind == FieldKind.Skip) continue;

            if (sb.Length > 0) sb.Append(' ');
            if (f.HasName) sb.Append(f.Name).Append('=');
            sb.Append(FormatValue(f, values[v++]));
        }
        return sb.ToString();
    }

    private static string FormatValue(BinaryField f, double value)
    {
        var inv = CultureInfo.InvariantCulture;
        if (f.IsScaled)
            return Math.Round(value, 10).ToString(inv);

        return f.Kind switch
        {
            FieldKind.Float when f.Bits == 32 => ((float)value).ToString(inv),
            FieldKind.Float when f.Bits == 16 => ((Half)value).ToString(null, inv),
            FieldKind.Float => value.ToString(inv),
            _ => value.ToString("0", inv)
        };
    }

    // ------------------------------------------------------------------
    // Spec parsing
    // ------------------------------------------------------------------

    public static List<BinaryField> ParseFields(string spec)
    {
        var fields = new List<BinaryField>();
        var tokens = spec.Split(new[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
            ParseToken(token, fields);

        return fields;
    }

    private static void ParseToken(string token, List<BinaryField> fields)
    {
        string name = "";
        string rest = token;

        int colon = rest.IndexOf(':');
        if (colon >= 0)
        {
            name = rest[..colon];
            rest = rest[(colon + 1)..];
            if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_') ||
                !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new FormatException($"Invalid field name in '{token}'.");
        }

        if (rest.Length == 0)
            throw new FormatException($"Missing type in '{token}'.");

        char t = char.ToLowerInvariant(rest[0]);
        int i = 1;
        while (i < rest.Length && char.IsDigit(rest[i])) i++;
        string bitsText = rest[1..i];
        rest = rest[i..];

        int bits;
        FieldKind kind;
        switch (t)
        {
            case 'u':
                kind = FieldKind.Unsigned;
                bits = RequireBits(bitsText, token, 1, 64);
                break;
            case 'i':
            case 's':
                kind = FieldKind.Signed;
                bits = RequireBits(bitsText, token, 2, 64);
                break;
            case 'b':
                kind = FieldKind.Unsigned;
                bits = bitsText.Length == 0 ? 1 : RequireBits(bitsText, token, 1, 64);
                break;
            case 'x':
                kind = FieldKind.Skip;
                bits = RequireBits(bitsText, token, 1, 8 * 4096);
                break;
            case 'f':
                kind = FieldKind.Float;
                bits = bitsText.Length == 0 ? 32 : RequireBits(bitsText, token, 16, 64);
                if (bits != 16 && bits != 32 && bits != 64)
                    throw new FormatException($"Floats can be f16, f32 or f64 ('{token}').");
                break;
            default:
                throw new FormatException($"Unknown type '{rest}' in '{token}'. Use u, i, b, f, x.");
        }

        int repeat = 1;
        if (rest.StartsWith('['))
        {
            int close = rest.IndexOf(']');
            if (close < 0 || !int.TryParse(rest[1..close], out repeat) || repeat < 1 || repeat > 1024)
                throw new FormatException($"Invalid repeat in '{token}' (use [n]).");
            rest = rest[(close + 1)..];
        }

        double scale = 1.0, offset = 0.0;
        if (rest.StartsWith('*'))
        {
            int end = FindSignAfterNumber(rest, 1);
            scale = ParseNumber(rest[1..end], token);
            rest = rest[end..];
        }
        if (rest.Length > 0)
        {
            if (rest[0] != '+' && rest[0] != '-')
                throw new FormatException($"Unexpected text '{rest}' in '{token}'.");
            offset = ParseNumber(rest, token);
            rest = "";
        }

        if (kind == FieldKind.Skip && (scale != 1.0 || offset != 0.0 || name.Length > 0))
            throw new FormatException($"Padding (x) cannot have a name, scale or offset ('{token}').");

        for (int r = 0; r < repeat; r++)
        {
            string n = repeat > 1 && name.Length > 0 ? $"{name}{r}" : name;
            fields.Add(new BinaryField(n, kind, bits, scale, offset));
        }
    }

    // End of the scale number: the first '+'/'-' that does not follow an exponent 'e'.
    private static int FindSignAfterNumber(string s, int start)
    {
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if ((c == '+' || c == '-') && i > start && s[i - 1] != 'e' && s[i - 1] != 'E')
                return i;
        }
        return s.Length;
    }

    private static int RequireBits(string text, string token, int min, int max)
    {
        if (!int.TryParse(text, out int bits) || bits < min || bits > max)
            throw new FormatException($"Invalid bit count in '{token}' (allowed {min}..{max}).");
        return bits;
    }

    private static double ParseNumber(string text, string token)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            throw new FormatException($"Invalid number '{text}' in '{token}' (use a dot as decimal separator).");
        return v;
    }

    public static byte[] ParseHex(string hex)
    {
        string clean = new string(hex.Where(c => !char.IsWhiteSpace(c) && c != ',' && c != '-').ToArray());
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            clean = clean[2..];
        clean = clean.Replace("0x", "", StringComparison.OrdinalIgnoreCase);

        if (clean.Length % 2 != 0)
            throw new FormatException("The hex header must have an even number of digits (e.g. AA55).");

        try
        {
            return Convert.FromHexString(clean);
        }
        catch (FormatException)
        {
            throw new FormatException($"Invalid hex header: '{hex}'.");
        }
    }
}
