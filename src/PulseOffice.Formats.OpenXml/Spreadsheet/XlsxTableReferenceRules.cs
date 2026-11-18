using System.Xml.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Book = PulseOffice.Sheets.Workbook;
using Rule = PulseOffice.Sheets.DataValidation;
using Sheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxTableReferenceRules(Book workbook, ITableReferenceFlattening flattening)
{
    public const string Uri = "{C4E1A7D2-5B39-4F86-9A0E-3D2B7C61E84F}";

    private readonly XNamespace _pulse = "urn:pulseoffice:spreadsheet";

    private readonly Dictionary<Sheet, IReadOnlyList<ConditionalRule>> _rules = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Sheet, IReadOnlyList<Rule>> _validations = new(ReferenceEqualityComparer.Instance);

    public XlsxTableReferenceRules(Book workbook) : this(workbook, new TableReferenceFlattening()) { }

    public IReadOnlyList<ConditionalRule> Rules(Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (_rules.TryGetValue(sheet, out var known)) return known;
        var index = workbook.IndexOf(sheet);
        var flattened = sheet.ConditionalRules
            .Select(rule => rule.WithFormulas(formula => Flatten(formula, new SheetCell(index, rule.Anchor))))
            .Select((rule, position) => rule.SameFormulas(sheet.ConditionalRules[position]) ? sheet.ConditionalRules[position] : rule)
            .ToList();
        _rules[sheet] = flattened;
        return flattened;
    }

    public IReadOnlyList<Rule> Validations(Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (_validations.TryGetValue(sheet, out var known)) return known;
        var index = workbook.IndexOf(sheet);
        var flattened = sheet.Validations.Select(validation =>
        {
            var at = new SheetCell(index, validation.Range.Start);
            var rewritten = validation with { FirstFormula = Flatten(validation.FirstFormula, at), SecondFormula = Flatten(validation.SecondFormula, at) };
            return rewritten == validation ? validation : rewritten;
        }).ToList();
        _validations[sheet] = flattened;
        return flattened;
    }

    public WorksheetExtension? Extension(Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var rules = Rules(sheet);
        var validations = Validations(sheet);
        var root = new XElement(_pulse + "tableReferences", new XAttribute(XNamespace.Xmlns + "pt", _pulse.NamespaceName));

        for (var index = 0; index < rules.Count; index++)
        {
            var structured = sheet.ConditionalRules[index];
            if (ReferenceEquals(structured, rules[index])) continue;
            root.Add(new XElement(_pulse + "rule",
                new XAttribute("range", structured.RangeText),
                Pair("flat", rules[index].Formula, "formula", structured.Formula),
                structured.Thresholds.Select((threshold, position) => new XElement(_pulse + "threshold",
                    Pair("flat", rules[index].Thresholds[position].Formula, "formula", threshold.Formula)))));
        }

        for (var index = 0; index < validations.Count; index++)
        {
            var structured = sheet.Validations[index];
            if (ReferenceEquals(structured, validations[index])) continue;
            root.Add(new XElement(_pulse + "validation",
                new XAttribute("range", structured.Range.ToString()),
                Pair("flat", validations[index].FirstFormula, "formula", structured.FirstFormula),
                Pair("secondFlat", validations[index].SecondFormula, "second", structured.SecondFormula)));
        }

        return root.HasElements ? new WorksheetExtension { Uri = Uri, InnerXml = root.ToString(SaveOptions.DisableFormatting) } : null;
    }

    public void Restore(WorksheetExtensionList? extensions, Sheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (extensions is null) return;
        foreach (var extension in extensions.Elements<WorksheetExtension>().Where(extension => extension.Uri?.Value == Uri))
        {
            XElement root;
            try { root = XElement.Parse(extension.OuterXml); }
            catch (System.Xml.XmlException) { continue; }
            RestoreRules(root, sheet);
            RestoreValidations(root, sheet);
        }
    }

    private void RestoreRules(XElement root, Sheet sheet)
    {
        var used = new HashSet<int>();
        foreach (var entry in root.Descendants(_pulse + "rule"))
        {
            var thresholds = entry.Elements(_pulse + "threshold").ToList();
            for (var index = 0; index < sheet.ConditionalRules.Count; index++)
            {
                var rule = sheet.ConditionalRules[index];
                if (used.Contains(index) || rule.RangeText != (string?)entry.Attribute("range")) continue;
                if (!Same(rule.Formula, (string?)entry.Attribute("flat")) || rule.Thresholds.Count != thresholds.Count) continue;
                if (!rule.Thresholds.Select((threshold, position) => Same(threshold.Formula, (string?)thresholds[position].Attribute("flat"))).All(same => same)) continue;

                used.Add(index);
                sheet.ConditionalRules[index] = rule with
                {
                    Formula = (string?)entry.Attribute("formula") ?? rule.Formula,
                    Thresholds = [.. rule.Thresholds.Select((threshold, position) =>
                        (string?)thresholds[position].Attribute("formula") is { } formula ? threshold with { Formula = formula } : threshold)],
                };
                break;
            }
        }
    }

    private void RestoreValidations(XElement root, Sheet sheet)
    {
        var used = new HashSet<int>();
        foreach (var entry in root.Descendants(_pulse + "validation"))
            for (var index = 0; index < sheet.Validations.Count; index++)
            {
                var validation = sheet.Validations[index];
                if (used.Contains(index) || validation.Range.ToString() != (string?)entry.Attribute("range")) continue;
                if (!Same(validation.FirstFormula, (string?)entry.Attribute("flat")) || !Same(validation.SecondFormula, (string?)entry.Attribute("secondFlat"))) continue;

                used.Add(index);
                sheet.Validations[index] = validation with
                {
                    FirstFormula = (string?)entry.Attribute("formula") ?? validation.FirstFormula,
                    SecondFormula = (string?)entry.Attribute("second") ?? validation.SecondFormula,
                };
                break;
            }
    }

    private IEnumerable<XAttribute> Pair(string flatName, string? flat, string formulaName, string? formula)
    {
        if (string.Equals(flat, formula, StringComparison.Ordinal) || formula is null) yield break;
        if (flat is not null) yield return new XAttribute(flatName, flat);
        yield return new XAttribute(formulaName, formula);
    }

    private bool Same(string? read, string? flat) =>
        flat is null || string.Equals(Normal(read), Normal(flat), StringComparison.OrdinalIgnoreCase);

    private string Normal(string? formula) =>
        (formula ?? string.Empty).Replace("$", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).TrimStart('=');

    private string? Flatten(string? formula, SheetCell at)
    {
        if (string.IsNullOrEmpty(formula) || !formula.Contains('[', StringComparison.Ordinal)) return formula;
        var prefixed = formula.StartsWith('=');
        var flattened = flattening.Flatten(prefixed ? formula : "=" + formula, workbook, at, _ => false);
        return prefixed ? flattened : flattened[1..];
    }
}
