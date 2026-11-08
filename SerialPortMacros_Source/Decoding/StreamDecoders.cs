using System.Text;

namespace SerialPortMacros.Decoding;

/// <summary>Un messaggio estratto dal flusso di byte.</summary>
public readonly record struct DecodedFrame(string Text, double[]? Values);

/// <summary>
/// Trasforma il flusso di byte di una porta in messaggi.
/// Ogni porta ha la sua istanza; <see cref="Feed"/> viene chiamato sempre dallo stesso
/// thread di ricezione (evento seriale o task TCP).
/// </summary>
public interface IStreamDecoder
{
    void Feed(ReadOnlySpan<byte> data, List<DecodedFrame> output);
    void Reset();
}

/// <summary>Modalità classica: testo ASCII separato da \r o \n.</summary>
public sealed class TextLineDecoder : IStreamDecoder
{
    private const int MaxLineLength = 30000;
    private readonly StringBuilder _line = new();

    public void Feed(ReadOnlySpan<byte> data, List<DecodedFrame> output)
    {
        foreach (byte b in data)
        {
            if (b == (byte)'\n' || b == (byte)'\r')
            {
                if (_line.Length == 0) continue;

                string message = _line.ToString().Trim();
                _line.Clear();
                if (message.Length > 0)
                    output.Add(new DecodedFrame(message, null));
            }
            else
            {
                // Come Encoding.ASCII: i byte non ASCII diventano '?'.
                _line.Append(b < 0x80 ? (char)b : '?');

                // Evita che un errore di protocollo riempia la memoria.
                if (_line.Length > MaxLineLength)
                    _line.Clear();
            }
        }
    }

    public void Reset() => _line.Clear();
}

/// <summary>
/// Modalità binaria: frame di lunghezza fissa descritti da <see cref="BinaryFrameFormat"/>,
/// opzionalmente preceduti da un header di sincronizzazione e seguiti da un checksum.
/// </summary>
public sealed class BinaryFrameDecoder : IStreamDecoder
{
    private const int MaxBuffer = 1 << 16;

    private readonly BinaryFrameFormat _format;
    private byte[] _buffer = new byte[1024];
    private int _count;

    public long FramesOk { get; private set; }
    public long FramesBad { get; private set; }

    public BinaryFrameDecoder(BinaryFrameFormat format)
    {
        _format = format;
    }

    public void Reset() => _count = 0;

    public void Feed(ReadOnlySpan<byte> data, List<DecodedFrame> output)
    {
        Append(data);

        int frameLen = _format.FrameBytes;
        byte[] header = _format.Header;
        int pos = 0;

        while (_count - pos >= frameLen)
        {
            if (header.Length > 0)
            {
                int found = IndexOf(_buffer, pos, _count, header);
                if (found < 0)
                {
                    // Tieni solo la coda che potrebbe essere l'inizio di un header.
                    pos = Math.Max(pos, _count - (header.Length - 1));
                    break;
                }
                pos = found;
                if (_count - pos < frameLen)
                    break;
            }

            var frame = new ReadOnlySpan<byte>(_buffer, pos, frameLen);
            var payload = frame.Slice(header.Length, _format.PayloadBytes);

            if (!_format.ChecksumOk(frame))
            {
                FramesBad++;
                // Header falso o dati corrotti: riprova la sincronizzazione dal byte successivo.
                pos += header.Length > 0 ? 1 : frameLen;
                continue;
            }

            double[] values = _format.Decode(payload);
            output.Add(new DecodedFrame(_format.FormatValues(values), values));
            FramesOk++;
            pos += frameLen;
        }

        // Compatta il buffer.
        if (pos > 0)
        {
            Buffer.BlockCopy(_buffer, pos, _buffer, 0, _count - pos);
            _count -= pos;
        }
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        if (_count + data.Length > MaxBuffer)
            _count = 0; // flusso non sincronizzabile: ricomincia

        if (_count + data.Length > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _count + data.Length));

        data.CopyTo(_buffer.AsSpan(_count));
        _count += data.Length;
    }

    private static int IndexOf(byte[] buffer, int start, int end, byte[] pattern)
    {
        int idx = buffer.AsSpan(start, end - start).IndexOf(pattern);
        return idx < 0 ? -1 : start + idx;
    }
}
