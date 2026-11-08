using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Sheets.Styling;

public interface IStyleTransplant
{
    CellStyle Carry(CellStyle style, NumberFormatCatalog source, NumberFormatCatalog target);

    CellStyle WithFormat(CellStyle style, string? code, NumberFormatCatalog target);
}

public sealed class StyleTransplant : IStyleTransplant
{
    public CellStyle Carry(CellStyle style, NumberFormatCatalog source, NumberFormatCatalog target)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(source, target) || style.NumberFormatId < NumberFormatCatalog.FirstCustomId) return style;
        return style with { NumberFormatId = target.Register(source.CodeOf(style.NumberFormatId)) };
    }

    public CellStyle WithFormat(CellStyle style, string? code, NumberFormatCatalog target)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(target);
        return string.IsNullOrWhiteSpace(code) ? style : style with { NumberFormatId = target.Register(code) };
    }
}
