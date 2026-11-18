using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxMirroredMargins
{
    public bool Symmetric(PageStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return style.Margins.Left == style.Margins.Right && style.Gutter.Value == 0
               && (style.Borders is not { } borders || borders.Inset(BorderSide.Left).Equals(borders.Inset(BorderSide.Right)));
    }
}
