namespace PulseOffice.Drawing.Assets;

public sealed class QrLayout
{
    private readonly QrVersion _version;
    private readonly int _size;
    private readonly bool[,] _modules;
    private readonly bool[,] _reserved;

    public QrLayout(QrVersion version)
    {
        _version = version;
        _size = version.Size;
        _modules = new bool[_size, _size];
        _reserved = new bool[_size, _size];

        PlaceTiming();
        PlaceFinders();
        PlaceAlignments();
        ReserveFormat();
        PlaceVersion();
    }

    public void PlaceData(byte[] codewords)
    {
        var bit = 0;

        for (var right = _size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;

            for (var step = 0; step < _size; step++)
                for (var column = 0; column < 2; column++)
                {
                    var x = right - column;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? _size - 1 - step : step;

                    if (_reserved[x, y]) continue;
                    if (bit < codewords.Length * 8)
                        _modules[x, y] = ((codewords[bit / 8] >> (7 - (bit % 8))) & 1) == 1;
                    bit++;
                }
        }
    }

    public bool[,] Render(QrMask mask)
    {
        var result = (bool[,])_modules.Clone();

        for (var y = 0; y < _size; y++)
            for (var x = 0; x < _size; x++)
                if (!_reserved[x, y] && mask.Inverts(x, y)) result[x, y] = !result[x, y];

        PlaceFormat(result, FormatBits(_version.Level, mask.Number));
        return result;
    }

    public int FormatBits(QrErrorCorrection level, int mask)
    {
        var levelBits = level switch
        {
            QrErrorCorrection.Low => 1,
            QrErrorCorrection.Medium => 0,
            QrErrorCorrection.Quartile => 3,
            _ => 2,
        };

        var data = (levelBits << 3) | mask;
        var remainder = data;
        for (var i = 0; i < 10; i++) remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);

        return ((data << 10) | remainder) ^ 0x5412;
    }

    public int VersionBits(int number)
    {
        var remainder = number;
        for (var i = 0; i < 12; i++) remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);

        return (number << 12) | remainder;
    }

    private void Set(int x, int y, bool dark)
    {
        _modules[x, y] = dark;
        _reserved[x, y] = true;
    }

    private void PlaceTiming()
    {
        for (var i = 0; i < _size; i++)
        {
            Set(6, i, i % 2 == 0);
            Set(i, 6, i % 2 == 0);
        }
    }

    private void PlaceFinders()
    {
        foreach (var (centreX, centreY) in new[] { (3, 3), (_size - 4, 3), (3, _size - 4) })
            for (var dy = -4; dy <= 4; dy++)
                for (var dx = -4; dx <= 4; dx++)
                {
                    var x = centreX + dx;
                    var y = centreY + dy;
                    if (x < 0 || y < 0 || x >= _size || y >= _size) continue;

                    var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    Set(x, y, distance != 2 && distance != 4);
                }
    }

    private void PlaceAlignments()
    {
        var centres = _version.AlignmentCentres;
        var last = centres.Count - 1;

        for (var i = 0; i < centres.Count; i++)
            for (var j = 0; j < centres.Count; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == last) || (i == last && j == 0)) continue;

                for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++)
                        Set(centres[i] + dx, centres[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
    }

    private void ReserveFormat()
    {
        for (var i = 0; i < 9; i++)
        {
            _reserved[i, 8] = true;
            _reserved[8, i] = true;
        }

        for (var i = 0; i < 8; i++)
        {
            _reserved[_size - 1 - i, 8] = true;
            _reserved[8, _size - 1 - i] = true;
        }

        Set(8, _size - 8, true);
    }

    private void PlaceVersion()
    {
        if (_version.Number < 7) return;

        var bits = VersionBits(_version.Number);

        for (var i = 0; i < 18; i++)
        {
            var dark = ((bits >> i) & 1) == 1;
            var a = _size - 11 + (i % 3);
            var b = i / 3;
            Set(a, b, dark);
            Set(b, a, dark);
        }
    }

    private void PlaceFormat(bool[,] matrix, int bits)
    {
        bool Bit(int i) => ((bits >> i) & 1) == 1;

        for (var i = 0; i <= 5; i++) matrix[8, i] = Bit(i);
        matrix[8, 7] = Bit(6);
        matrix[8, 8] = Bit(7);
        matrix[7, 8] = Bit(8);
        for (var i = 9; i < 15; i++) matrix[14 - i, 8] = Bit(i);

        for (var i = 0; i < 8; i++) matrix[_size - 1 - i, 8] = Bit(i);
        for (var i = 8; i < 15; i++) matrix[8, _size - 15 + i] = Bit(i);
        matrix[8, _size - 8] = true;
    }
}
