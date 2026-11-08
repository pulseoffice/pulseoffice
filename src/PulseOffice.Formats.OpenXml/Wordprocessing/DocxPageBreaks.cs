using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public enum DocxBreakPlace
{
    None,
    Before,
    After,
}

public enum DocxPendingBreak
{
    None,
    Page,
    Column,
}

public sealed class DocxPageBreaks
{
    public DocxBreakPlace Place(W.Paragraph paragraph, W.BreakValues kind)
    {
        var content = false;
        var place = DocxBreakPlace.None;
        foreach (var element in paragraph.Descendants())
        {
            if (element is W.Break { Type.Value: var type } && type == kind)
            {
                place = content ? DocxBreakPlace.After : DocxBreakPlace.Before;
                continue;
            }
            if (!Content(element)) continue;
            content = true;
            if (place == DocxBreakPlace.After) place = DocxBreakPlace.None;
        }
        return place == DocxBreakPlace.Before && !content ? DocxBreakPlace.After : place;
    }

    private bool Content(OpenXmlElement element) => element switch
    {
        W.Text text => text.Text.Length > 0,
        W.TabChar or W.Drawing or W.EmbeddedObject or W.SymbolChar or W.FieldChar or W.NoBreakHyphen => true,
        W.Break { Type: null } => true,
        _ => false,
    };
}
