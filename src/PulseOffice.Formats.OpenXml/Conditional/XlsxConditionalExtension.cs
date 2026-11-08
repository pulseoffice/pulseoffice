using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal interface IWorksheetExtensionSource
{
    WorksheetExtension? Extension(Worksheet sheet);
}

internal sealed class XlsxConditionalExtension(
    XlsxIconSets sets, X14Namespaces names, X14Thresholds thresholds, X14DataBarCodec bars, X14RuleIds ids, IReadOnlyList<IX14RuleReader> readers)
    : IWorksheetExtensionSource
{
    public const string Uri = "{78C0D931-6437-407d-A8EE-F0AAD7539E65}";

    private XNamespace X14 => names.X14;

    private XNamespace Xm => names.Xm;

    public WorksheetExtension? Extension(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var rules = sheet.ConditionalRules.Where(rule => sets.IsExtended(rule) || rule.Visual == ConditionalVisual.DataBar).ToList();
        if (rules.Count == 0) return null;

        var container = new XElement(X14 + "conditionalFormattings",
            new XAttribute(XNamespace.Xmlns + "x14", X14), new XAttribute(XNamespace.Xmlns + "xm", Xm),
            rules.Select(rule => rule.Visual == ConditionalVisual.DataBar ? bars.Write(rule, ids.For(rule)) : Formatting(rule)));

        return new WorksheetExtension { Uri = Uri, InnerXml = container.ToString(SaveOptions.DisableFormatting) };
    }

    public IEnumerable<ConditionalRule> Read(WorksheetExtensionList? extensions)
    {
        foreach (var (rule, ranges) in Rules(extensions))
            if (readers.FirstOrDefault(reader => reader.Reads((string?)rule.Attribute("type") ?? string.Empty)) is { } reader
                && reader.Read(rule, ranges) is { } read)
                yield return read;
    }

    public X14DataBarLinks DataBars(WorksheetExtensionList? extensions)
    {
        var entries = new Dictionary<string, X14DataBarEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rule, ranges) in Rules(extensions))
            if ((string?)rule.Attribute("type") == "dataBar" && (string?)rule.Attribute("id") is { Length: > 0 } id)
                entries.TryAdd(id, new X14DataBarEntry(rule, ranges));
        return new X14DataBarLinks(entries);
    }

    public ConditionalRule ReadDataBar(X14DataBarEntry entry) => bars.Read(entry);

    public ConditionalRule ApplyDataBar(ConditionalRule rule, X14DataBarEntry entry) => bars.Apply(rule, entry.Rule);

    private IEnumerable<(XElement Rule, IReadOnlyList<CellRange> Ranges)> Rules(WorksheetExtensionList? extensions)
    {
        if (extensions is null) yield break;

        foreach (var extension in extensions.Elements<WorksheetExtension>().Where(extension => extension.Uri?.Value == Uri))
        {
            XElement root;
            try { root = XElement.Parse(extension.OuterXml); }
            catch (System.Xml.XmlException) { continue; }

            foreach (var formatting in root.Descendants(X14 + "conditionalFormatting"))
            {
                var ranges = names.Ranges(formatting);
                if (ranges.Count == 0) continue;

                foreach (var rule in formatting.Elements(X14 + "cfRule"))
                    yield return (rule, ranges);
            }
        }
    }

    private XElement Formatting(ConditionalRule rule)
    {
        var count = rule.IconCount;
        var stops = rule.Thresholds.Count == count
            ? rule.Thresholds
            : [.. Enumerable.Range(0, count).Select(index => new ConditionalThreshold(ConditionalThresholdKind.Percent, 100.0 * index / count))];

        var set = new XElement(X14 + "iconSet", new XAttribute("iconSet", rule.IconSetName));
        if (rule.ReverseIcons) set.Add(new XAttribute("reverse", "1"));
        if (!rule.ShowValue) set.Add(new XAttribute("showValue", "0"));
        set.Add(stops.Select(thresholds.Write));
        if (rule.HasCustomIcons)
        {
            set.Add(new XAttribute("custom", "1"));
            set.Add(rule.CustomIcons.Select(icon => new XElement(X14 + "cfIcon",
                new XAttribute("iconSet", icon.IsNone ? ConditionalIconChoice.NoIcons : icon.Set),
                new XAttribute("iconId", (icon.IsNone ? 0 : icon.Index).ToString(CultureInfo.InvariantCulture)))));
        }

        return new XElement(X14 + "conditionalFormatting",
            new XElement(X14 + "cfRule",
                new XAttribute("type", "iconSet"),
                new XAttribute("priority", ((long)rule.Priority + 1).ToString(CultureInfo.InvariantCulture)),
                new XAttribute("id", ids.For(rule)),
                set),
            new XElement(Xm + "sqref", rule.RangeText));
    }
}
