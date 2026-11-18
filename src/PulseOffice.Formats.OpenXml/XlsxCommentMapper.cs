using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxCommentMapper(XlsxVmlDocuments documents)
{
    private readonly XlsxThreadedComments _threads = new(new OoxmlTextEscape());
    private readonly CommentBoxGeometry _geometry = new();

    public void Write(WorksheetPart part, Sheet sheet, int sheetIndex)
    {
        if (sheet.Comments.Count == 0) return;

        var ordered = Ordered(sheet);
        string LegacyAuthor(CellComment comment, CellAddress address) =>
            comment.IsThread ? XlsxThreadedComments.LegacyAuthorPrefix + _threads.ThreadId(sheetIndex, address) : comment.Author ?? string.Empty;
        string LegacyText(CellComment comment) => comment.IsThread ? _threads.LegacyText(comment) : comment.Text;

        var authors = ordered.Select(pair => LegacyAuthor(pair.Value, pair.Key)).Distinct(StringComparer.Ordinal).ToList();
        var authorIds = authors.Select((author, index) => (author, index)).ToDictionary(pair => pair.author, pair => (uint)pair.index, StringComparer.Ordinal);

        var commentsPart = part.AddNewPart<WorksheetCommentsPart>();
        commentsPart.Comments = new Comments(
            new Authors(authors.Select(author => new Author(author))),
            new CommentList(ordered.Select(pair => new Comment(
                new CommentText(new Run(new DocumentFormat.OpenXml.Spreadsheet.Text(new OoxmlTextEscape().Encode(LegacyText(pair.Value))) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve })))
            {
                Reference = pair.Key.ToA1(),
                AuthorId = authorIds[LegacyAuthor(pair.Value, pair.Key)],
            })));
        commentsPart.Comments.Save();
        _threads.Write(part, sheet, sheetIndex);
    }

    public void Read(WorksheetPart part, Sheet sheet)
    {
        if (part.WorksheetCommentsPart?.Comments is not { } comments) return;

        var authors = comments.Authors?.Elements<Author>().Select(author => author.Text).ToList() ?? [];
        var notes = Notes(part, sheet);
        foreach (var comment in comments.CommentList?.Elements<Comment>() ?? [])
        {
            if (comment.Reference?.Value is not { } reference || !CellAddress.TryParseA1(reference, out var address)) continue;

            var index = comment.AuthorId?.Value ?? 0;
            var author = index < (uint)authors.Count && !string.IsNullOrWhiteSpace(authors[(int)index]) ? authors[(int)index] : null;
            if (author?.StartsWith(XlsxThreadedComments.LegacyAuthorPrefix, StringComparison.Ordinal) == true) author = null;
            var note = notes.GetValueOrDefault(address);
            sheet.Comments[address] = new CellComment
            {
                Text = WithoutAuthorHeading(Text(comment.CommentText), author),
                Author = author,
                IsAlwaysVisible = note.Visible,
                Box = note.Box,
            };
        }
    }

    private Dictionary<CellAddress, (bool Visible, CommentBox? Box)> Notes(WorksheetPart part, Sheet sheet)
    {
        var excel = XNamespace.Get("urn:schemas-microsoft-com:office:excel");
        var result = new Dictionary<CellAddress, (bool Visible, CommentBox? Box)>();

        foreach (var document in documents.Load(part))
        {
            foreach (var data in document.Descendants(excel + "ClientData"))
            {
                if (data.Attribute("ObjectType")?.Value != "Note") continue;
                if (int.TryParse(data.Element(excel + "Row")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)
                    && int.TryParse(data.Element(excel + "Column")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var column)
                    && row >= 0 && column >= 0 && row < CellAddress.MaxRows && column < CellAddress.MaxColumns)
                {
                    var address = new CellAddress(row, column);
                    var box = Anchor(data.Element(excel + "Anchor")?.Value) is { } anchor
                        ? _geometry.Relative(sheet.Layout, address, _geometry.FromAnchor(sheet.Layout, anchor))
                        : null;
                    result[address] = (data.Element(excel + "Visible") is not null, box);
                }
            }
        }

        return result;
    }

    private NoteAnchor? Anchor(string? text)
    {
        if (text is null) return null;
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 8) return null;
        var values = new int[8];
        for (var index = 0; index < 8; index++)
            if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[index]) || values[index] < 0) return null;
        if (values[0] >= CellAddress.MaxColumns || values[4] >= CellAddress.MaxColumns || values[2] >= CellAddress.MaxRows || values[6] >= CellAddress.MaxRows) return null;
        return new NoteAnchor(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]);
    }

    private string WithoutAuthorHeading(string text, string? author)
    {
        if (author is null) return text;
        var heading = author + ":";
        if (!text.StartsWith(heading, StringComparison.Ordinal)) return text;
        var rest = text[heading.Length..];
        return rest.StartsWith('\n') ? rest[1..] : rest.StartsWith("\r\n", StringComparison.Ordinal) ? rest[2..] : text;
    }

    private string Text(CommentText? text) =>
        text is null
            ? string.Empty
            : new OoxmlTextEscape().Decode(text.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Text>()?.Text ?? string.Concat(text.Elements<Run>().Select(run => run.Text?.Text ?? string.Empty)));

    public IEnumerable<XElement> Shapes(Sheet sheet, XlsxVmlShapes shapes)
    {
        var ordered = Ordered(sheet);
        if (ordered.Count == 0) yield break;
        yield return new XElement(shapes.Vml + "shapetype",
            new XAttribute("id", "_x0000_t202"), new XAttribute("coordsize", "21600,21600"), new XAttribute(shapes.Office + "spt", "202"), new XAttribute("path", "m,l,21600r21600,l21600,xe"),
            new XElement(shapes.Vml + "stroke", new XAttribute("joinstyle", "miter")),
            new XElement(shapes.Vml + "path", new XAttribute("gradientshapeok", "t"), new XAttribute(shapes.Office + "connecttype", "rect")));

        foreach (var (address, comment) in ordered)
        {
            var visible = comment.IsAlwaysVisible;
            var bounds = _geometry.Bounds(sheet.Layout, address, comment.Box);
            var anchor = _geometry.Anchor(sheet.Layout, bounds);
            yield return new XElement(shapes.Vml + "shape",
                new XAttribute("id", shapes.Next()),
                new XAttribute("type", "#_x0000_t202"),
                new XAttribute("style", string.Create(CultureInfo.InvariantCulture, $"position:absolute;margin-left:{bounds.X:0.##}pt;margin-top:{bounds.Y:0.##}pt;width:{bounds.Width:0.##}pt;height:{bounds.Height:0.##}pt;z-index:1;visibility:{(visible ? "visible" : "hidden")}")),
                new XAttribute("fillcolor", "#ffffe1"),
                new XAttribute(shapes.Office + "insetmode", "auto"),
                new XElement(shapes.Vml + "fill", new XAttribute("color2", "#ffffe1")),
                new XElement(shapes.Vml + "shadow", new XAttribute("on", "t"), new XAttribute("color", "black"), new XAttribute("obscured", "t")),
                new XElement(shapes.Vml + "path", new XAttribute(shapes.Office + "connecttype", "none")),
                new XElement(shapes.Vml + "textbox", new XAttribute("style", "mso-direction-alt:auto"), new XElement("div", new XAttribute("style", "text-align:left"))),
                new XElement(shapes.Excel + "ClientData", new XAttribute("ObjectType", "Note"),
                    new XElement(shapes.Excel + "MoveWithCells"),
                    new XElement(shapes.Excel + "SizeWithCells"),
                    new XElement(shapes.Excel + "Anchor", string.Create(CultureInfo.InvariantCulture, $"{anchor.LeftColumn}, {anchor.LeftOffset}, {anchor.TopRow}, {anchor.TopOffset}, {anchor.RightColumn}, {anchor.RightOffset}, {anchor.BottomRow}, {anchor.BottomOffset}")),
                    new XElement(shapes.Excel + "AutoFill", "False"),
                    new XElement(shapes.Excel + "Row", address.Row.ToString(CultureInfo.InvariantCulture)),
                    new XElement(shapes.Excel + "Column", address.Column.ToString(CultureInfo.InvariantCulture)),
                    visible ? new XElement(shapes.Excel + "Visible") : null));
        }
    }

    private List<KeyValuePair<CellAddress, CellComment>> Ordered(Sheet sheet) =>
        sheet.Comments.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column).ToList();
}
