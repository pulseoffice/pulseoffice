using System.Xml.Linq;
using DocumentFormat.OpenXml.CustomXmlDataProperties;
using DocumentFormat.OpenXml.Packaging;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxCustomXmlParts
{
    private readonly SafeXmlLoader _xml = new();

    public void Add(MainDocumentPart main, XElement root)
    {
        var part = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
        using (var stream = part.GetStream(FileMode.Create, FileAccess.Write)) root.Save(stream);
        var properties = part.AddNewPart<CustomXmlPropertiesPart>();
        properties.DataStoreItem = new DataStoreItem(new SchemaReferences(new SchemaReference { Uri = root.Name.NamespaceName }))
        {
            ItemId = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}",
        };
    }

    public IEnumerable<XElement> Roots(MainDocumentPart main, XName name)
    {
        foreach (var part in main.CustomXmlParts)
        {
            XElement root;
            try
            {
                using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
                root = _xml.Element(stream);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }
            if (root.Name == name) yield return root;
        }
    }
}
