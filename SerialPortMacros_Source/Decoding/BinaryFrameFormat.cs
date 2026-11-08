using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace SerialPortMacros.Decoding;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataMode
{
    /// <summary>Righe di testo separate da \r / \n (comportamento classico).</summary>
    Text,
    /// <summary>Frame binari a campi di bit.</summary>
    Binary
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChecksumKind
{
    None,
    /// <summary>Somma dei byte del payload, modulo 256.</summary>
    Sum8,
    /// <summary>XOR dei byte del payload.</summary>
    Xor8,
    /// <summary>CRC-8 (polinomio 0x07, init 0x00).</summary>
    Crc8,
    /// <summary>CRC-16/MODBUS (poly 0xA001 riflesso, init 0xFFFF), trasmesso little-endian.</summary>
    Crc16Modbus
}

/// <summary>
/// Configurazione salvata nelle impostazioni per ogni porta.
/// </summary>
public sealed class PortDataFormat
{
    public DataMode Mode { get; set; } = DataMode.Text;

    /// <summary>Elenco dei campi, es. "a:u5 b:i13 temp:f32 st:u6".</summary>
    public string Fields { get; set; } = "";

    /// <summary>Header di sincronizzazione in esadecimale, es. "AA 55". Vuoto = nessuno.</summary>
    public string Header { get; set; } = "";

    /// <summary>
    /// true (default) = little-endian / LSB first: come una struct copiata in memoria
    /// da un micro ARM, AVR, ESP32. false = big-endian / MSB first.
    /// </summary>
    public bool LittleEndian { get; set; } = true;

    public ChecksumKind Checksum { get; set; } = ChecksumKind.None;

    public PortDataFormat Clone() => (PortDataFormat)MemberwiseClone();

    /// <summary>Crea il decoder adatto. Lancia <see cref="FormatException"/> se la specifica non è valida.</summary>
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
/// Formato di un frame binario.
///
/// Sintassi dei campi (separati da spazio, virgola o punto e virgola):
///   [nome:]tipo[[n]][*scala][+offset]
///
///   u&lt;bit&gt;   intero senza segno, 1..64 bit     es. u5, u13, u32
///   i&lt;bit&gt;   intero con segno (complemento a 2), 2..64 bit  (anche s&lt;bit&gt;)
///   b        un bit (0/1)
///   f16 f32 f64   float IEEE-754 (f = f32)
///   x&lt;bit&gt;   bit da saltare (padding)
///   [n]      ripete il campo n volte, es. acc:i16[3]
///   *scala   moltiplica il valore, es. temp:i16*0.01
///   +offset  somma dopo la scala, es. p:u10*0.1-40
///
/// Esempio: "a:u5 b:u13 c:f32 d:i6"  -> 56 bit = 7 byte di payload.
/// </summary>
public sealed class BinaryFrameFormat
{
    public IReadOnlyList<BinaryField> Fields { get; }
    public byte[] Header { get; }
    public bool LittleEndian { get; }
    public ChecksumKind Checksum { get; }

    public int PayloadBits { get; }
    public int PayloadBytes => (PayloadBits + 7) / 8;
    public int ChecksumBytes => Checksum switch
    {
        ChecksumKind.None => 0,
        ChecksumKind.Crc16Modbus => 2,
        _ => 1
    };
    public int FrameBytes => Header.Length + PayloadBytes + ChecksumBytes;
    public int ValueCount { get; }

    private BinaryFrameFormat(List<BinaryField> fields, byte[] header, bool littleEndian, ChecksumKind checksum)
    {
        Fields = fields;
        Header = header;
        LittleEndian = littleEndian;
        Checksum = checksum;
        PayloadBits = fields.Sum(f => f.Bits);
        ValueCount = fields.Count(f => f.Kind != FieldKind.Skip);
    }

    public static BinaryFrameFormat Parse(PortDataFormat cfg) =>
        Parse(cfg.Fields, cfg.Header, cfg.LittleEndian, cfg.Checksum);

    public static BinaryFrameFormat Parse(string fieldSpec, string headerHex, bool littleEndian, ChecksumKind checksum)
    {
        var fields = ParseFields(fieldSpec ?? "");
        if (fields.Count == 0)
            throw new FormatException("Nessun campo definito.");
        if (fields.All(f => f.Kind == FieldKind.Skip))
            throw new FormatException("Il frame contiene solo campi di padding (x).");

        int bits = fields.Sum(f => f.Bits);
        if (bits > 8 * 4096)
            throw new FormatException("Frame troppo lungo (max 4096 byte).");

        return new BinaryFrameFormat(fields, ParseHex(headerHex ?? ""), littleEndian, checksum);
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append($"Frame: {FrameBytes} byte");
        sb.Append($" (header {Header.Length} + payload {PayloadBytes}");
        if (ChecksumBytes > 0) sb.Append($" + checksum {ChecksumBytes}");
        sb.Append($"), {PayloadBits} bit di dati, {ValueCount} valori");
        int pad = PayloadBytes * 8 - PayloadBits;
        if (pad > 0) sb.Append($", {pad} bit di riempimento a fine payload");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Decodifica
    // ------------------------------------------------------------------

    public double[] Decode(ReadOnlySpan<byte> payload)
    {
        var values = new double[ValueCount];
        int bitPos = 0;
        int v = 0;

        foreach (var f in Fields)
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

        return values;
    }

    /// <summary>
    /// Legge <paramref name="count"/> bit a partire da <paramref name="bitPos"/>.
    /// Little-endian: il primo bit del flusso è il bit 0 del primo byte ed è l'LSB del campo.
    /// Big-endian: il primo bit del flusso è il bit 7 del primo byte ed è l'MSB del campo.
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
    // Testo
    // ------------------------------------------------------------------

    /// <summary>"a=3 b=-120 c=3.14" (oppure solo i valori se i campi non hanno nome).</summary>
    public string FormatValues(double[] values)
    {
        var sb = new StringBuilder(values.Length * 10);
        int v = 0;
        foreach (var f in Fields)
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
    // Parsing della specifica
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
                throw new FormatException($"Nome di campo non valido in '{token}'.");
        }

        if (rest.Length == 0)
            throw new FormatException($"Tipo mancante in '{token}'.");

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
                    throw new FormatException($"I float possono essere f16, f32 o f64 ('{token}').");
                break;
            default:
                throw new FormatException($"Tipo '{rest}' sconosciuto in '{token}'. Usa u, i, b, f, x.");
        }

        int repeat = 1;
        if (rest.StartsWith('['))
        {
            int close = rest.IndexOf(']');
            if (close < 0 || !int.TryParse(rest[1..close], out repeat) || repeat < 1 || repeat > 1024)
                throw new FormatException($"Ripetizione non valida in '{token}' (usa [n]).");
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
                throw new FormatException($"Testo inatteso '{rest}' in '{token}'.");
            offset = ParseNumber(rest, token);
            rest = "";
        }

        if (kind == FieldKind.Skip && (scale != 1.0 || offset != 0.0 || name.Length > 0))
            throw new FormatException($"Il padding x non può avere nome, scala o offset ('{token}').");

        for (int r = 0; r < repeat; r++)
        {
            string n = repeat > 1 && name.Length > 0 ? $"{name}{r}" : name;
            fields.Add(new BinaryField(n, kind, bits, scale, offset));
        }
    }

    // Fine del numero della scala: il primo '+'/'-' che non segue una 'e' di esponente.
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
            throw new FormatException($"Numero di bit non valido in '{token}' (consentito {min}..{max}).");
        return bits;
    }

    private static double ParseNumber(string text, string token)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            throw new FormatException($"Numero '{text}' non valido in '{token}' (usa il punto decimale).");
        return v;
    }

    public static byte[] ParseHex(string hex)
    {
        string clean = new string(hex.Where(c => !char.IsWhiteSpace(c) && c != ',' && c != '-').ToArray());
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            clean = clean[2..];
        clean = clean.Replace("0x", "", StringComparison.OrdinalIgnoreCase);

        if (clean.Length % 2 != 0)
            throw new FormatException("L'header esadecimale deve avere un numero pari di cifre (es. AA55).");

        try
        {
            return Convert.FromHexString(clean);
        }
        catch (FormatException)
        {
            throw new FormatException($"Header esadecimale non valido: '{hex}'.");
        }
    }
}
