using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxVmlDocuments
{
    private readonly SafeXmlLoader _xml = new();

    public IEnumerable<XDocument> Load(WorksheetPart part)
    {
        foreach (var vml in part.VmlDrawingParts)
        {
            XDocument document;
            try
            {
                using var stream = vml.GetStream(FileMode.Open, FileAccess.Read);
                document = _xml.Document(stream);
            }
            catch (XmlException)
            {
                continue;
            }

            yield return document;
        }
    }

    public void Write(WorksheetPart part, XlsxVmlShapes shapes, Func<VmlDrawingPart, IEnumerable<XElement>> contents)
    {
        var vml = part.AddNewPart<VmlDrawingPart>();
        var elements = contents(vml).ToList();
        if (elements.Count == 0)
        {
            part.DeletePart(vml);
            return;
        }

        var root = new XElement("xml",
            new XAttribute(XNamespace.Xmlns + "v", shapes.Vml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "o", shapes.Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "x", shapes.Excel.NamespaceName),
            new XElement(shapes.Office + "shapelayout", new XAttribute(shapes.Vml + "ext", "edit"),
                new XElement(shapes.Office + "idmap", new XAttribute(shapes.Vml + "ext", "edit"), new XAttribute("data", shapes.Block))),
            elements);
        using (var stream = vml.GetStream(FileMode.Create))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            writer.Write(root.ToString(SaveOptions.DisableFormatting));
        part.Worksheet!.Append(new LegacyDrawing { Id = part.GetIdOfPart(vml) });
    }
}
