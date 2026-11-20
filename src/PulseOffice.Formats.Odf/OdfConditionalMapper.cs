using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfConditionalMapper
{
    private readonly ConditionStyleSheet _styles = new();
    private readonly OdfRangeAddresses _addresses = new();
    private readonly OdfTableFormulas _tableFormulas = new();
    private readonly OdfConditionalExtras _extras = new();
    private readonly IReadOnlyList<ConditionalRuleCodec> _codecs;
    private readonly ConditionCodec _condition;
    private readonly StyleMapConditionText _mapConditions = new();

    public OdfConditionalMapper()
    {
        var thresholds = new OdfThresholds();
        _condition = new ConditionCodec(new OdfConditionSyntax(), _styles, _addresses);
        _codecs = [new DataBarCodec(thresholds), new ColorScaleCodec(thresholds), new IconSetCodec(thresholds), new DateIsCodec(_styles), _condition];
    }

    public IReadOnlyList<XElement> NamedStyles => _styles.Styles;

    public XElement? Build(Workbook workbook, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        if (sheet.ConditionalRules.Count == 0) return null;

        var root = new XElement(Namespaces.Calcext + "conditional-formats");
        foreach (var group in sheet.ConditionalRules.GroupBy(rule => rule.RangeText))
            root.Add(new XElement(Namespaces.Calcext + "conditional-format",
                new XAttribute(Namespaces.Calcext + "target-range-address", string.Join(' ', group.First().Ranges.Select(range => _addresses.Write(sheet.Name, range)))),
                group.OrderBy(rule => rule.Priority).Select(rule => Write(workbook, sheet, rule))));
        return root;
    }

    private XElement Write(Workbook workbook, Worksheet sheet, ConditionalRule rule)
    {
        var at = new SheetCell(workbook.IndexOf(sheet), rule.Anchor);
        var flattened = rule.WithFormulas(formula => _tableFormulas.FlattenText(formula, workbook, at));
        var element = _codecs.First(codec => codec.Writes(flattened)).Write(sheet, flattened);
        element.Add(_tableFormulas.Kept(rule.Formula, flattened.Formula));
        _extras.Write(element, rule);
        var entries = Entries(element);
        for (var index = 0; index < Math.Min(entries.Count, rule.Thresholds.Count); index++)
            entries[index].Add(_tableFormulas.KeptOne(rule.Thresholds[index].Formula, flattened.Thresholds[index].Formula));
        return element;
    }

    internal IReadOnlyList<StyleMapGroup> StyleMapGroups(Workbook workbook, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        var groups = new List<StyleMapGroup>();
        foreach (var group in sheet.ConditionalRules.GroupBy(rule => rule.RangeText))
        {
            var maps = new List<XElement>();
            foreach (var rule in group.OrderBy(rule => rule.Priority))
            {
                var element = Write(workbook, sheet, rule);
                if (element.Name != Namespaces.Calcext + "condition") continue;
                if (_mapConditions.ToStyleMapCondition((string?)element.Attribute(Namespaces.Calcext + "value")) is not { } condition) continue;
                maps.Add(new XElement(Namespaces.Style + "map",
                    new XAttribute(Namespaces.Style + "condition", condition),
                    new XAttribute(Namespaces.Style + "apply-style-name", (string?)element.Attribute(Namespaces.Calcext + "apply-style-name") ?? string.Empty),
                    new XAttribute(Namespaces.Style + "base-cell-address", (string?)element.Attribute(Namespaces.Calcext + "base-cell-address") ?? string.Empty)));
            }
            if (maps.Count > 0) groups.Add(new StyleMapGroup([.. group.First().Ranges], maps));
        }
        return groups;
    }

    private List<XElement> Entries(XElement element) =>
        [.. element.Elements().Where(child => child.Name == Namespaces.Calcext + "formatting-entry" || child.Name == Namespaces.Calcext + "color-scale-entry")];

    private ConditionalRule KeptThresholds(XElement element, ConditionalRule rule)
    {
        var entries = Entries(element);
        var thresholds = rule.Thresholds.ToList();
        var changed = false;
        for (var index = 0; index < Math.Min(entries.Count, thresholds.Count); index++)
        {
            if (_tableFormulas.Read(entries[index]) is not { } structured) continue;
            thresholds[index] = thresholds[index] with { Formula = structured };
            changed = true;
        }
        return changed ? rule with { Thresholds = thresholds } : rule;
    }

    public IReadOnlyList<ConditionalRule> ReadRules(XElement table, IReadOnlyDictionary<string, CellStyle> namedStyles)
    {
        ArgumentNullException.ThrowIfNull(table);

        var rules = new List<ConditionalRule>();
        if (table.Element(Namespaces.Calcext + "conditional-formats") is not { } root) return rules;

        foreach (var format in root.Elements(Namespaces.Calcext + "conditional-format"))
        {
            var ranges = _addresses.Read((string?)format.Attribute(Namespaces.Calcext + "target-range-address"));
            if (ranges.Count == 0) continue;

            var priority = 0;
            foreach (var element in format.Elements())
            {
                if (_codecs.FirstOrDefault(codec => codec.Element == element.Name) is not { } codec) continue;
                var rule = codec.Read(element, new RuleReadContext(ranges[0], priority++, namedStyles)) with { AdditionalRanges = [.. ranges.Skip(1)] };
                rule = _extras.Read(element, KeptThresholds(element, rule));
                rules.Add(_tableFormulas.Read(element) is { } structured ? rule with { Formula = structured } : rule);
            }
        }
        return rules;
    }

    internal IReadOnlyList<ConditionalRule> ReadStyleMaps(OdsStyleMaps maps, XElement table, string sheetName, IReadOnlyDictionary<string, CellStyle> namedStyles)
    {
        var rules = new List<ConditionalRule>();
        var priority = 0;
        foreach (var group in maps.RegionsOf(table).GroupBy(region => region.Style, StringComparer.Ordinal))
        {
            var ranges = group.Select(region => region.Range).ToList();
            foreach (var map in maps.MapsOf(group.Key))
            {
                if (_mapConditions.ToConditionValue((string?)map.Attribute(Namespaces.Style + "condition")) is not { } value) continue;
                var condition = new XElement(Namespaces.Calcext + "condition",
                    new XAttribute(Namespaces.Calcext + "apply-style-name", (string?)map.Attribute(Namespaces.Style + "apply-style-name") ?? string.Empty),
                    new XAttribute(Namespaces.Calcext + "value", value));
                var baseCell = (string?)map.Attribute(Namespaces.Style + "base-cell-address");
                condition.SetAttributeValue(Namespaces.Calcext + "base-cell-address", baseCell ?? _addresses.WriteAddress(sheetName, ranges[0].Start));
                rules.Add(_condition.Read(condition, new RuleReadContext(ranges[0], priority++, namedStyles)) with { AdditionalRanges = [.. ranges.Skip(1)] });
            }
        }
        return rules;
    }
}
