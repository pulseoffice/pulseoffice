using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Styling;
using CellStyle = PulseOffice.Sheets.Styling.CellStyle;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxStyleMapper
{
    private readonly Fonts _fontsElement = new();
    private readonly Fills _fillsElement = new();
    private readonly Borders _bordersElement = new();
    private readonly CellFormats _cellFormats = new();
    private readonly NumberingFormats _numberFormats = new();
    private readonly CellStyleFormats _namedFormats = new();
    private readonly CellStyles _namedStyles = new();

    private readonly StyleTable<Font> _fonts;
    private readonly StyleTable<Fill> _fills;
    private readonly StyleTable<Border> _borders;
    private readonly FontCodec _fontCodec;
    private readonly FillCodec _fillCodec;
    private readonly BorderCodec _borderCodec;
    private readonly AlignmentCodec _alignment;

    private readonly Dictionary<(int StyleId, string? Named, int Inferred), uint> _styleIndex = [];
    private readonly Dictionary<string, uint> _namedIndex = new(StringComparer.OrdinalIgnoreCase);

    public XlsxStyleMapper()
    {
        var colours = new SpreadsheetColours();
        var vocabulary = new SpreadsheetVocabulary();
        _fontCodec = new FontCodec(colours, vocabulary);
        _fillCodec = new FillCodec(colours);
        _borderCodec = new BorderCodec(colours, vocabulary, new BorderPresets());
        _alignment = new AlignmentCodec(vocabulary);

        _fonts = new StyleTable<Font>(_fontsElement, count => _fontsElement.Count = count);
        _fills = new StyleTable<Fill>(_fillsElement, count => _fillsElement.Count = count);
        _borders = new StyleTable<Border>(_bordersElement, count => _bordersElement.Count = count);

        _fills.Intern("none", () => new Fill(new PatternFill { PatternType = PatternValues.None }));
        _fills.Intern("gray125", () => new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        _fonts.Intern("Calibri|10|0|0|0|0|#000000", () => new Font(new FontSize { Val = 10 }, new FontName { Val = "Calibri" }));
        _borders.Intern("none", () => new Border(new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder()));
    }

    public Stylesheet Build()
    {
        if (_namedFormats.ChildElements.Count == 0)
        {
            _namedFormats.AppendChild(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 });
            _namedStyles.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 });
        }

        _namedFormats.Count = (uint)_namedFormats.ChildElements.Count;
        _namedStyles.Count = (uint)_namedStyles.ChildElements.Count;

        return new Stylesheet(_numberFormats, _fontsElement, _fillsElement, _bordersElement, _namedFormats, _cellFormats, _namedStyles);
    }

    public void DeclareNamedStyles(PulseOffice.Sheets.NamedStyleCatalog catalog, PulseOffice.Sheets.Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(workbook);

        if (catalog.All.Count > 0 && catalog.Find(DefaultStyle) is null)
        {
            _namedFormats.AppendChild(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 });
            _namedStyles.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 });
        }

        foreach (var named in catalog.All.OrderBy(named => string.Equals(named.Name, DefaultStyle, StringComparison.OrdinalIgnoreCase) ? 0 : 1))
        {
            var style = catalog.StyleOf(named);
            var index = (uint)_namedFormats.ChildElements.Count;
            var code = style.NumberFormatId >= PulseOffice.Sheets.Formatting.NumberFormatCatalog.FirstCustomId
                ? workbook.NumberFormats.Get(style.NumberFormatId).Code
                : null;

            var format = new CellFormat
            {
                FontId = Font(style.Font),
                FillId = Fill(style.BackgroundHex),
                BorderId = Border(style.Borders),
                NumberFormatId = NumberFormat(style.NumberFormatId, code),
                ApplyFont = true,
                ApplyFill = style.BackgroundHex is not null,
                ApplyBorder = !style.Borders.IsEmpty,
                ApplyNumberFormat = style.NumberFormatId != 0,
            };
            if (_alignment.Build(style) is { } alignment)
            {
                format.Alignment = alignment;
                format.ApplyAlignment = true;
            }
            if (!style.Locked)
            {
                format.Protection = new Protection { Locked = false };
                format.ApplyProtection = true;
            }
            _namedFormats.AppendChild(format);
            _namedStyles.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.CellStyle
            {
                Name = named.Name,
                FormatId = index,
                BuiltinId = string.Equals(named.Name, DefaultStyle, StringComparison.OrdinalIgnoreCase) ? 0u : null,
            });

            _namedIndex[named.Name] = index;
        }
    }

    public uint? NamedFormatOf(string? name) =>
        name is not null && _namedIndex.TryGetValue(name, out var index) ? index : null;

    public uint StyleIndexOf(int styleId, PulseOffice.Sheets.Workbook workbook)
    {
        var style = workbook.Styles.Get(styleId);
        var code = style.NumberFormatId >= PulseOffice.Sheets.Formatting.NumberFormatCatalog.FirstCustomId
            ? workbook.NumberFormats.Get(style.NumberFormatId).Code
            : null;
        return Map(styleId, style, code);
    }

    public uint Map(int styleId, CellStyle style, string? numberFormatCode, string? namedStyle = null, int inferredFormat = 0)
    {
        var key = (styleId, namedStyle, inferredFormat);
        if (_styleIndex.TryGetValue(key, out var existing)) return existing;

        var format = new CellFormat
        {
            FontId = Font(style.Font),
            FillId = Fill(style.BackgroundHex),
            BorderId = Border(style.Borders),
            NumberFormatId = NumberFormat(style.NumberFormatId, numberFormatCode),
            ApplyFont = true,
            ApplyFill = style.BackgroundHex is not null,
            ApplyBorder = !style.Borders.IsEmpty,
            ApplyNumberFormat = style.NumberFormatId != 0,
        };

        if (_alignment.Build(style) is { } alignment)
        {
            format.Alignment = alignment;
            format.ApplyAlignment = true;
        }

        if (!style.Locked)
        {
            format.Protection = new Protection { Locked = false };
            format.ApplyProtection = true;
        }

        if (NamedFormatOf(namedStyle) is { } namedIndex) format.FormatId = namedIndex;

        var index = _cellFormatCount;
        _cellFormats.AppendChild(format);
        _cellFormatCount = index + 1;
        _cellFormats.Count = _cellFormatCount;
        _styleIndex[key] = index;
        return index;
    }

    private uint Font(CellFont font) => _fonts.Intern(_fontCodec.Key(font), () => _fontCodec.Build(font));

    private uint Fill(string? background) =>
        background is null ? 0 : _fills.Intern(background, () => _fillCodec.Build(background));

    private uint Border(CellBorders borders) =>
        borders.IsEmpty ? 0 : _borders.Intern(_borderCodec.Key(borders), () => _borderCodec.Build(borders));

    private readonly ExcelBooleanFormat _booleanFormats = new();

    private uint NumberFormat(int numberFormatId, string? code)
    {
        if (numberFormatId < PulseOffice.Sheets.Formatting.NumberFormatCatalog.FirstCustomId) return (uint)numberFormatId;
        if (code is null) return 0;

        if (_writtenNumberFormats.Add(numberFormatId))
        {
            _numberFormats.AppendChild(new NumberingFormat { NumberFormatId = (uint)numberFormatId, FormatCode = _booleanFormats.ToExcel(code) });
            _numberFormats.Count = (uint)_writtenNumberFormats.Count;
        }
        return (uint)numberFormatId;
    }

    private readonly HashSet<int> _writtenNumberFormats = [];

    private uint _cellFormatCount;

    private const string DefaultStyle = "Default";
}
