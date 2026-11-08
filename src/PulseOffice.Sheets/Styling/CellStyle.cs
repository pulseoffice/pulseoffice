using PulseOffice.Foundation.Units;

namespace PulseOffice.Sheets.Styling;

public enum CellHorizontalAlignment { General, Left, Center, Right, Justify, Fill }

public enum CellVerticalAlignment { Top, Middle, Bottom }

public sealed record BorderEdge(BorderStyle Style, string ColorHex, Twip Width)
{
    public static readonly BorderEdge None = new(BorderStyle.None, "#000000", Twip.Zero);

    public bool IsVisible => Style != BorderStyle.None && Width.Value > 0;
}

public enum BorderStyle { None, Thin, Medium, Thick, Dashed, Dotted, Double }

public sealed record CellBorders(BorderEdge Left, BorderEdge Top, BorderEdge Right, BorderEdge Bottom)
{
    public static readonly CellBorders None =
        new(BorderEdge.None, BorderEdge.None, BorderEdge.None, BorderEdge.None);

    public BorderEdge DiagonalUp { get; init; } = BorderEdge.None;

    public BorderEdge DiagonalDown { get; init; } = BorderEdge.None;

    public bool HasDiagonal => DiagonalUp.IsVisible || DiagonalDown.IsVisible;

    public bool IsEmpty =>
        !Left.IsVisible && !Top.IsVisible && !Right.IsVisible && !Bottom.IsVisible && !HasDiagonal;
}

public enum CellScript { Normal, Superscript, Subscript }

public sealed record CellFont
{
    public string Family { get; init; } = "Calibri";

    public double Size { get; init; } = 10;

    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool DoubleUnderline { get; init; }
    public bool Overline { get; init; }
    public bool Strikethrough { get; init; }
    public bool Shadow { get; init; }
    public bool Outline { get; init; }

    public CellScript Script { get; init; } = CellScript.Normal;

    public string ColorHex { get; init; } = "#000000";

    public static readonly CellFont Default = new();
}

public sealed record CellStyle
{
    public CellFont Font { get; init; } = CellFont.Default;

    public string? BackgroundHex { get; init; }

    public CellBorders Borders { get; init; } = CellBorders.None;

    public CellHorizontalAlignment Horizontal { get; init; } = CellHorizontalAlignment.General;
    public CellVerticalAlignment Vertical { get; init; } = CellVerticalAlignment.Bottom;

    public bool WrapText { get; init; }

    public int RotationDegrees { get; init; }

    public int Indent { get; init; }

    public int NumberFormatId { get; init; }

    public bool Locked { get; init; } = true;

    public string? Language { get; init; }

    public static readonly CellStyle Default = new();

    public bool IsDefault => Equals(Default);
}
