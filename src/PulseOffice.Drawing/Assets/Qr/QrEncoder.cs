using System.Globalization;
using System.Text;

namespace PulseOffice.Drawing.Assets;

public interface IQrEncoder
{
    bool Fits(string content, QrErrorCorrection level = QrErrorCorrection.Medium);

    bool[,] Encode(string content, QrErrorCorrection level = QrErrorCorrection.Medium);

    string ToSvg(bool[,] matrix, int quiet);
}

public sealed class QrEncoder(IQrVersionTable versions, IQrReedSolomon reedSolomon, IQrPenalty penalty) : IQrEncoder
{
    public QrEncoder()
        : this(new QrVersionTable(), new QrReedSolomon(), new QrPenalty())
    {
    }

    public bool Fits(string content, QrErrorCorrection level = QrErrorCorrection.Medium)
    {
        ArgumentNullException.ThrowIfNull(content);
        return versions.Smallest(Encoding.UTF8.GetByteCount(content), level) is not null;
    }

    public QrVersion VersionFor(string content, QrErrorCorrection level) =>
        versions.Smallest(Encoding.UTF8.GetByteCount(content), level)
        ?? throw new ArgumentException("The content does not fit in a QR code at this error correction level.", nameof(content));

    public bool[,] Encode(string content, QrErrorCorrection level = QrErrorCorrection.Medium)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = Encoding.UTF8.GetBytes(content);
        var version = VersionFor(content, level);
        var codewords = Interleave(version, Payload(version, bytes));

        var layout = new QrLayout(version);
        layout.PlaceData(codewords);

        bool[,]? best = null;
        var bestScore = int.MaxValue;

        foreach (var mask in QrMask.All)
        {
            var candidate = layout.Render(mask);
            var score = penalty.Score(candidate);

            if (score >= bestScore) continue;

            best = candidate;
            bestScore = score;
        }

        return best!;
    }

    public string ToSvg(bool[,] matrix, int quiet)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        var size = matrix.GetLength(0);
        var side = size + (quiet * 2);
        var builder = new StringBuilder();

        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {side} {side}\">");
        builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"{side}\" height=\"{side}\" fill=\"#FFFFFF\"/>");

        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (!matrix[x, y]) continue;
                builder.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{x + quiet}\" y=\"{y + quiet}\" width=\"1\" height=\"1\" fill=\"#000000\"/>");
            }

        builder.Append("</svg>");
        return builder.ToString();
    }

    public byte[] Payload(QrVersion version, byte[] bytes)
    {
        var bits = new QrBitBuffer();
        var capacity = version.DataCodewords * 8;

        bits.Append(0b0100, 4);
        bits.Append(bytes.Length, version.CountBits);
        foreach (var value in bytes) bits.Append(value, 8);

        bits.Append(0, Math.Min(4, capacity - bits.Count));
        bits.Append(0, (8 - (bits.Count % 8)) % 8);

        for (var pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11)
            bits.Append(pad, 8);

        return bits.ToBytes();
    }

    public byte[] Interleave(QrVersion version, byte[] data)
    {
        var shortBlocks = version.Blocks - (version.RawCodewords % version.Blocks);
        var shortLength = (version.RawCodewords / version.Blocks) - version.CorrectionPerBlock;

        var dataBlocks = new List<byte[]>();
        var correctionBlocks = new List<byte[]>();
        var offset = 0;

        for (var block = 0; block < version.Blocks; block++)
        {
            var length = shortLength + (block < shortBlocks ? 0 : 1);
            var slice = data.AsSpan(offset, length).ToArray();
            offset += length;

            dataBlocks.Add(slice);
            correctionBlocks.Add(reedSolomon.Remainder(slice, version.CorrectionPerBlock));
        }

        var result = new List<byte>(version.RawCodewords);

        for (var i = 0; i <= shortLength; i++)
            foreach (var block in dataBlocks)
                if (i < block.Length) result.Add(block[i]);

        for (var i = 0; i < version.CorrectionPerBlock; i++)
            foreach (var block in correctionBlocks)
                result.Add(block[i]);

        return [.. result];
    }
}
