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
    public void Write(WorksheetPart part, Sheet sheet)
    {
        if (sheet.Comments.Count == 0) return;

        var ordered = Ordered(sheet);
        var authors = ordered.Select(pair => pair.Value.Author ?? string.Empty).Distinct(StringComparer.Ordinal).ToList();
        var authorIds = authors.Select((author, index) => (author, index)).ToDictionary(pair => pair.author, pair => (uint)pair.index, StringComparer.Ordinal);

        var commentsPart = part.AddNewPart<WorksheetCommentsPart>();
        commentsPart.Comments = new Comments(
            new Authors(authors.Select(author => new Author(author))),
            new CommentList(ordered.Select(pair => new Comment(
                new CommentText(new Run(new DocumentFormat.OpenXml.Spreadsheet.Text(new OoxmlTextEscape().Encode(pair.Value.Text)) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve })))
            {
                Reference = pair.Key.ToA1(),
                AuthorId = authorIds[pair.Value.Author ?? string.Empty],
            })));
        commentsPart.Comments.Save();
    }

    public void Read(WorksheetPart part, Sheet sheet)
    {
        if (part.WorksheetCommentsPart?.Comments is not { } comments) return;

        var authors = comments.Authors?.Elements<Author>().Select(author => author.Text).ToList() ?? [];
        var visible = VisibleNotes(part);
        foreach (var comment in comments.CommentList?.Elements<Comment>() ?? [])
        {
            if (comment.Reference?.Value is not { } reference || !CellAddress.TryParseA1(reference, out var address)) continue;

            var index = comment.AuthorId?.Value ?? 0;
            sheet.Comments[address] = new CellComment
            {
                Text = Text(comment.CommentText),
                Author = index < (uint)authors.Count && !string.IsNullOrWhiteSpace(authors[(int)index]) ? authors[(int)index] : null,
                IsAlwaysVisible = visible.Contains(address),
            };
        }
    }

    private HashSet<CellAddress> VisibleNotes(WorksheetPart part)
    {
        var excel = System.Xml.Linq.XNamespace.Get("urn:schemas-microsoft-com:office:excel");
        var result = new HashSet<CellAddress>();

        foreach (var document in documents.Load(part))
        {
            foreach (var data in document.Descendants(excel + "ClientData"))
            {
                if (data.Attribute("ObjectType")?.Value != "Note" || data.Element(excel + "Visible") is null) continue;
                if (int.TryParse(data.Element(excel + "Row")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)
                    && int.TryParse(data.Element(excel + "Column")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var column)
                    && row >= 0 && column >= 0)
                    result.Add(new CellAddress(row, column));
            }
        }

        return result;
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
            yield return new XElement(shapes.Vml + "shape",
                new XAttribute("id", shapes.Next()),
                new XAttribute("type", "#_x0000_t202"),
                new XAttribute("style", $"position:absolute;margin-left:80pt;margin-top:2pt;width:108pt;height:60pt;z-index:1;visibility:{(visible ? "visible" : "hidden")}"),
                new XAttribute("fillcolor", "#ffffe1"),
                new XAttribute(shapes.Office + "insetmode", "auto"),
                new XElement(shapes.Vml + "fill", new XAttribute("color2", "#ffffe1")),
                new XElement(shapes.Vml + "shadow", new XAttribute("on", "t"), new XAttribute("color", "black"), new XAttribute("obscured", "t")),
                new XElement(shapes.Vml + "path", new XAttribute(shapes.Office + "connecttype", "none")),
                new XElement(shapes.Vml + "textbox", new XAttribute("style", "mso-direction-alt:auto"), new XElement("div", new XAttribute("style", "text-align:left"))),
                new XElement(shapes.Excel + "ClientData", new XAttribute("ObjectType", "Note"),
                    new XElement(shapes.Excel + "MoveWithCells"),
                    new XElement(shapes.Excel + "SizeWithCells"),
                    new XElement(shapes.Excel + "Anchor", string.Create(CultureInfo.InvariantCulture, $"{address.Column + 1}, 15, {address.Row}, 2, {address.Column + 3}, 15, {address.Row + 4}, 16")),
                    new XElement(shapes.Excel + "AutoFill", "False"),
                    new XElement(shapes.Excel + "Row", address.Row.ToString(CultureInfo.InvariantCulture)),
                    new XElement(shapes.Excel + "Column", address.Column.ToString(CultureInfo.InvariantCulture)),
                    visible ? new XElement(shapes.Excel + "Visible") : null));
        }
    }

    private List<KeyValuePair<CellAddress, CellComment>> Ordered(Sheet sheet) =>
        sheet.Comments.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column).ToList();
}
