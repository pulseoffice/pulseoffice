using PulseOffice.Sheets.Styling;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.TextFormat;

namespace PulseOffice.Sheets.UI.CellFormat;

public interface ICellCharacterFormat
{
    CharacterFormatRequest Request(CellFont font, string sample);

    CellFont Apply(CellFont font, CharacterFormatState state);
}

public sealed class CellCharacterFormat : ICellCharacterFormat
{
    private const CharacterFeatures CellFeatures =
        CharacterFeatures.Font | CharacterFeatures.Size | CharacterFeatures.Style | CharacterFeatures.Underline
        | CharacterFeatures.DoubleUnderline | CharacterFeatures.Strikethrough | CharacterFeatures.Overline
        | CharacterFeatures.Shadow | CharacterFeatures.Outline | CharacterFeatures.Color | CharacterFeatures.Position;

    private readonly ColorFieldValue _colors = new();

    public CharacterFormatRequest Request(CellFont font, string sample)
    {
        ArgumentNullException.ThrowIfNull(font);
        var state = new CharacterFormatState
        {
            Font = font.Family,
            Size = font.Size,
            Bold = font.Bold,
            Italic = font.Italic,
            Underline = font.DoubleUnderline ? UnderlineKind.Double : font.Underline ? UnderlineKind.Single : UnderlineKind.None,
            Strikethrough = font.Strikethrough,
            Overline = font.Overline,
            Shadow = font.Shadow,
            Outline = font.Outline,
            Color = _colors.Argb(font.ColorHex),
            Baseline = font.Script switch
            {
                CellScript.Superscript => TextBaseline.Superscript,
                CellScript.Subscript => TextBaseline.Subscript,
                _ => TextBaseline.Normal,
            },
        };
        return new CharacterFormatRequest(state, CellFeatures, sample ?? string.Empty);
    }

    public CellFont Apply(CellFont font, CharacterFormatState state)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(state);
        return font with
        {
            Family = state.Font.Length > 0 ? state.Font : font.Family,
            Size = state.Size,
            Bold = state.Bold,
            Italic = state.Italic,
            Underline = state.Underline != UnderlineKind.None,
            DoubleUnderline = state.Underline == UnderlineKind.Double,
            Strikethrough = state.Strikethrough,
            Overline = state.Overline,
            Shadow = state.Shadow,
            Outline = state.Outline,
            ColorHex = state.Color is { } color ? _colors.Hex(color) : CellFont.Default.ColorHex,
            Script = state.Baseline switch
            {
                TextBaseline.Superscript => CellScript.Superscript,
                TextBaseline.Subscript => CellScript.Subscript,
                _ => CellScript.Normal,
            },
        };
    }
}
