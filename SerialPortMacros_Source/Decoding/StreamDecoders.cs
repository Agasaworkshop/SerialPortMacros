using System.Text;

namespace SerialPortMacros.Decoding;

/// <summary>A message extracted from the byte stream.</summary>
public readonly record struct DecodedFrame(string Text, double[]? Values);

/// <summary>
/// Turns the byte stream of a port into messages.
/// Each port has its own instance; <see cref="Feed"/> is always called from the same
/// receive thread (serial event or TCP task).
/// </summary>
public interface IStreamDecoder
{
    void Feed(ReadOnlySpan<byte> data, List<DecodedFrame> output);
    void Reset();
}

/// <summary>Classic mode: ASCII text separated by \r or \n.</summary>
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
                // Same as Encoding.ASCII: non-ASCII bytes become '?'.
                _line.Append(b < 0x80 ? (char)b : '?');

                // Prevents a protocol error from filling memory.
                if (_line.Length > MaxLineLength)
                    _line.Clear();
            }
        }
    }

    public void Reset() => _line.Clear();
}

/// <summary>
/// Binary mode: fixed-length frames described by <see cref="BinaryFrameFormat"/>,
/// optionally preceded by a sync header and followed by a checksum.
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
                    // Keep only the tail that could be the start of a header.
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
                // False header or corrupted data: resync from the next byte.
                pos += header.Length > 0 ? 1 : frameLen;
                continue;
            }

            double[] values = _format.Decode(payload);
            output.Add(new DecodedFrame(_format.FormatValues(values), values));
            FramesOk++;
            pos += frameLen;
        }

        // Compact the buffer.
        if (pos > 0)
        {
            Buffer.BlockCopy(_buffer, pos, _buffer, 0, _count - pos);
            _count -= pos;
        }
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        if (_count + data.Length > MaxBuffer)
            _count = 0; // stream cannot be synchronized: start over

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
