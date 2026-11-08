using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Documents.Objects;
using PulseOffice.Formulas;
using PulseOffice.Foundation.Units;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;
using W14 = DocumentFormat.OpenXml.Office2010.Word;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public abstract class DocxInlineObjectCodec
{
    public virtual bool Positions => false;

    public abstract bool Writes(AnchoredContent content);

    public abstract OpenXmlElement Write(AnchoredObject item, DocxWriteContext context);

    protected AnchoredObject Inline(DocxReadContext context, string paragraphId, EmbeddedObject value, DocumentSize size) =>
        new(value.Id, new AsCharacterAnchor(paragraphId), new EmbeddedContent(value with { NaturalSize = size }), size);
}

public sealed class DocxFormulaCodec(IOmmlSyntax omml, IFormulaParser parser, ILinearFormulaWriter linear) : DocxInlineObjectCodec
{
    public override bool Writes(AnchoredContent content) => content is EmbeddedContent { Value: FormulaObject };

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context)
    {
        var formula = (FormulaObject)((EmbeddedContent)item.Content).Value;
        var math = omml.Write(parser.Parse(formula.Expression));
        return new M.OfficeMath(math.ToString(SaveOptions.DisableFormatting));
    }

    public AnchoredObject? Read(OpenXmlElement math, DocxReadContext context, string paragraphId)
    {
        try
        {
            var formula = omml.Read(XElement.Parse(math.OuterXml));
            var expression = linear.Write(formula);
            if (expression.Length == 0) return null;
            return Inline(context, paragraphId, new FormulaObject { Id = context.Document.NextId("obj"), Expression = expression }, FormulaObject.StandardSize);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}

public sealed class DocxOleCodec(DocxValues values) : DocxInlineObjectCodec
{
    private const string OleType = "application/vnd.openxmlformats-officedocument.oleObject";
    private const double LargestPoints = 31_680;
    private readonly PulseOffice.Formats.OpenXml.Embedding.OoxmlOleReplacement _replacement = new();
    private static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    private static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Wml = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public override bool Writes(AnchoredContent content) => content is EmbeddedContent { Value: PackagedObject { IsLinked: false } };

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context)
    {
        var package = (PackagedObject)((EmbeddedContent)item.Content).Value;
        var data = context.Part.AddNewPart<EmbeddedObjectPart>(OleType, null);
        using (var stream = data.GetStream(FileMode.Create)) stream.Write(package.Data.Span);
        var image = context.AddImage(ImagePartType.Png);
        var png = _replacement.Picture(package);
        using (var stream = image.GetStream(FileMode.Create)) stream.Write(png, 0, png.Length);
        var shapeId = "_x0000_i" + values.Text(context.Drawings.Fresh() + 1024);
        var width = item.Size.Width.ToPoints().ToString("0.##", CultureInfo.InvariantCulture);
        var height = item.Size.Height.ToPoints().ToString("0.##", CultureInfo.InvariantCulture);
        var element = new XElement(Wml + "object",
            new XAttribute(XNamespace.Xmlns + "w", Wml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "v", V.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "o", O.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
            new XElement(V + "shape",
                new XAttribute("id", shapeId),
                new XAttribute("style", $"width:{width}pt;height:{height}pt"),
                new XAttribute("alt", package.FileName),
                new XElement(V + "imagedata", new XAttribute(R + "id", context.Part.GetIdOfPart(image)), new XAttribute(O + "title", package.FileName))),
            new XElement(O + "OLEObject",
                new XAttribute("Type", "Embed"),
                new XAttribute("ProgID", package.MediaType),
                new XAttribute("ShapeID", shapeId),
                new XAttribute("DrawAspect", package.HasReplacement ? "Content" : "Icon"),
                new XAttribute("ObjectID", "_" + values.Text(context.Drawings.Fresh() + 100000)),
                new XAttribute(R + "id", context.Part.GetIdOfPart(data))));
        return new W.Run(new W.EmbeddedObject(element.ToString(SaveOptions.DisableFormatting)));
    }

    public AnchoredObject? Read(W.EmbeddedObject element, DocxReadContext context, string paragraphId)
    {
        var xml = XElement.Parse(element.OuterXml);
        var ole = xml.Descendants(O + "OLEObject").FirstOrDefault();
        if ((string?)ole?.Attribute(R + "id") is not { } id || !context.Part.TryGetPartById(id, out var part)) return null;
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var shape = xml.Descendants(V + "shape").FirstOrDefault();
        var progId = (string?)ole.Attribute("ProgID") ?? string.Empty;
        var replacement = (string?)shape?.Element(V + "imagedata")?.Attribute(R + "id") is { } pictureId && context.Part.TryGetPartById(pictureId, out var picturePart)
            ? _replacement.Replacement(Bytes(picturePart))
            : null;
        var mediaType = progId.Contains('/', StringComparison.Ordinal) ? progId : "application/octet-stream";
        var name = (string?)shape?.Attribute("alt") ?? (string?)shape?.Element(V + "imagedata")?.Attribute(O + "title") ?? progId + ".bin";
        var size = Size((string?)shape?.Attribute("style"));
        var packaged = new PackagedObject
        {
            Id = context.Document.NextId("obj"),
            FileName = name,
            MediaType = mediaType,
            Data = buffer.ToArray(),
            Replacement = replacement,
            Packaged = mediaType.StartsWith("video/", StringComparison.Ordinal) || mediaType.StartsWith("audio/", StringComparison.Ordinal)
                ? PackagedObjectKind.Media
                : PackagedObjectKind.File,
        };
        return Inline(context, paragraphId, packaged, size);
    }

    private byte[] Bytes(OpenXmlPart part)
    {
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private DocumentSize Size(string? style)
    {
        double Points(string name)
        {
            var part = (style ?? string.Empty).Split(';').Select(item => item.Split(':')).FirstOrDefault(pair => pair.Length == 2 && pair[0].Trim() == name);
            var text = part?[1].Trim().Replace("pt", string.Empty, StringComparison.Ordinal);
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? Math.Clamp(value, 0, LargestPoints) : 72;
        }

        return new DocumentSize(Twip.FromPoints(Points("width")), Twip.FromPoints(Points("height")));
    }
}

public sealed class DocxFormControlCodec : DocxInlineObjectCodec
{
    private const string TagPrefix = "pulseoffice.form:";

    private readonly IReadOnlySet<FormControlKind> _texts = new HashSet<FormControlKind>
    {
        FormControlKind.TextBox, FormControlKind.FormattedField, FormControlKind.NumericField,
        FormControlKind.CurrencyField, FormControlKind.PatternField,
    };

    public override bool Writes(AnchoredContent content) =>
        content is EmbeddedContent { Value: FormControlObject control } && Maps(control.ControlKind);

    public bool Maps(FormControlKind kind) =>
        kind is FormControlKind.CheckBox or FormControlKind.ListBox or FormControlKind.ComboBox or FormControlKind.DateField || _texts.Contains(kind);

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context)
    {
        var control = (FormControlObject)((EmbeddedContent)item.Content).Value;
        var properties = control.Properties;
        var sdtProperties = new W.SdtProperties();
        if (properties.Label is { } label) sdtProperties.Append(new W.SdtAlias { Val = label });
        sdtProperties.Append(new W.Tag { Val = TagPrefix + properties.Name });
        string shown;
        switch (control.ControlKind)
        {
            case FormControlKind.CheckBox:
                var on = properties.Value.Checked == CheckState.Checked;
                sdtProperties.Append(new W14.SdtContentCheckBox(
                    new W14.Checked { Val = on ? W14.OnOffValues.One : W14.OnOffValues.Zero },
                    new W14.CheckedState { Font = "MS Gothic", Val = "2612" },
                    new W14.UncheckedState { Font = "MS Gothic", Val = "2610" }));
                shown = on ? "☒" : "☐";
                break;
            case FormControlKind.ListBox or FormControlKind.ComboBox:
                OpenXmlCompositeElement list = control.ControlKind == FormControlKind.ComboBox ? new W.SdtContentComboBox() : new W.SdtContentDropDownList();
                foreach (var entry in properties.Items) list.Append(new W.ListItem { DisplayText = entry, Value = entry });
                var selected = properties.Value.SelectedIndices.FirstOrDefault(-1);
                shown = selected >= 0 && selected < properties.Items.Count ? properties.Items[selected] : properties.Value.Text ?? string.Empty;
                if (list is W.SdtContentComboBox combo && shown.Length > 0) combo.LastValue = shown;
                if (list is W.SdtContentDropDownList drop && shown.Length > 0) drop.LastValue = shown;
                sdtProperties.Append(list);
                break;
            case FormControlKind.DateField:
                shown = properties.Value.Text ?? string.Empty;
                var date = new W.SdtContentDate(new W.DateFormat { Val = properties.FormatCode ?? "dd/MM/yyyy" });
                if (DateTime.TryParse(shown, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) date.FullDate = parsed;
                sdtProperties.Append(date);
                break;
            default:
                shown = properties.Value.Text ?? string.Empty;
                sdtProperties.Append(new W.SdtContentText { MultiLine = properties.MultiLine ? true : null });
                break;
        }

        return new W.SdtRun(sdtProperties, new W.SdtContentRun(new W.Run(new W.Text(shown) { Space = SpaceProcessingModeValues.Preserve })));
    }

    public AnchoredObject? Read(W.SdtRun sdt, DocxReadContext context, string paragraphId)
    {
        var properties = sdt.SdtProperties;
        if (properties?.GetFirstChild<W.Tag>()?.Val?.Value is not { } tag || !tag.StartsWith(TagPrefix, StringComparison.Ordinal)) return null;
        var text = string.Concat(sdt.SdtContentRun?.Descendants<W.Text>().Select(item => item.Text) ?? []);
        FormControlKind kind;
        var value = new FormControlValue();
        IReadOnlyList<string> items = [];
        string? format = null;
        if (properties.GetFirstChild<W14.SdtContentCheckBox>() is { } box)
        {
            kind = FormControlKind.CheckBox;
            var on = box.Checked?.Val?.Value is { } state && (state == W14.OnOffValues.One || state == W14.OnOffValues.True);
            value = value with { Checked = on ? CheckState.Checked : CheckState.Unchecked };
        }
        else if (properties.GetFirstChild<W.SdtContentDropDownList>() is { } drop)
        {
            kind = FormControlKind.ListBox;
            items = [.. drop.Elements<W.ListItem>().Select(entry => entry.DisplayText?.Value ?? entry.Value?.Value ?? string.Empty)];
            value = Selection(items, text);
        }
        else if (properties.GetFirstChild<W.SdtContentComboBox>() is { } combo)
        {
            kind = FormControlKind.ComboBox;
            items = [.. combo.Elements<W.ListItem>().Select(entry => entry.DisplayText?.Value ?? entry.Value?.Value ?? string.Empty)];
            value = Selection(items, text);
        }
        else if (properties.GetFirstChild<W.SdtContentDate>() is { } date)
        {
            kind = FormControlKind.DateField;
            format = date.DateFormat?.Val?.Value;
            value = value with { Text = date.FullDate?.Value is { } full ? full.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : text };
        }
        else if (properties.GetFirstChild<W.SdtContentText>() is not null)
        {
            kind = FormControlKind.TextBox;
            value = value with { Text = text };
        }
        else
        {
            return null;
        }

        var control = new FormControlObject
        {
            Id = context.Document.NextId("obj"),
            ControlKind = kind,
            Properties = new FormControlProperties
            {
                Name = tag[TagPrefix.Length..],
                Label = properties.GetFirstChild<W.SdtAlias>()?.Val?.Value,
                Items = items,
                Value = value,
                FormatCode = format,
                MultiLine = properties.GetFirstChild<W.SdtContentText>()?.MultiLine?.Value ?? false,
            },
        };
        return Inline(context, paragraphId, control, new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(0.8)));
    }

    private FormControlValue Selection(IReadOnlyList<string> items, string text)
    {
        var index = items.ToList().IndexOf(text);
        return index >= 0 ? new FormControlValue { SelectedIndices = [index], Text = text } : new FormControlValue { Text = text };
    }
}

public sealed record DocxObjectCodecs(DocxFormulaCodec Formulas, DocxOleCodec Ole, DocxFormControlCodec Controls, DocxSignatureLineCodec Signatures)
{
    public IReadOnlyList<DocxInlineObjectCodec> All => [Formulas, Ole, Controls, Signatures];
}
