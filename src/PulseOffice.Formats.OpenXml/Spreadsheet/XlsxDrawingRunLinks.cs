using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxDrawingRunLinks(DrawingsPart? part) : IDrawingRunLinks
{
    private readonly HyperlinkTargets _targets = new();

    public string? Read(D.HyperlinkOnClick? link) =>
        part is not null && link?.Id?.Value is { Length: > 0 } relationship
            ? _targets.Of(part, relationship)
            : null;

    public D.HyperlinkOnClick? Build(string target) =>
        part is not null && Uri.TryCreate(target, UriKind.RelativeOrAbsolute, out var uri)
            ? new D.HyperlinkOnClick { Id = part.AddHyperlinkRelationship(uri, true).Id }
            : null;
}

internal sealed class XlsxUnreadTextLinks
{
    private readonly OoxmlFontworkText _fontwork = new(new XlsxShapeStyleMapper());

    public bool Present(DocumentFormat.OpenXml.Drawing.Spreadsheet.WorksheetDrawing? drawing) =>
        drawing?.Descendants<D.RunProperties>()
            .Where(run => run.GetFirstChild<D.HyperlinkOnClick>() is not null)
            .Any(run => !Read(run.Ancestors<DocumentFormat.OpenXml.Drawing.Spreadsheet.Shape>().FirstOrDefault())) == true;

    private bool Read(DocumentFormat.OpenXml.Drawing.Spreadsheet.Shape? shape) =>
        shape?.GetFirstChild<DocumentFormat.OpenXml.Drawing.Spreadsheet.ShapeProperties>()?.GetFirstChild<D.PresetGeometry>()?.Preset is not null
        && !_fontwork.Reads(shape.GetFirstChild<DocumentFormat.OpenXml.Drawing.Spreadsheet.TextBody>());
}
