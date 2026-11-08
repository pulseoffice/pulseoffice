using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal sealed class OdfTabStops : OdfXml
{
    private readonly TokenMap<TabAlignment> _alignments = new(TabAlignment.Left, "left",
        (TabAlignment.Left, "left"), (TabAlignment.Center, "center"), (TabAlignment.Right, "right"), (TabAlignment.Decimal, "char"));

    private readonly TokenMap<TabLeader> _leaders = new(TabLeader.None, "none",
        (TabLeader.None, "none"), (TabLeader.Dots, "dotted"), (TabLeader.Dashes, "dash"), (TabLeader.Underline, "solid"));

    public XElement Write(IReadOnlyList<TabStop> stops) =>
        new(Style + "tab-stops", stops.Select(stop =>
        {
            var tab = new XElement(Style + "tab-stop", new XAttribute(Style + "position", stop.Position.ToString("0.####", CultureInfo.InvariantCulture) + "pt"));
            if (stop.Alignment != TabAlignment.Left) tab.Add(new XAttribute(Style + "type", _alignments.Token(stop.Alignment)));
            if (stop.Alignment == TabAlignment.Decimal) tab.Add(new XAttribute(Style + "char", stop.DecimalCharacter.ToString()));
            if (stop.Leader != TabLeader.None)
            {
                tab.Add(new XAttribute(Style + "leader-style", _leaders.Token(stop.Leader)));
                tab.Add(new XAttribute(Style + "leader-text", stop.Leader == TabLeader.Dots ? "." : stop.Leader == TabLeader.Dashes ? "-" : "_"));
            }
            return tab;
        }));

    public IReadOnlyList<TabStop>? Read(XElement? paragraphProperties, Func<string?, double?> points) =>
        paragraphProperties?.Element(Style + "tab-stops") is { } tabs
            ? [.. tabs.Elements(Style + "tab-stop").Select(tab => new TabStop(Math.Max(0, points((string?)tab.Attribute(Style + "position")) ?? 0))
            {
                Alignment = _alignments.Value((string?)tab.Attribute(Style + "type")),
                DecimalCharacter = (string?)tab.Attribute(Style + "char") is { Length: > 0 } c ? c[0] : '.',
                Leader = _leaders.Value((string?)tab.Attribute(Style + "leader-style")),
            })]
            : null;
}
