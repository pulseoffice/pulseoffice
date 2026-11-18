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
                new XAttribute("priority", structured.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute("ordinal", RuleOrdinal(sheet.ConditionalRules, index).ToString(System.Globalization.CultureInfo.InvariantCulture)),
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
                new XAttribute("ordinal", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
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
        var ambiguous = new List<XElement>();
        foreach (var entry in root.Descendants(_pulse + "rule"))
        {
            var candidates = Candidates(sheet, entry, used);
            if (candidates.Count == 1) Apply(sheet, candidates[0], entry, used);
            else if (candidates.Count > 1) ambiguous.Add(entry);
        }

        foreach (var entry in ambiguous)
        {
            var candidates = Candidates(sheet, entry, used);
            if (candidates.Count == 0) continue;
            var byId = RuleById(sheet.ConditionalRules, entry);
            Apply(sheet, candidates.Contains(byId) ? byId : candidates[0], entry, used);
        }
    }

    private List<int> Candidates(Sheet sheet, XElement entry, HashSet<int> used) =>
        [.. Enumerable.Range(0, sheet.ConditionalRules.Count).Where(index => !used.Contains(index) && RuleMatches(sheet.ConditionalRules[index], entry))];

    private void Apply(Sheet sheet, int index, XElement entry, HashSet<int> used)
    {
        used.Add(index);
        var rule = sheet.ConditionalRules[index];
        var thresholds = entry.Elements(_pulse + "threshold").ToList();
        sheet.ConditionalRules[index] = rule with
        {
            Formula = (string?)entry.Attribute("formula") ?? rule.Formula,
            Thresholds = [.. rule.Thresholds.Select((threshold, position) =>
                (string?)thresholds[position].Attribute("formula") is { } formula ? threshold with { Formula = formula } : threshold)],
        };
    }

    private bool RuleMatches(ConditionalRule rule, XElement entry)
    {
        var thresholds = entry.Elements(_pulse + "threshold").ToList();
        if (rule.RangeText != (string?)entry.Attribute("range")) return false;
        if (!Same(rule.Formula, (string?)entry.Attribute("flat")) || rule.Thresholds.Count != thresholds.Count) return false;
        return rule.Thresholds.Select((threshold, position) => Same(threshold.Formula, (string?)thresholds[position].Attribute("flat"))).All(same => same);
    }

    private int RuleById(IList<ConditionalRule> rules, XElement entry)
    {
        if (!int.TryParse((string?)entry.Attribute("priority"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var priority)) return -1;
        if (!int.TryParse((string?)entry.Attribute("ordinal"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ordinal)) return -1;
        var range = (string?)entry.Attribute("range");
        var seen = 0;
        for (var index = 0; index < rules.Count; index++)
        {
            if (rules[index].RangeText != range || rules[index].Priority != priority) continue;
            if (seen++ == ordinal) return index;
        }
        return -1;
    }

    private int RuleOrdinal(IList<ConditionalRule> rules, int position)
    {
        var rule = rules[position];
        var ordinal = 0;
        for (var index = 0; index < position; index++)
            if (rules[index].RangeText == rule.RangeText && rules[index].Priority == rule.Priority) ordinal++;
        return ordinal;
    }

    private void RestoreValidations(XElement root, Sheet sheet)
    {
        var used = new HashSet<int>();
        var pending = new List<XElement>();
        foreach (var entry in root.Descendants(_pulse + "validation"))
        {
            var index = int.TryParse((string?)entry.Attribute("ordinal"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ordinal) ? ordinal : -1;
            if (index >= 0 && index < sheet.Validations.Count && !used.Contains(index) && ValidationMatches(sheet.Validations[index], entry)) Apply(sheet.Validations, index, entry, used);
            else pending.Add(entry);
        }

        foreach (var entry in pending)
            for (var index = 0; index < sheet.Validations.Count; index++)
            {
                if (used.Contains(index) || !ValidationMatches(sheet.Validations[index], entry)) continue;
                Apply(sheet.Validations, index, entry, used);
                break;
            }
    }

    private bool ValidationMatches(Rule validation, XElement entry) =>
        validation.Range.ToString() == (string?)entry.Attribute("range")
        && Same(validation.FirstFormula, (string?)entry.Attribute("flat"))
        && Same(validation.SecondFormula, (string?)entry.Attribute("secondFlat"));

    private void Apply(IList<Rule> validations, int index, XElement entry, HashSet<int> used)
    {
        used.Add(index);
        var validation = validations[index];
        validations[index] = validation with
        {
            FirstFormula = (string?)entry.Attribute("formula") ?? validation.FirstFormula,
            SecondFormula = (string?)entry.Attribute("second") ?? validation.SecondFormula,
        };
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
