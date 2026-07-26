using System.Text;

namespace LibSixel;

/// <summary>
/// Callback invoked by <see cref="SixelOutput"/> when buffered data is ready to be written.
/// </summary>
/// <param name="data">The chunk of encoded bytes to write.</param>
public delegate void WriteDelegate(ReadOnlySpan<byte> data);

/// <summary>
/// Buffered output context for SIXEL encoding.
/// Accumulates encoded bytes and calls the user-supplied <see cref="WriteDelegate"/>
/// whenever the internal 32 KB buffer is full or <see cref="Flush"/> is called.
/// </summary>
public sealed class SixelOutput : IDisposable
{
    private const int BufferSize = 32768;

    private readonly WriteDelegate _writer;
    private readonly byte[] _buffer;
    private int _pos;
    private bool _disposed;

    private SixelOutput(WriteDelegate writer)
    {
        _writer = writer;
        _buffer = new byte[BufferSize];
        _pos = 0;
    }

    /// <summary>
    /// Creates a new <see cref="SixelOutput"/> that sends data to <paramref name="writer"/>.
    /// </summary>
    public static SixelOutput Create(WriteDelegate writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return new SixelOutput(writer);
    }

    /// <summary>
    /// When <see langword="true"/>, wrap the DCS string with the GNU Screen pass-through
    /// sequence so that SIXEL graphics reach the terminal from inside a multiplexer.
    /// </summary>
    public bool PenetrateMultiplexer { get; set; }

    /// <summary>
    /// When <see langword="true"/>, use 8-bit C1 control codes (0x9B / 0x9C) instead of
    /// the 7-bit ESC sequences (ESC P … ESC \).
    /// </summary>
    public bool Has8BitControls { get; set; }

    /// <summary>
    /// When <see langword="true"/>, limit repeat counts to 255 (VT240 compatibility).
    /// </summary>
    public bool HasGriArgLimit { get; set; }

    /// <summary>
    /// When <see langword="true"/>, skip the DCS header and string terminator so that
    /// only the sixel body is emitted.
    /// </summary>
    public bool BodyOnly { get; set; }

    // -----------------------------------------------------------------------
    // Internal write helpers
    // -----------------------------------------------------------------------

    /// <summary>Writes a single byte into the output buffer.</summary>
    internal void WriteByte(byte b)
    {
        if (_pos == BufferSize) FlushBuffer();
        _buffer[_pos++] = b;
    }

    /// <summary>Writes a span of bytes into the output buffer.</summary>
    internal void WriteBytes(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            int available = BufferSize - _pos;
            int toCopy = Math.Min(available, data.Length - offset);
            data.Slice(offset, toCopy).CopyTo(_buffer.AsSpan(_pos));
            _pos += toCopy;
            offset += toCopy;
            if (_pos == BufferSize) FlushBuffer();
        }
    }

    /// <summary>
    /// Writes a repeat sequence <c>!count char</c> when <paramref name="count"/> &gt; 3,
    /// otherwise writes the byte <paramref name="pixel"/> <paramref name="count"/> times.
    /// </summary>
    internal void WriteRepeat(byte pixel, int count)
    {
        if (HasGriArgLimit && count > 255) count = 255;

        if (count > 3)
        {
            WriteByte((byte)'!');
            WriteNumber(count);
            WriteByte(pixel);
        }
        else
        {
            for (int i = 0; i < count; i++)
                WriteByte(pixel);
        }
    }

    /// <summary>Writes a non-negative integer as ASCII decimal digits.</summary>
    internal void WriteNumber(int value)
    {
        // Fast path for small numbers
        Span<byte> tmp = stackalloc byte[10];
        int len = 0;
        if (value == 0)
        {
            WriteByte((byte)'0');
            return;
        }
        int v = value;
        while (v > 0)
        {
            tmp[len++] = (byte)('0' + v % 10);
            v /= 10;
        }
        // tmp holds digits in reverse order
        for (int i = len - 1; i >= 0; i--)
            WriteByte(tmp[i]);
    }

    // -----------------------------------------------------------------------
    // Public flush
    // -----------------------------------------------------------------------

    /// <summary>Flushes any remaining buffered bytes to the writer.</summary>
    public void Flush()
    {
        if (_pos > 0) FlushBuffer();
    }

    private void FlushBuffer()
    {
        if (_pos > 0)
        {
            _writer(_buffer.AsSpan(0, _pos));
            _pos = 0;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            Flush();
            _disposed = true;
        }
    }
}
