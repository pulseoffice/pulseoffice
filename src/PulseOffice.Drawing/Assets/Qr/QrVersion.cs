namespace PulseOffice.Drawing.Assets;

public sealed record QrVersion(int Number, QrErrorCorrection Level, int CorrectionPerBlock, int Blocks)
{
    public int Size => (Number * 4) + 17;

    public int RawCodewords
    {
        get
        {
            var modules = (((16 * Number) + 128) * Number) + 64;

            if (Number >= 2)
            {
                var alignments = (Number / 7) + 2;
                modules -= (((25 * alignments) - 10) * alignments) - 55;
                if (Number >= 7) modules -= 36;
            }

            return modules / 8;
        }
    }

    public int DataCodewords => RawCodewords - (CorrectionPerBlock * Blocks);

    public int CountBits => Number <= 9 ? 8 : 16;

    public int ByteCapacity => ((DataCodewords * 8) - 4 - CountBits) / 8;

    public IReadOnlyList<int> AlignmentCentres
    {
        get
        {
            if (Number == 1) return [];

            var count = (Number / 7) + 2;
            var step = ((Number * 8) + (count * 3) + 5) / ((count * 4) - 4) * 2;
            var centres = new int[count];
            centres[0] = 6;

            for (int i = count - 1, position = Size - 7; i >= 1; i--, position -= step)
                centres[i] = position;

            return centres;
        }
    }
}
