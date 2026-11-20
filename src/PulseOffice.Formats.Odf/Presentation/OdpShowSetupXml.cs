using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpShowSetupXml
{
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Pulse = "urn:pulseoffice:presentation-show";

    public void Write(XElement settings, ShowSetup setup, int slideCount, Func<int, string> pageName)
    {
        if (setup.Kind == ShowKind.Window) settings.Add(new XAttribute(Presentation + "full-screen", "false"));
        if (setup.Kind == ShowKind.Kiosk && settings.Attribute(Presentation + "endless") is null)
            settings.Add(new XAttribute(Presentation + "endless", "true"));
        if (setup.LoopPauseMs > 0)
            settings.Add(new XAttribute(Presentation + "pause", System.Xml.XmlConvert.ToString(TimeSpan.FromMilliseconds(setup.LoopPauseMs))));
        if (!string.IsNullOrEmpty(setup.CustomShowName)) settings.Add(new XAttribute(Presentation + "show", setup.CustomShowName));

        var own = new List<XAttribute>();
        if (setup.Kind == ShowKind.Kiosk) own.Add(new XAttribute(Pulse + "kind", "kiosk"));
        if (setup.Kind == ShowKind.Kiosk && !setup.Loop) own.Add(new XAttribute(Pulse + "loop", "false"));
        if (setup.ToSlide > 0 && setup.ToSlide <= slideCount) own.Add(new XAttribute(Pulse + "end-page", pageName(setup.ToSlide)));
        if (setup.KioskRestartMs != ShowSetup.DefaultKioskRestartMs)
            own.Add(new XAttribute(Pulse + "kiosk-restart", System.Xml.XmlConvert.ToString(TimeSpan.FromMilliseconds(Math.Max(0, setup.KioskRestartMs)))));
        if (!string.Equals(setup.PenColor, new ShowSetup().PenColor, StringComparison.OrdinalIgnoreCase))
            own.Add(new XAttribute(Pulse + "pen-color", setup.PenColor));
        if (own.Count == 0) return;
        if (settings.Attribute(XNamespace.Xmlns + "pulseshow") is null)
            settings.Add(new XAttribute(XNamespace.Xmlns + "pulseshow", Pulse.NamespaceName));
        settings.Add(own);
    }

    public ShowSetup Read(XElement settings, ShowSetup setup, Func<string?, int> pageNumber)
    {
        var kiosk = (string?)settings.Attribute(Pulse + "kind") == "kiosk";
        var end = pageNumber((string?)settings.Attribute(Pulse + "end-page"));
        var pen = (string?)settings.Attribute(Pulse + "pen-color");
        return setup with
        {
            Kind = kiosk ? ShowKind.Kiosk
                : (string?)settings.Attribute(Presentation + "full-screen") == "false" ? ShowKind.Window
                : ShowKind.Speaker,
            Loop = setup.Loop && (string?)settings.Attribute(Pulse + "loop") != "false",
            CustomShowName = (string?)settings.Attribute(Presentation + "show") is { Length: > 0 } show ? show : null,
            ToSlide = end > 0 ? end : setup.ToSlide,
            LoopPauseMs = Milliseconds((string?)settings.Attribute(Presentation + "pause")) ?? 0,
            KioskRestartMs = Milliseconds((string?)settings.Attribute(Pulse + "kiosk-restart")) ?? ShowSetup.DefaultKioskRestartMs,
            PenColor = pen is { Length: 7 } && pen[0] == '#' ? pen.ToUpperInvariant() : setup.PenColor,
        };
    }

    private int? Milliseconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return null;
        try
        {
            var span = System.Xml.XmlConvert.ToTimeSpan(duration.Trim());
            return span < TimeSpan.Zero ? null : (int)Math.Min(span.TotalMilliseconds, int.MaxValue);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
