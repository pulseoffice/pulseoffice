using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfCalculationSettingsMapper
{
    public CalculationSettings Read(XElement spreadsheet)
    {
        var settings = spreadsheet.Element(Namespaces.Table + "calculation-settings");
        var iteration = settings?.Element(Namespaces.Table + "iteration");
        return new CalculationSettings
        {
            CaseSensitive = Flag(settings, "case-sensitive", true),
            AutomaticFindLabels = Flag(settings, "automatic-find-labels", true),
            WholeCellCriteria = Flag(settings, "search-criteria-must-apply-to-whole-cell", true),
            Criteria = Flag(settings, "use-wildcards", false) ? CriteriaSyntax.Wildcards
                : Flag(settings, "use-regular-expressions", true) ? CriteriaSyntax.RegularExpressions
                : CriteriaSyntax.Literal,
            Iterative = iteration?.Attribute(Namespaces.Table + "status")?.Value == "enable",
            MaxIterations = int.TryParse(iteration?.Attribute(Namespaces.Table + "steps")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var steps) && steps > 0
                ? steps : CalculationSettings.DefaultMaxIterations,
            MaxChange = double.TryParse(iteration?.Attribute(Namespaces.Table + "minimum-difference")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var change) && change >= 0
                ? change : CalculationSettings.DefaultMaxChange,
        };
    }

    public XElement Build(CalculationSettings settings)
    {
        var element = new XElement(Namespaces.Table + "calculation-settings",
            new XAttribute(Namespaces.Table + "case-sensitive", Text(settings.CaseSensitive)),
            new XAttribute(Namespaces.Table + "search-criteria-must-apply-to-whole-cell", Text(settings.WholeCellCriteria)),
            new XAttribute(Namespaces.Table + "automatic-find-labels", Text(settings.AutomaticFindLabels)),
            new XAttribute(Namespaces.Table + "use-regular-expressions", Text(settings.Criteria == CriteriaSyntax.RegularExpressions)),
            new XAttribute(Namespaces.Table + "use-wildcards", Text(settings.Criteria == CriteriaSyntax.Wildcards)));
        if (settings.Iterative || settings.MaxIterations != CalculationSettings.DefaultMaxIterations || settings.MaxChange != CalculationSettings.DefaultMaxChange)
            element.Add(new XElement(Namespaces.Table + "iteration",
                new XAttribute(Namespaces.Table + "status", settings.Iterative ? "enable" : "disable"),
                new XAttribute(Namespaces.Table + "steps", settings.MaxIterations.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Table + "minimum-difference", settings.MaxChange.ToString("R", CultureInfo.InvariantCulture))));
        return element;
    }

    private bool Flag(XElement? settings, string name, bool fallback) =>
        settings?.Attribute(Namespaces.Table + name)?.Value is { } value ? value == "true" : fallback;

    private string Text(bool value) => value ? "true" : "false";
}
