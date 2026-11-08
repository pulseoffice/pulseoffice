namespace PulseOffice.Typography.MicroType;

internal abstract class ByteSink(int capacity)
{
    private byte[] _buffer = new byte[Math.Min(capacity, 64 * 1024)];
    private int _length;

    public abstract void Accept(byte value);

    public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

    protected void Emit(byte value, int count)
    {
        if (count > capacity - _length) throw new InvalidDataException("Decompressed font block exceeds the size limit.");

        var needed = _length + count;
        if (needed > _buffer.Length)
            Array.Resize(ref _buffer, (int)Math.Min(capacity, Math.Max(needed, (long)_buffer.Length * 2)));

        _buffer.AsSpan(_length, count).Fill(value);
        _length = needed;
    }
}

internal sealed class PlainByteSink(int capacity) : ByteSink(capacity)
{
    public override void Accept(byte value) => Emit(value, 1);
}

internal sealed class RunLengthByteSink(int capacity) : ByteSink(capacity)
{
    private RunState _state = RunState.Initial;
    private byte _escape;
    private byte _count;

    public override void Accept(byte value)
    {
        switch (_state)
        {
            case RunState.Initial:
                _escape = value;
                _state = RunState.Normal;
                break;
            case RunState.Normal when value == _escape:
                _state = RunState.SeenEscape;
                break;
            case RunState.Normal:
                Emit(value, 1);
                break;
            case RunState.SeenEscape when value == 0:
                Emit(_escape, 1);
                _state = RunState.Normal;
                break;
            case RunState.SeenEscape:
                _count = value;
                _state = RunState.NeedByte;
                break;
            default:
                Emit(value, _count);
                _state = RunState.Normal;
                break;
        }
    }

    private enum RunState
    {
        Initial,
        Normal,
        SeenEscape,
        NeedByte
    }
}
