using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets.Formatting;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfConditionalExtras
{
    private readonly XNamespace _pulse = "urn:pulseoffice:spreadsheet";

    private readonly TokenMap<DataBarDirection> _directions = new(
        DataBarDirection.Context, "context",
        (DataBarDirection.Context, "context"), (DataBarDirection.LeftToRight, "left-to-right"), (DataBarDirection.RightToLeft, "right-to-left"));

    public void Write(XElement element, ConditionalRule rule)
    {
        if (rule.StopIfTrue) element.Add(new XAttribute(_pulse + "stop-if-true", "true"));
        if (rule.Visual == ConditionalVisual.IconSet && rule.ReverseIcons) element.Add(new XAttribute(_pulse + "reverse-icons", "true"));
        if (rule.Visual == ConditionalVisual.Style && rule.StandardDeviations > 0)
            element.Add(new XAttribute(_pulse + "standard-deviations", rule.StandardDeviations.ToString(CultureInfo.InvariantCulture)));
        if (rule.Visual != ConditionalVisual.DataBar) return;
        if (rule.Bar.BorderColorHex is { } border) element.Add(new XAttribute(_pulse + "border-color", border.ToLowerInvariant()));
        if (rule.Bar.NegativeBorderColorHex is { } negative) element.Add(new XAttribute(_pulse + "negative-border-color", negative.ToLowerInvariant()));
        if (rule.Bar.Direction != DataBarDirection.Context) element.Add(new XAttribute(_pulse + "direction", _directions.Token(rule.Bar.Direction)));
    }

    public ConditionalRule Read(XElement element, ConditionalRule rule)
    {
        var read = rule with
        {
            StopIfTrue = (string?)element.Attribute(_pulse + "stop-if-true") is "true" or "1",
            ReverseIcons = rule.Visual == ConditionalVisual.IconSet && (string?)element.Attribute(_pulse + "reverse-icons") is "true" or "1",
            StandardDeviations = int.TryParse((string?)element.Attribute(_pulse + "standard-deviations"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var deviations)
                && deviations is > 0 and <= 3 ? deviations : rule.StandardDeviations,
        };
        if (rule.Visual != ConditionalVisual.DataBar) return read;
        return read with
        {
            Bar = read.Bar with
            {
                BorderColorHex = ((string?)element.Attribute(_pulse + "border-color"))?.ToUpperInvariant() ?? read.Bar.BorderColorHex,
                NegativeBorderColorHex = ((string?)element.Attribute(_pulse + "negative-border-color"))?.ToUpperInvariant() ?? read.Bar.NegativeBorderColorHex,
                Direction = element.Attribute(_pulse + "direction") is { } direction ? _directions.Value(direction.Value) : read.Bar.Direction,
            },
        };
    }
}
