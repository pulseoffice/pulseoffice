using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfTableTemplateSink
{
    private readonly Dictionary<string, IReadOnlyList<XElement>> _templates = new(StringComparer.Ordinal);

    public void Add(string name, Func<IEnumerable<XElement>> build)
    {
        if (!_templates.ContainsKey(name)) _templates[name] = [.. build()];
    }

    public IEnumerable<XElement> Elements => _templates.Values.SelectMany(elements => elements);
}

internal sealed class OdfDeckGridTemplates
{
    private const string Prefix = "PulseOfficeGrid";

    private readonly ITableGridLook _look;

    private readonly OdtTableTemplates _templates;

    public OdfDeckGridTemplates(ITableGridLook look)
    {
        _look = look;
        var measure = new OdtMeasure();
        _templates = new OdtTableTemplates(measure, new OdtBorders(measure), new OdtCharacterCodec(measure, new OdtLanguageTag()));
    }

    public string Name(DataTableStyle style, OdfTableTemplateSink sink)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(sink);
        var accent = _look.Accent(style);
        var name = Prefix + accent.TrimStart('#');
        sink.Add(name, () => _templates.Write(Properties(style), name));
        return name;
    }

    public string? Accent(string? templateName)
    {
        if (templateName is null || !templateName.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var hex = templateName[Prefix.Length..];
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return null;
        var accent = "#" + hex.ToUpperInvariant();
        return string.Equals(accent, _look.Accent(new DataTableStyle()), StringComparison.Ordinal) ? null : accent;
    }

    public TableProperties Properties(DataTableStyle style) => new()
    {
        BorderWidth = _look.LineWidthPoints,
        BorderColor = Argb(_look.LineColour),
        HeaderBackground = Argb(_look.HeaderFill(style)),
        HeaderColor = Argb(_look.Accent(style)),
        HeaderBold = true,
        BandBackground = Argb(_look.BandFill),
        BandAxes = TableBandAxes.Rows,
        FirstBandAxes = TableBandAxes.Rows,
    };

    private uint Argb(string hex) => 0xFF000000u | uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
