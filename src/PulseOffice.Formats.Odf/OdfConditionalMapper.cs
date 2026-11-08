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
    private readonly IReadOnlyList<ConditionalRuleCodec> _codecs;

    public OdfConditionalMapper()
    {
        var thresholds = new OdfThresholds();
        _codecs = [new DataBarCodec(thresholds), new ColorScaleCodec(thresholds), new IconSetCodec(thresholds), new DateIsCodec(_styles), new ConditionCodec(new OdfConditionSyntax(), _styles, _addresses)];
    }

    public IReadOnlyList<XElement> NamedStyles => _styles.Styles;

    public XElement? Build(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (sheet.ConditionalRules.Count == 0) return null;

        var root = new XElement(Namespaces.Calcext + "conditional-formats");
        foreach (var group in sheet.ConditionalRules.GroupBy(rule => rule.RangeText))
            root.Add(new XElement(Namespaces.Calcext + "conditional-format",
                new XAttribute(Namespaces.Calcext + "target-range-address", string.Join(' ', group.First().Ranges.Select(range => _addresses.Write(sheet.Name, range)))),
                group.OrderBy(rule => rule.Priority).Select(rule => _codecs.First(codec => codec.Writes(rule)).Write(sheet, rule))));
        return root;
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
                rules.Add(codec.Read(element, new RuleReadContext(ranges[0], priority++, namedStyles)) with { AdditionalRanges = [.. ranges.Skip(1)] });
            }
        }
        return rules;
    }
}
