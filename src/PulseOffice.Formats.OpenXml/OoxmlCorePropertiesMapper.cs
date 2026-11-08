using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using System.Globalization;
using PulseOffice.Document.Metadata;

namespace PulseOffice.Formats.OpenXml;

internal abstract class CustomValueCodec
{
    public abstract CustomPropertyKind Kind { get; }

    public abstract bool Write(CustomDocumentProperty property, string value);

    public abstract string? Read(CustomDocumentProperty property);
}

internal sealed class TextValueCodec : CustomValueCodec
{
    public override CustomPropertyKind Kind => CustomPropertyKind.Text;

    public override bool Write(CustomDocumentProperty property, string value)
    {
        property.VTLPWSTR = new VTLPWSTR(value);
        return true;
    }

    public override string? Read(CustomDocumentProperty property) => property.VTLPWSTR?.Text ?? property.VTBString?.Text;
}

internal sealed class NumberValueCodec : CustomValueCodec
{
    public override CustomPropertyKind Kind => CustomPropertyKind.Number;

    public override bool Write(CustomDocumentProperty property, string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return false;
        if (number == Math.Floor(number) && number is >= int.MinValue and <= int.MaxValue)
            property.VTInt32 = new VTInt32(((int)number).ToString(CultureInfo.InvariantCulture));
        else
            property.VTDouble = new VTDouble(number.ToString("R", CultureInfo.InvariantCulture));
        return true;
    }

    public override string? Read(CustomDocumentProperty property) =>
        property.VTInt32?.Text ?? property.VTDouble?.Text ?? property.VTInt64?.Text ?? property.VTFloat?.Text
        ?? property.VTUnsignedInt32?.Text ?? property.VTDecimal?.Text;
}

internal sealed class BooleanValueCodec : CustomValueCodec
{
    public override CustomPropertyKind Kind => CustomPropertyKind.Boolean;

    public override bool Write(CustomDocumentProperty property, string value)
    {
        if (!bool.TryParse(value, out var flag)) return false;
        property.VTBool = new VTBool(flag ? "true" : "false");
        return true;
    }

    public override string? Read(CustomDocumentProperty property) =>
        property.VTBool?.Text is { } text ? (text is "1" or "true" ? "true" : "false") : null;
}

internal sealed class DateValueCodec : CustomValueCodec
{
    public override CustomPropertyKind Kind => CustomPropertyKind.Date;

    public override bool Write(CustomDocumentProperty property, string value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)) return false;
        property.VTFileTime = new VTFileTime(moment.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        return true;
    }

    public override string? Read(CustomDocumentProperty property) =>
        property.VTFileTime?.Text is { } text
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
            : property.VTDate?.Text;
}

public sealed class OoxmlCorePropertiesMapper
{
    private readonly IReadOnlyList<CustomValueCodec> _codecs =
        [new NumberValueCodec(), new BooleanValueCodec(), new DateValueCodec(), new TextValueCodec()];

    private const string CustomFormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";
    private const string VariantTypes = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

    public void Write(OpenXmlPackage package, DocumentProperties properties)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(properties);

        if (properties.IsEmpty && properties.Created is null && properties.Author is null) return;

        var core = package.PackageProperties;

        core.Title = properties.Title;
        core.Subject = properties.Subject;
        core.Description = properties.Description;
        core.Creator = properties.Author;
        core.LastModifiedBy = properties.LastModifiedBy;
        core.Keywords = properties.Keywords.Count > 0 ? string.Join(", ", properties.Keywords) : null;
        core.Created = properties.Created?.UtcDateTime;
        core.Modified = properties.Modified?.UtcDateTime;
        core.Language = properties.Language;
        core.Revision = properties.EditingCycles > 0 ? properties.EditingCycles.ToString(CultureInfo.InvariantCulture) : null;

        WriteCustom(package, properties);
    }

    private void WriteCustom(OpenXmlPackage package, DocumentProperties properties)
    {
        if (properties.Custom.Count == 0) return;

        var root = new Properties();
        root.AddNamespaceDeclaration("vt", VariantTypes);

        var id = 2;
        foreach (var (name, value) in properties.Custom)
        {
            var property = new CustomDocumentProperty { FormatId = CustomFormatId, PropertyId = id++, Name = name };
            var kind = properties.KindOf(name);
            if (!_codecs.First(codec => codec.Kind == kind).Write(property, value))
                _codecs.First(codec => codec.Kind == CustomPropertyKind.Text).Write(property, value);
            root.Append(property);
        }

        var part = package.GetPartsOfType<CustomFilePropertiesPart>().FirstOrDefault()
            ?? package.AddNewPart<CustomFilePropertiesPart>();
        part.Properties = root;
        part.Properties.Save();
    }

    public void Read(OpenXmlPackage package, DocumentProperties properties)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(properties);

        var core = package.PackageProperties;

        properties.Title = core.Title;
        properties.Subject = core.Subject;
        properties.Description = core.Description;
        properties.Author = core.Creator;
        properties.LastModifiedBy = core.LastModifiedBy;
        properties.Language = core.Language is { Length: > 0 } language ? language : null;
        properties.Created = core.Created is { } created ? Utc(created) : null;
        properties.Modified = core.Modified is { } modified ? Utc(modified) : null;

        properties.Keywords.Clear();

        if (core.Keywords is { Length: > 0 } keywords)
            properties.Keywords.AddRange(
                keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        properties.EditingCycles = int.TryParse(core.Revision, NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision) && revision > 0
            ? revision
            : 0;

        properties.Custom.Clear();
        properties.CustomKinds.Clear();

        if (package.GetPartsOfType<CustomFilePropertiesPart>().FirstOrDefault()?.Properties is { } custom)
            foreach (var property in custom.Elements<CustomDocumentProperty>())
            {
                if (property.Name?.Value is not { Length: > 0 } name) continue;
                var read = _codecs.Select(codec => (codec.Kind, Value: codec.Read(property))).FirstOrDefault(pair => pair.Value is not null);
                properties.Custom[name] = read.Value ?? property.InnerText;
                if (read.Value is not null && read.Kind != CustomPropertyKind.Text) properties.CustomKinds[name] = read.Kind;
            }
    }

    private DateTimeOffset Utc(DateTime moment) =>
        new(moment.Kind == DateTimeKind.Local ? moment.ToUniversalTime() : DateTime.SpecifyKind(moment, DateTimeKind.Utc));
}
