using System.Xml;
using System.Xml.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtPreservedFields(IPreservedFieldTable table, PulseOffice.Formats.Odf.TextCodecs.IOdfParagraphText text) : OdtXml
{
    private const int NativeLimit = 65536;

    private readonly IReadOnlySet<string> _elements = new HashSet<string>(StringComparer.Ordinal)
    {
        "author-initials", "template-name", "placeholder", "text-input", "drop-down", "page-variable-set", "page-variable-get",
        "expression", "sheet-name", "dde-connection", "database-next", "database-row-select", "database-row-number",
        "database-name", "print-time", "print-date", "printed-by", "editing-cycles", "editing-duration", "creation-time",
        "modification-time", "script", "execute-macro", "measure", "user-field-input", "variable-input",
    };

    private readonly IReadOnlySet<string> _mappedOnly = new HashSet<string>(StringComparer.Ordinal) { "author-name" };

    private readonly IReadOnlySet<XName> _literalChildren = new HashSet<XName> { Text + "s", Text + "tab", Text + "line-break", Text + "span", Text + "soft-page-break" };

    private XName Instruction => Pt + "field-instruction";

    public bool Preserves(XElement element) =>
        element.Attribute(Instruction) is not null || element.Name.Namespace == Text && _elements.Contains(element.Name.LocalName);

    public OdtFieldRead? Read(XElement element)
    {
        if (!Preserves(element)) return null;
        var instruction = (string?)element.Attribute(Instruction) is { Length: > 0 } stored ? stored : null;
        var native = Native(element, instruction);
        return new OdtFieldRead(TextFieldKind.Preserved, instruction, native is null ? null : new FieldDetail { NativeOdf = native });
    }

    public XElement Build(TextRun run, string shown)
    {
        var native = Parsed(run.FieldDetail?.NativeOdf);
        var instruction = run.FieldArgument is { Length: > 0 } stored ? stored : native is null ? table.WordInstructionFor(null, shown) : null;
        var element = native ?? Mapped(instruction);
        if (instruction is not null) element.SetAttributeValue(Instruction, instruction);
        element.Add(text.Paragraph(shown).Nodes());
        return element;
    }

    private string? Native(XElement element, string? instruction)
    {
        if (element.Name.Namespace != Text || !Accepted(element.Name.LocalName)) return null;
        var copy = Stripped(element);
        if (instruction is not null && table.OdfElementFor(instruction) == copy.Name.LocalName && Generated(copy)) return null;
        var markup = copy.ToString(SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces);
        return markup.Length <= NativeLimit ? markup : null;
    }

    private bool Generated(XElement element) =>
        !element.HasElements && element.Attributes().All(attribute => attribute.IsNamespaceDeclaration || attribute.Name == Text + "placeholder-type" && attribute.Value == "text");

    private bool Accepted(string localName) => _elements.Contains(localName) || _mappedOnly.Contains(localName);

    private XElement Stripped(XElement element)
    {
        var copy = new XElement(element.Name, element.Attributes().Where(attribute => attribute.Name != Instruction && !attribute.IsNamespaceDeclaration),
            element.Elements().Where(child => !_literalChildren.Contains(child.Name)).Select(child => new XElement(child)));
        return copy;
    }

    private XElement? Parsed(string? native)
    {
        if (native is not { Length: > 0 and <= NativeLimit }) return null;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true, IgnoreProcessingInstructions = true, MaxCharactersInDocument = NativeLimit * 4 };
            using var reader = XmlReader.Create(new StringReader(native), settings);
            var element = XElement.Load(reader);
            return element.Name.Namespace == Text && Accepted(element.Name.LocalName) ? Stripped(element) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private XElement Mapped(string? instruction)
    {
        if (instruction is null || table.OdfElementFor(instruction) is not { } local) return new XElement(Text + "span");
        var element = new XElement(Text + local);
        if (local == "placeholder") element.SetAttributeValue(Text + "placeholder-type", "text");
        if (local == "text-input" && table.Prompt(instruction) is { Length: > 0 } prompt) element.SetAttributeValue(Text + "description", prompt);
        return element;
    }
}
