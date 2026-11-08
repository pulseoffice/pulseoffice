using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Fields;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxBibliography
{
    private readonly XNamespace _b = "http://schemas.openxmlformats.org/officeDocument/2006/bibliography";
    private readonly SafeXmlLoader _xml = new();

    private readonly IReadOnlyList<(string Ours, string Word)> _fields =
    [
        ("title", "Title"), ("year", "Year"), ("publisher", "Publisher"), ("address", "City"), ("edition", "Edition"),
        ("journal", "JournalName"), ("volume", "Volume"), ("number", "Issue"), ("pages", "Pages"), ("url", "URL"),
        ("isbn", "StandardNumber"), ("booktitle", "BookTitle"), ("institution", "Institution"), ("month", "Month"),
        ("note", "Comments"), ("chapter", "ChapterNumber"), ("school", "ThesisType"), ("howpublished", "Medium"),
        ("series", "ShortTitle"), ("report-type", "ReportType"), ("organizations", "ProductionCompany"),
    ];

    private readonly IReadOnlyList<(string Ours, string Word)> _types =
    [
        ("book", "Book"), ("article", "JournalArticle"), ("inbook", "BookSection"), ("inproceedings", "ConferenceProceedings"),
        ("techreport", "Report"), ("www", "InternetSite"), ("misc", "Misc"), ("journal", "ArticleInAPeriodical"),
        ("incollection", "BookSection"), ("proceedings", "ConferenceProceedings"), ("conference", "ConferenceProceedings"),
        ("phdthesis", "Report"), ("mastersthesis", "Report"), ("email", "ElectronicSource"),
    ];

    public bool Loses(BibliographySource source) =>
        source.Values.Any(pair => pair.Value.Length > 0 && pair.Key != "author" && pair.Key != "editor" && _fields.All(field => field.Ours != pair.Key))
        || Reverse(WordType(source.Type)) != source.Type;

    private string WordType(string type) => _types.FirstOrDefault(entry => entry.Ours == type).Word ?? "Misc";

    private string Reverse(string? word) => _types.FirstOrDefault(entry => entry.Word == word).Ours ?? "misc";

    public void Write(MainDocumentPart main, TextDocument document)
    {
        if (document.Bibliography.All.Count == 0) return;
        var sources = new XElement(_b + "Sources", new XAttribute(XNamespace.Xmlns + "b", _b), new XAttribute("SelectedStyle", string.Empty),
            document.Bibliography.All.Select(Source));
        var part = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
        using (var stream = part.GetStream(FileMode.Create, FileAccess.Write)) sources.Save(stream);
        var properties = part.AddNewPart<CustomXmlPropertiesPart>();
        properties.DataStoreItem = new DocumentFormat.OpenXml.CustomXmlDataProperties.DataStoreItem(
            new DocumentFormat.OpenXml.CustomXmlDataProperties.SchemaReferences(
                new DocumentFormat.OpenXml.CustomXmlDataProperties.SchemaReference { Uri = _b.NamespaceName }))
        { ItemId = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}" };
    }

    private XElement Source(BibliographySource source)
    {
        var element = new XElement(_b + "Source",
            new XElement(_b + "Tag", source.Identifier),
            new XElement(_b + "SourceType", WordType(source.Type)));
        var people = new XElement(_b + "Author");
        if (source.Value("author") is { Length: > 0 } author) people.Add(new XElement(_b + "Author", new XElement(_b + "Corporate", author)));
        if (source.Value("editor") is { Length: > 0 } editor) people.Add(new XElement(_b + "Editor", new XElement(_b + "Corporate", editor)));
        if (people.HasElements) element.Add(people);
        foreach (var (ours, word) in _fields)
            if (source.Value(ours) is { Length: > 0 } value) element.Add(new XElement(_b + word, value));
        return element;
    }

    public void Read(MainDocumentPart main, TextDocument document)
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
            if (root.Name != _b + "Sources") continue;
            foreach (var source in root.Elements(_b + "Source"))
            {
                if (source.Element(_b + "Tag")?.Value is not { Length: > 0 } tag) continue;
                var word = source.Element(_b + "SourceType")?.Value;
                var type = Reverse(word);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                if (Person(source.Element(_b + "Author")?.Element(_b + "Author")) is { Length: > 0 } author) values["author"] = author;
                if (Person(source.Element(_b + "Author")?.Element(_b + "Editor")) is { Length: > 0 } editor) values["editor"] = editor;
                foreach (var (ours, name) in _fields)
                    if (source.Element(_b + name)?.Value is { Length: > 0 } value) values[ours] = value;
                if (document.Bibliography.Find(tag) is null) document.Bibliography.Store(new BibliographySource(tag, type, values));
            }
        }
    }

    private string? Person(XElement? role)
    {
        if (role is null) return null;
        if (role.Element(_b + "Corporate")?.Value is { Length: > 0 } corporate) return corporate;
        var names = role.Descendants(_b + "Person")
            .Select(person => string.Join(' ', new[] { person.Element(_b + "First")?.Value, person.Element(_b + "Middle")?.Value, person.Element(_b + "Last")?.Value }.Where(part => !string.IsNullOrEmpty(part))))
            .Where(name => name.Length > 0);
        return string.Join("; ", names);
    }
}
