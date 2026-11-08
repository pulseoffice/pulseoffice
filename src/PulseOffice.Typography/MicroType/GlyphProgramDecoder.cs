namespace PulseOffice.Typography.MicroType;

internal sealed class GlyphProgramDecoder
{
    private const byte Hop3Code = 0xFB;
    private const byte Hop4Code = 0xFC;
    private const byte PushBytes = 0xB0;
    private const byte PushWords = 0xB8;
    private const byte PushBytesN = 0x40;
    private const byte PushWordsN = 0x41;
    private const int MaxRun = 255;

    public void Write(CtfCursor glyph, CtfFont font, FontBuffer output)
    {
        var lengthAt = output.Length;
        output.WriteUInt16(0);
        var start = output.Length;

        var values = ReadPushData(glyph.Read255UShort(), font.PushData);
        WritePushes(values, output);
        output.WriteBytes(font.Instructions.ReadBytes(glyph.Read255UShort()));

        var length = output.Length - start;
        if (length > ushort.MaxValue) throw new InvalidDataException("Glyph instructions are too long.");
        output.PatchUInt16(lengthAt, (ushort)length);
    }

    private short[] ReadPushData(int count, CtfCursor push)
    {
        var values = new short[count];
        var filled = 0;
        while (filled < count)
        {
            var code = push.PeekByte();
            if (code is Hop3Code or Hop4Code)
            {
                var span = code == Hop3Code ? 3 : 5;
                if (filled < 2 || count - filled < span) throw new InvalidDataException("Glyph push data contains a malformed hop code.");

                push.ReadByte();
                var repeated = values[filled - 2];
                values[filled++] = repeated;
                values[filled++] = push.Read255Short();
                values[filled++] = repeated;
                if (code == Hop4Code)
                {
                    values[filled++] = push.Read255Short();
                    values[filled++] = repeated;
                }
            }
            else
            {
                values[filled++] = push.Read255Short();
            }
        }

        return values;
    }

    private void WritePushes(short[] values, FontBuffer output)
    {
        var index = 0;
        while (index < values.Length)
        {
            var bytes = IsByte(values[index]);
            var run = 1;
            while (index + run < values.Length && run < MaxRun && IsByte(values[index + run]) == bytes) run++;

            if (run < 8) output.WriteByte((byte)((bytes ? PushBytes : PushWords) | (run - 1)));
            else
            {
                output.WriteByte(bytes ? PushBytesN : PushWordsN);
                output.WriteByte((byte)run);
            }

            for (var offset = 0; offset < run; offset++)
            {
                if (bytes) output.WriteByte((byte)values[index + offset]);
                else output.WriteInt16(values[index + offset]);
            }

            index += run;
        }
    }

    private bool IsByte(short value) => value is >= 0 and < 256;
}
