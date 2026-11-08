namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class MatrixAlgebra
{
    private const double Epsilon = 1e-12;

    public static double Determinant(double[,] matrix)
    {
        var size = matrix.GetLength(0);
        var work = (double[,])matrix.Clone();
        var determinant = 1.0;
        var threshold = Threshold(matrix);

        for (var column = 0; column < size; column++)
        {
            var pivot = FindPivot(work, column, size);
            if (System.Math.Abs(work[pivot, column]) <= threshold) return 0;

            if (pivot != column)
            {
                SwapRows(work, pivot, column, size);

                determinant = -determinant;
            }

            determinant *= work[column, column];
            Eliminate(work, column, size);
        }

        return determinant;
    }

    public static double[,]? Invert(double[,] matrix)
    {
        var size = matrix.GetLength(0);
        var work = (double[,])matrix.Clone();
        var result = Identity(size);
        var threshold = Threshold(matrix);

        for (var column = 0; column < size; column++)
        {
            var pivot = FindPivot(work, column, size);
            if (System.Math.Abs(work[pivot, column]) <= threshold) return null;

            if (pivot != column)
            {
                SwapRows(work, pivot, column, size);
                SwapRows(result, pivot, column, size);
            }

            var divisor = work[column, column];
            for (var k = 0; k < size; k++)
            {
                work[column, k] /= divisor;
                result[column, k] /= divisor;
            }

            for (var row = 0; row < size; row++)
            {
                if (row == column) continue;
                var factor = work[row, column];
                if (factor == 0) continue;

                for (var k = 0; k < size; k++)
                {
                    work[row, k] -= factor * work[column, k];
                    result[row, k] -= factor * result[column, k];
                }
            }
        }

        return result;
    }

    private static double Threshold(double[,] matrix)
    {
        var largest = 0.0;
        foreach (var value in matrix) largest = System.Math.Max(largest, System.Math.Abs(value));
        return largest * Epsilon;
    }

    private static double[,] Identity(int size)
    {
        var result = new double[size, size];
        for (var i = 0; i < size; i++) result[i, i] = 1;
        return result;
    }

    private static int FindPivot(double[,] matrix, int column, int size)
    {
        var best = column;
        for (var row = column + 1; row < size; row++)
            if (System.Math.Abs(matrix[row, column]) > System.Math.Abs(matrix[best, column]))
                best = row;
        return best;
    }

    private static void SwapRows(double[,] matrix, int first, int second, int size)
    {
        for (var k = 0; k < size; k++)
            (matrix[first, k], matrix[second, k]) = (matrix[second, k], matrix[first, k]);
    }

    private static void Eliminate(double[,] matrix, int column, int size)
    {
        for (var row = column + 1; row < size; row++)
        {
            var factor = matrix[row, column] / matrix[column, column];
            for (var k = column; k < size; k++) matrix[row, k] -= factor * matrix[column, k];
        }
    }
}
