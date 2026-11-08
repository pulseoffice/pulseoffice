using PulseOffice.Document.Versions;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed record DefinedName(string Name, string Expression, string? Scope = null);

public sealed class Workbook : Security.IMacroSignatureHolder, PulseOffice.Document.Forms.IFormDocument, PulseOffice.Document.Fonts.IFontDocument, PulseOffice.Document.Forms.IMacroDocument
{
    private readonly List<Worksheet> _sheets = [];
    private readonly Dictionary<DefinedNameKey, DefinedName> _names = [];
    private readonly Dictionary<Worksheet, List<DefinedName>> _detachedNames = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<Worksheet> Sheets => _sheets;

    public Styling.StyleCatalog Styles { get; } = new();

    public Document.Metadata.DocumentProperties Properties { get; } = new();

    public DatabaseRangeCollection DatabaseRanges { get; } = new();

    public ChangeLog Changes { get; } = new();

    public DocumentVersionCollection Versions { get; } = new();

    public List<Pivot.PivotDefinition> Pivots { get; } = [];

    public ExternalDataCatalog ExternalData { get; } = new();

    public Document.Forms.IMacroLibrary Macros { get; } = new Document.Forms.MacroLibrary();

    public string? DefaultLanguage { get; set; }

    public string? TextLanguage => DefaultLanguage ?? Properties.Language;

    public WorkbookProtection Protection { get; set; } = WorkbookProtection.None;

    public CalculationSettings Calculation { get; set; } = CalculationSettings.Default;

    public ConsolidationSettings? Consolidation { get; set; }

    public Security.MacroSignature? MacroSignature { get; set; }

    public PulseOffice.Document.Forms.FormDocumentSettings FormSettings { get; set; } = new();

    public PulseOffice.Document.Fonts.DocumentFonts Fonts { get; set; } = PulseOffice.Document.Fonts.DocumentFonts.Empty;

    public IReadOnlyList<(string Name, string Language, string Source)> SignableMacros => [.. Macros.All.Select(macro => (macro.Name, macro.Language.ToString(), macro.Source))];

    public NamedStyleCatalog NamedStyles { get; }

    public PageStyleCatalog PageStyles { get; } = new();

    public Formatting.NumberFormatCatalog NumberFormats { get; } = new();
    public IReadOnlyCollection<DefinedName> DefinedNames => _names.Values;

    public event Action<Worksheet, CellAddress>? CellContentChanged;

    private Worksheet Attach(Worksheet sheet)
    {
        sheet.ContentChanged -= Relay;
        sheet.ContentChanged += Relay;
        sheet.RowVisibilityChanged -= RelayVisibility;
        sheet.RowVisibilityChanged += RelayVisibility;
        sheet.Cleared -= RelayCleared;
        sheet.Cleared += RelayCleared;
        sheet.Owner = this;
        return sheet;
    }

    private void Relay(Worksheet sheet, CellAddress address) => CellContentChanged?.Invoke(sheet, address);

    public event Action<Worksheet>? RowVisibilityChanged;

    private void RelayVisibility(Worksheet sheet) => RowVisibilityChanged?.Invoke(sheet);

    public event Action<Worksheet>? SheetCleared;

    private void RelayCleared(Worksheet sheet) => SheetCleared?.Invoke(sheet);

