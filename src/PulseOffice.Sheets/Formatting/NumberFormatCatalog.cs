namespace PulseOffice.Sheets.Formatting;

public sealed class NumberFormatCatalog(ISpreadsheetLocale locale, ISystemDateCode systemDates)
{
    private readonly NumberFormatParser _parser = new();

    public NumberFormatCatalog() : this(new SystemSpreadsheetLocale(), new SystemDateCode())
    {
    }

    public const int SystemShortDateId = 14;

    public const int SystemDateTimeId = 22;

    public const int FirstCustomId = 164;

    private static readonly Dictionary<int, string> BuiltIn = new()
    {
        [0] = "General",
        [1] = "0",
        [2] = "0.00",
        [3] = "#,##0",
        [4] = "#,##0.00",
        [9] = "0%",
        [10] = "0.00%",
        [11] = "0.00E+00",
        [12] = "# ?/?",
        [13] = "# ??/??",
        [14] = "dd/mm/yyyy",
        [15] = "d-mmm-yy",
        [16] = "d-mmm",
        [17] = "mmm-yy",
        [18] = "h:mm AM/PM",
        [19] = "h:mm:ss AM/PM",
        [20] = "h:mm",
        [21] = "h:mm:ss",
        [22] = "dd/mm/yyyy h:mm",
        [37] = "#,##0;-#,##0",
        [38] = "#,##0;[RED]-#,##0",
        [39] = "#,##0.00;-#,##0.00",
        [40] = "#,##0.00;[RED]-#,##0.00",
        [45] = "mm:ss",
        [46] = "[h]:mm:ss",
        [47] = "mm:ss.0",
        [48] = "##0.0E+0",
        [49] = "@",
    };

    private readonly Dictionary<int, NumberFormat> _cache = [];
    private readonly Dictionary<string, int> _customIds = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _customCodes = [];
    private int _nextCustomId = FirstCustomId;

    public NumberFormat Get(int id)
    {
        if (SystemCode(id) is { } system)
        {
            if (_system.TryGetValue(system, out var known)) return known;
            return _system[system] = _parser.Parse(system);
        }

        if (_cache.TryGetValue(id, out var cached)) return cached;

        var code = BuiltIn.GetValueOrDefault(id, "General");
        var format = _parser.Parse(code);

        _cache[id] = format;
        return format;
    }

    private readonly Dictionary<string, NumberFormat> _system = new(StringComparer.Ordinal);

    private string? SystemCode(int id) => id switch
    {
        SystemShortDateId => systemDates.ShortDate(locale.Culture),
        SystemDateTimeId => systemDates.ShortDate(locale.Culture) + " h:mm",
        _ => null,
    };

    private string? BuiltInCode(int id) => SystemCode(id) ?? BuiltIn.GetValueOrDefault(id);

    public int Register(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        foreach (var builtIn in BuiltIn.Keys)
            if (string.Equals(BuiltInCode(builtIn), code, StringComparison.OrdinalIgnoreCase)) return builtIn;
        if (_customIds.TryGetValue(code, out var id)) return id;

        id = _nextCustomId++;
        _customIds[code] = id;
        _customCodes[id] = code;
        _cache[id] = _parser.Parse(code);
        return id;
    }

    public IReadOnlyDictionary<string, int> CustomFormats => _customIds;

    public void CopyTo(NumberFormatCatalog target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target._customIds.Clear();
        target._customCodes.Clear();
        target._cache.Clear();
        foreach (var (code, id) in _customIds) target._customIds[code] = id;
        foreach (var (id, code) in _customCodes) target._customCodes[id] = code;
        foreach (var (id, format) in _cache) target._cache[id] = format;
        target._nextCustomId = _nextCustomId;
    }

    public string CodeOf(int id) =>
        BuiltInCode(id) is { } code
            ? code
            : _customCodes.GetValueOrDefault(id, "General");

    public static IReadOnlyDictionary<int, string> BuiltInFormats => BuiltIn;
}
