namespace PulseOffice.Drawing.Assets;

public interface IQrPenalty
{
    int Score(bool[,] matrix);
}

public sealed class QrPenalty : IQrPenalty
{
    private readonly bool[] _finderBefore = [false, false, false, false, true, false, true, true, true, false, true];
    private readonly bool[] _finderAfter = [true, false, true, true, true, false, true, false, false, false, false];

    public int Score(bool[,] matrix)
    {
        var size = matrix.GetLength(0);
        var score = 0;

        for (var line = 0; line < size; line++)
        {
            var row = line;
            score += Runs(size, i => matrix[i, row]);
            score += Finders(size, i => matrix[i, row]);

            var column = line;
            score += Runs(size, i => matrix[column, i]);
            score += Finders(size, i => matrix[column, i]);
        }

        for (var y = 0; y < size - 1; y++)
            for (var x = 0; x < size - 1; x++)
            {
                var colour = matrix[x, y];
                if (colour == matrix[x + 1, y] && colour == matrix[x, y + 1] && colour == matrix[x + 1, y + 1]) score += 3;
            }

        var dark = 0;
        foreach (var module in matrix) if (module) dark++;

        var total = size * size;
        score += Math.Abs((dark * 20) - (total * 10)) / total * 10;

        return score;
    }

    private int Runs(int size, Func<int, bool> at)
    {
        var score = 0;
        var length = 1;

        for (var i = 1; i <= size; i++)
        {
            if (i < size && at(i) == at(i - 1))
            {
                length++;
                continue;
            }

            if (length >= 5) score += length - 2;
            length = 1;
        }

        return score;
    }

    private int Finders(int size, Func<int, bool> at)
    {
        var score = 0;

        for (var start = 0; start + 11 <= size; start++)
        {
            if (Matches(_finderBefore, start, at)) score += 40;
            if (Matches(_finderAfter, start, at)) score += 40;
        }

        return score;
    }

    private bool Matches(bool[] pattern, int start, Func<int, bool> at)
    {
        for (var i = 0; i < pattern.Length; i++)
            if (at(start + i) != pattern[i]) return false;

        return true;
    }
}
