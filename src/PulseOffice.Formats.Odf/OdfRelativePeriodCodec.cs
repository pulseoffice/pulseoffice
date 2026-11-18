using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfRelativePeriodCodec
{
    private readonly XName _period = XNamespace.Get("urn:pulseoffice:spreadsheet") + "relative-period";

    private readonly RelativeDatePeriods _periods = new();

    public IEnumerable<XElement> Write(int column, RelativeDatePeriod period, DateTime today)
    {
        var bounds = _periods.Bounds(period, today);
        var token = Token(period);
        yield return Bound(column, ">=", bounds.Start, token);
        yield return Bound(column, "<", bounds.End, token);
    }

    public RelativeDatePeriod? Read(XElement condition)
    {
        if ((string?)condition.Attribute(_period) is not { Length: > 0 } token) return null;
        foreach (var period in Enum.GetValues<RelativeDatePeriod>())
            if (Token(period) == token) return period;
        return null;
    }

    private XElement Bound(int column, string op, DateTime moment, string token) =>
        new(Namespaces.Table + "filter-condition",
            new XAttribute(Namespaces.Table + "field-number", column),
            new XAttribute(Namespaces.Table + "value", moment.ToOADate().ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute(Namespaces.Table + "operator", op),
            new XAttribute(Namespaces.Table + "data-type", "number"),
            new XAttribute(_period, token));

    private string Token(RelativeDatePeriod period)
    {
        var name = period.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
