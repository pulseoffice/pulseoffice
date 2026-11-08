using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Document.Embedding;
using PulseOffice.Formulas;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxFormulaMapper
{
    private readonly IFormulaParser _parser;
    private readonly IOmmlSyntax _omml;
    private readonly SafeXmlLoader _xml = new();

    public XlsxFormulaMapper(IOmmlSyntax? omml = null, IFormulaParser? parser = null)
    {
        _omml = omml ?? new OmmlSyntax();
        _parser = parser ?? new FormulaParser();
    }

    public void Write(WorkbookPart part, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(workbook);

        var root = new XElement(OmmlSyntax.Namespace + "oMathPara");

        foreach (var sheet in workbook.Sheets)
        foreach (var item in sheet.Objects)
        {
            if (item.Content is not FormulaObject formula) continue;

            var math = _omml.Write(_parser.Parse(formula.Expression));

            math.SetAttributeValue("name", Describe(formula, item, sheet.Name));

            root.Add(math);
        }

        if (!root.HasElements) return;

        var custom = part.AddNewPart<CustomXmlPart>("application/xml");

        using var stream = custom.GetStream(FileMode.Create, FileAccess.Write);

        new XDocument(root).Save(stream);
    }

    public void Read(WorkbookPart part, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(workbook);

        foreach (var custom in part.GetPartsOfType<CustomXmlPart>())
        {
            XDocument document;

            using (var stream = custom.GetStream(FileMode.Open, FileAccess.Read))
            {
                try
                {
                    document = _xml.Document(stream);
                }
                catch (System.Xml.XmlException)
                {
                    continue;
                }
            }

            if (document.Root?.Name != OmmlSyntax.Namespace + "oMathPara") continue;

            foreach (var math in document.Root.Elements(OmmlSyntax.Namespace + "oMath"))
            {
                if ((string?)math.Attribute("name") is not { } name) continue;
                if (Parse(name) is not { } described) continue;

                var sheet = workbook.Sheets.FirstOrDefault(
                    candidate => candidate.Name == described.SheetName);

                if (sheet is null) continue;

                var formula = _omml.Read(math);

                var expression = described.Expression.Length > 0
                    ? described.Expression
                    : formula.Source;

                sheet.Objects.Add(new PlacedObject
                {
                    Id = described.Id,
                    Anchor = described.Anchor,
                    Size = described.Size,
                    Content = new FormulaObject { Id = described.Id, Expression = expression },
                });
            }
        }
    }

    private string Describe(FormulaObject formula, PlacedObject placed, string sheetName) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{Field(sheetName)}|{Field(placed.Id)}|{placed.Anchor.Row}|{placed.Anchor.Column}|{placed.Size.Width.Value}|{placed.Size.Height.Value}|{formula.Expression}");

    private Described? Parse(string text)
    {
        var parts = text.Split('|', 7);
        if (parts.Length < 7) return null;

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)) return null;
        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var column)) return null;
        if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)) return null;
        if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var height)) return null;

        return new Described(
            Uri.UnescapeDataString(parts[0]),
            Uri.UnescapeDataString(parts[1]),
            new CellAddress(row, column),
            new DocumentSize(new Twip((int)Math.Round(width)), new Twip((int)Math.Round(height))),
            parts[6]);
    }

    private string Field(string text) => text.Replace("%", "%25", StringComparison.Ordinal).Replace("|", "%7C", StringComparison.Ordinal);

    private sealed record Described(string SheetName, string Id, CellAddress Anchor, DocumentSize Size, string Expression);
}
