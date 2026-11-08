namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public static class ResultLimits
{
    public const int MaximumTextLength = 32_767;

    public const long MaximumArrayCells = 16_777_216;

    public static bool TextFits(long length) => length <= MaximumTextLength;

    public static bool ArrayFits(long rows, long columns) => rows * columns <= MaximumArrayCells;
}
