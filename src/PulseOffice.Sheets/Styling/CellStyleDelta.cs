namespace PulseOffice.Sheets.Styling;

public interface ICellStyleDelta
{
    CellStyle Apply(CellStyle before, CellStyle after, CellStyle target);
}

public sealed class CellStyleDelta : ICellStyleDelta
{
    public CellStyle Apply(CellStyle before, CellStyle after, CellStyle target)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(target);

        if (before == after) return target;

        return target with
        {
            Font = Font(before.Font, after.Font, target.Font),
            BackgroundHex = Pick(before.BackgroundHex, after.BackgroundHex, target.BackgroundHex),
            Borders = new CellBorders(
                Pick(before.Borders.Left, after.Borders.Left, target.Borders.Left),
                Pick(before.Borders.Top, after.Borders.Top, target.Borders.Top),
                Pick(before.Borders.Right, after.Borders.Right, target.Borders.Right),
                Pick(before.Borders.Bottom, after.Borders.Bottom, target.Borders.Bottom))
            {
                DiagonalUp = Pick(before.Borders.DiagonalUp, after.Borders.DiagonalUp, target.Borders.DiagonalUp),
                DiagonalDown = Pick(before.Borders.DiagonalDown, after.Borders.DiagonalDown, target.Borders.DiagonalDown),
            },
            Horizontal = Pick(before.Horizontal, after.Horizontal, target.Horizontal),
            Vertical = Pick(before.Vertical, after.Vertical, target.Vertical),
            WrapText = Pick(before.WrapText, after.WrapText, target.WrapText),
            RotationDegrees = Pick(before.RotationDegrees, after.RotationDegrees, target.RotationDegrees),
            Indent = Pick(before.Indent, after.Indent, target.Indent),
            NumberFormatId = Pick(before.NumberFormatId, after.NumberFormatId, target.NumberFormatId),
            Locked = Pick(before.Locked, after.Locked, target.Locked),
            Language = Pick(before.Language, after.Language, target.Language),
        };
    }

    private CellFont Font(CellFont before, CellFont after, CellFont target) =>
        before == after ? target : target with
        {
            Family = Pick(before.Family, after.Family, target.Family),
            Size = Pick(before.Size, after.Size, target.Size),
            Bold = Pick(before.Bold, after.Bold, target.Bold),
            Italic = Pick(before.Italic, after.Italic, target.Italic),
            Underline = Pick(before.Underline, after.Underline, target.Underline),
            DoubleUnderline = Pick(before.DoubleUnderline, after.DoubleUnderline, target.DoubleUnderline),
            Overline = Pick(before.Overline, after.Overline, target.Overline),
            Strikethrough = Pick(before.Strikethrough, after.Strikethrough, target.Strikethrough),
            Shadow = Pick(before.Shadow, after.Shadow, target.Shadow),
            Outline = Pick(before.Outline, after.Outline, target.Outline),
            Script = Pick(before.Script, after.Script, target.Script),
            ColorHex = Pick(before.ColorHex, after.ColorHex, target.ColorHex),
        };

    private T Pick<T>(T before, T after, T target) => EqualityComparer<T>.Default.Equals(before, after) ? target : after;
}
