using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxConditionalMapper
{
    private readonly DifferentialFormatTable _formats;
    private readonly IReadOnlyList<XlsxRuleCodec> _codecs;
    private readonly XlsxIconSets _sets = new();
    private readonly XlsxConditionalExtension _extension;
    private readonly XlsxDataBarCodec _bars;

    public XlsxConditionalMapper()
    {
        var colours = new SpreadsheetColours();
        _formats = new DifferentialFormatTable(colours);
        var formulas = new XlsxFormulaSyntax();
        var names = new X14Namespaces();
        var x14Thresholds = new X14Thresholds(names, formulas, new X14ThresholdVocabulary().Kinds);
        var ids = new X14RuleIds();
        var comparisons = new XlsxComparisonOperands(formulas);
        _extension = new XlsxConditionalExtension(_sets, names, x14Thresholds, new X14DataBarCodec(names, x14Thresholds, colours), ids,
            [new X14IconSetRuleReader(names, x14Thresholds), new X14StyledRuleReader(names, formulas, _formats, comparisons)]);
        var thresholds = new XlsxThresholds();
        _bars = new XlsxDataBarCodec(colours, thresholds, ids);
        _codecs =
        [
            _bars, new XlsxColorScaleCodec(colours, thresholds), new XlsxIconSetCodec(colours, thresholds, _sets),
            new XlsxDuplicateCodec(colours, _formats), new XlsxTopItemsCodec(colours, _formats),
            new XlsxAverageCodec(colours, _formats), new XlsxTimePeriodCodec(colours, _formats),
            new XlsxExpressionCodec(colours, _formats), new XlsxOperatorCodec(colours, _formats, comparisons),
        ];
    }

    public DifferentialFormats Build() => _formats.Element;

    public bool HasFormats => _formats.Any;

    public uint InternColour(string hex) => _formats.InternColour(hex);

    public IEnumerable<ConditionalFormatting> Build(IEnumerable<ConditionalRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var group in rules.Where(rule => !_sets.IsExtended(rule)).GroupBy(rule => rule.RangeText))
        {
            var formatting = new ConditionalFormatting { SequenceOfReferences = new ListValue<StringValue> { InnerText = group.Key } };
            foreach (var rule in group.OrderBy(rule => rule.Priority))
                formatting.AppendChild(_codecs.First(codec => codec.Writes(rule)).Write(rule));
            yield return formatting;
        }
    }

    public IReadOnlyList<ConditionalRule> ReadRules(Worksheet worksheet, Stylesheet? stylesheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);

        var formats = stylesheet?.DifferentialFormats?.Elements<DifferentialFormat>().ToList() ?? [];
        var rules = new List<ConditionalRule>();
        var extensions = worksheet.GetFirstChild<WorksheetExtensionList>();
        var bars = _extension.DataBars(extensions);

        foreach (var formatting in worksheet.Elements<ConditionalFormatting>())
        {
            var reference = formatting.SequenceOfReferences?.InnerText;
            var ranges = (reference ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(text => CellRange.TryParseA1(text, out var parsed) ? parsed : (CellRange?)null)
                .OfType<CellRange>()
                .ToList();
            if (ranges.Count == 0) continue;

            var context = new XlsxRuleReadContext(ranges[0], formats, _formats);
            foreach (var element in formatting.Elements<ConditionalFormattingRule>())
            {
                var rule = _codecs.First(codec => codec.Reads(element)).Read(element, context) with { AdditionalRanges = [.. ranges.Skip(1)] };
                if (rule.Visual == ConditionalVisual.DataBar && bars.Take(_bars.ExtensionId(element)) is { } entry)
                    rule = _extension.ApplyDataBar(rule, entry);
                rules.Add(rule);
            }
        }
        rules.AddRange(bars.Unused.Select(_extension.ReadDataBar));
        rules.AddRange(_extension.Read(extensions));
        return rules;
    }

    internal IWorksheetExtensionSource Extensions => _extension;
}