    public Worksheet AddSheet(string name)
    {
        if (_sheets.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A sheet named '{name}' already exists.");
        var sheet = Attach(new Worksheet(name));
        _sheets.Add(sheet);
        return sheet;
    }

    public Worksheet? FindSheet(string name) =>
        _sheets.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public int IndexOf(Worksheet sheet) => _sheets.IndexOf(sheet);

    public Worksheet InsertSheet(string name, int index)
    {
        if (_sheets.Any(sheet => string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A sheet named '{name}' already exists.");

        var created = Attach(new Worksheet(name));

        var position = Math.Clamp(index, 0, _sheets.Count);
        _sheets.Insert(position, created);
        ShiftIndexed(old => old >= position ? old + 1 : old);
        return created;
    }

    public void InsertSheet(Worksheet sheet, int index)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var position = Math.Clamp(index, 0, _sheets.Count);
        _sheets.Insert(position, Attach(sheet));
        var consolidationBefore = Consolidation;
        ShiftIndexed(old => old >= position ? old + 1 : old);
        if (_detachedData.Remove(sheet, out var data))
        {
            ExternalData.Attach(data.External, position);
            foreach (var range in data.Ranges)
                if (DatabaseRanges.Find(range.Name) is null) DatabaseRanges.Define(range with { SheetIndex = position });
            Changes.Reattach(data.Changes, position);
        }

        if (_detachedConsolidation.Remove(sheet, out var consolidation) && consolidationBefore == consolidation.After)
            Consolidation = consolidation.Before;

        if (!_detachedNames.Remove(sheet, out var names)) return;
        foreach (var name in names) _names[DefinedNameKey.Of(name.Name, sheet.Name)] = name with { Scope = sheet.Name };
        if (names.Count > 0) NamesChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveSheet(Worksheet sheet)
    {
        var removed = _sheets.IndexOf(sheet);
        if (removed < 0) return false;
        _sheets.RemoveAt(removed);
        Func<int, int> map = old => old == removed ? -1 : old > removed ? old - 1 : old;
        _detachedData[sheet] = (ExternalData.Reindex(map), DatabaseRanges.Reindex(map), Changes.ReindexSheets(map));
        ReindexSheetFilters(map);
        var consolidationBefore = Consolidation;
        Consolidation = Consolidation?.Reindexed(map);
        if (consolidationBefore != Consolidation) _detachedConsolidation[sheet] = (consolidationBefore, Consolidation);
        sheet.ContentChanged -= Relay;
        sheet.RowVisibilityChanged -= RelayVisibility;
        sheet.Cleared -= RelayCleared;
        sheet.Owner = null;

        var local = _names.Where(pair => string.Equals(pair.Value.Scope, sheet.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var (key, _) in local) _names.Remove(key);
        _detachedNames[sheet] = [.. local.Select(pair => pair.Value)];
        if (local.Count > 0) NamesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool MoveSheet(int from, int to)
    {
        if (from < 0 || from >= _sheets.Count) return false;
        if (to < 0 || to >= _sheets.Count || to == from) return false;

        var sheet = _sheets[from];

        _sheets.RemoveAt(from);
        _sheets.Insert(to, sheet);
        ShiftIndexed(old => old == from ? to
            : from < to && old > from && old <= to ? old - 1
            : to < from && old >= to && old < from ? old + 1
            : old);

        return true;
    }

    private readonly Dictionary<Worksheet, (ExternalDataMemento External, IReadOnlyList<DatabaseRange> Ranges, IReadOnlyList<TrackedChange> Changes)> _detachedData = [];

    private readonly Dictionary<Worksheet, (ConsolidationSettings? Before, ConsolidationSettings? After)> _detachedConsolidation = [];

    private void ShiftIndexed(Func<int, int> map)
    {
        Consolidation = Consolidation?.Reindexed(map);
        ExternalData.Reindex(map);
        DatabaseRanges.Reindex(map);
        Changes.ReindexSheets(map);
        ReindexSheetFilters(map);
    }

    private void ReindexSheetFilters(Func<int, int> map)
    {
        foreach (var sheet in _sheets)
            if (sheet.RangeFilter is { } filtered)
                sheet.RangeFilter = filtered.Filter.Reindexed(map) is { } kept ? filtered with { Filter = kept } : null;
    }

    public string UniqueSheetName(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        var index = _sheets.Count + 1;

        while (FindSheet($"{prefix}{index}") is not null) index++;

        return $"{prefix}{index}";
    }

    public event EventHandler? NamesChanged;

    public void DefineName(DefinedName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var key = DefinedNameKey.Of(name.Name, name.Scope);
        if (_names.TryGetValue(key, out var existing) && existing == name) return;
        _names[key] = name;
        NamesChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveName(string name, string? scope = null)
    {
        if (!_names.Remove(DefinedNameKey.Of(name, scope))) return false;
        NamesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public DefinedName? FindName(string name, string? scope = null) =>
        _names.TryGetValue(DefinedNameKey.Of(name, scope), out var n) ? n : null;

    public DefinedName? ResolveName(string name, string? sheetName) =>
        (sheetName is null ? null : FindName(name, sheetName)) ?? FindName(name);

    public Workbook()
    {
        NamedStyles = new NamedStyleCatalog(Styles);
        NamedStyles.AddDefaults();
    }

    public const string CanonicalSheetPrefix = "Sheet";

    public static Workbook CreateDefault() => CreateDefault(CanonicalSheetPrefix);

    public static Workbook CreateDefault(string sheetPrefix)
    {
        var wb = new Workbook();
        wb.AddSheet(wb.UniqueSheetName(sheetPrefix));
        return wb;
    }
}

public readonly record struct SheetCell(int SheetIndex, CellAddress Address)
{
    public override string ToString() => $"#{SheetIndex}!{Address.ToA1()}";
}

public readonly record struct DefinedNameKey(string Name, string Scope)
{
    public static DefinedNameKey Of(string name, string? scope) =>
        new(name.ToUpperInvariant(), (scope ?? string.Empty).ToUpperInvariant());
}
