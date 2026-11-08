using System.Globalization;
using System.Text;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record FilterValueCount(string Value, int Count)
{
    public DateTime? Date { get; init; }

    public string? Shown { get; init; }
}

public sealed record FilterDateGroup(string Key, int Level, int Value, string Label, IReadOnlyList<FilterValueItem> Items, IReadOnlyList<FilterDateGroup> Children);

public sealed record FilterColumnFacts(
    IReadOnlyList<FilterValueCount> Values,
    int Blanks,
    bool Numeric,
    IReadOnlyList<ColorFilterCriterion> Colours);

public interface IFilterColumnFacts
{
    FilterColumnFacts Read(
        Worksheet sheet, CellRange range, int columnOffset, bool hasHeader, Styling.StyleCatalog? styles, Formatting.NumberFormatCatalog? formats = null);
}

public sealed class FilterColumnFactsReader(Engine.Operations.IFilterValueText values, IFilterColumnDates columnDates) : IFilterColumnFacts
{
    public FilterColumnFactsReader() : this(new Engine.Operations.FilterValueText(Formatting.FixedSpreadsheetLocale.Invariant()), new FilterColumnDates())
    {
    }

    public FilterColumnFactsReader(Engine.Operations.IFilterValueText values) : this(values, new FilterColumnDates())
    {
    }

    public FilterColumnFacts Read(
        Worksheet sheet, CellRange range, int columnOffset, bool hasHeader, Styling.StyleCatalog? styles, Formatting.NumberFormatCatalog? formats = null)
    {
        var dates = new Dictionary<string, DateTime>(StringComparer.CurrentCultureIgnoreCase);
        ArgumentNullException.ThrowIfNull(sheet);
        var column = range.Start.Column + columnOffset;
        var counts = new SortedDictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        var shownNumbers = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
        var colours = new List<ColorFilterCriterion>();
        var blanks = 0;
        var numbers = 0;
        var texts = 0;

        for (var row = range.Start.Row + (hasHeader ? 1 : 0); row <= range.End.Row; row++)
        {
            var address = new CellAddress(row, column);
            var value = sheet[address].Value;
            if (value.IsEmpty) blanks++;
            else
            {
                var shown = value.ToString();
                counts[shown] = counts.GetValueOrDefault(shown) + 1;
                if (value.Kind == ValueKind.Number && formats is not null && styles is not null
                    && columnDates.TryRead(sheet, address, styles, formats, out var moment))
                    dates[shown] = moment;
                else if (value.Kind == ValueKind.Number)
                    shownNumbers[shown] = values.Display(shown, false);
                if (value.Kind == ValueKind.Number) numbers++;
                else texts++;
            }

            if (styles?.Get(sheet.StyleIdAt(address)) is not { } style) continue;
            if (style.BackgroundHex is { Length: > 0 } background) Add(colours, new ColorFilterCriterion(ColorFilterTarget.Background, background));
            if (style.Font.ColorHex is { Length: > 0 } ink && !string.Equals(ink, Styling.CellFont.Default.ColorHex, StringComparison.OrdinalIgnoreCase))
                Add(colours, new ColorFilterCriterion(ColorFilterTarget.Text, ink));
        }

        return new FilterColumnFacts(
            [.. counts.Select(pair => new FilterValueCount(pair.Key, pair.Value)
            {
                Date = dates.TryGetValue(pair.Key, out var date) ? date : null,
                Shown = shownNumbers.GetValueOrDefault(pair.Key),
            })],
            blanks, numbers > texts, colours);
    }

    private void Add(List<ColorFilterCriterion> colours, ColorFilterCriterion colour)
    {
        if (colours.Count < 16 && !colours.Any(known => known.Target == colour.Target && string.Equals(known.ColorHex, colour.ColorHex, StringComparison.OrdinalIgnoreCase)))
            colours.Add(colour);
    }
}

public enum FilterOperator { None, Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual, Contains, NotContains, BeginsWith, EndsWith }

public sealed record FilterConditionDraft(FilterOperator Condition, string Value);

public interface IFilterCriteriaCodec
{
    IReadOnlyList<FilterOperator> For(bool numeric);

    string? Write(FilterConditionDraft draft);

    FilterConditionDraft Read(string? criteria, bool dates = false);
}

public sealed class FilterCriteriaCodec(Engine.Operations.IFilterValueText values) : IFilterCriteriaCodec
{
    public FilterCriteriaCodec() : this(new Engine.Operations.FilterValueText(Formatting.FixedSpreadsheetLocale.Invariant()))
    {
    }

    private readonly IReadOnlyList<FilterOperator> _numeric =
    [
        FilterOperator.None, FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.Greater,
        FilterOperator.GreaterOrEqual, FilterOperator.Less, FilterOperator.LessOrEqual,
    ];

    private readonly IReadOnlyList<FilterOperator> _text =
    [
        FilterOperator.None, FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.Contains,
        FilterOperator.NotContains, FilterOperator.BeginsWith, FilterOperator.EndsWith,
    ];

    public IReadOnlyList<FilterOperator> For(bool numeric) => numeric ? _numeric : _text;

    public string? Write(FilterConditionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var value = draft.Value.Trim();
        if (draft.Condition == FilterOperator.None || value.Length == 0) return null;
        if (!IsPattern(draft.Condition)) value = values.Canonical(value);
        return draft.Condition switch
        {
            FilterOperator.Equal => "=" + value,
            FilterOperator.NotEqual => "<>" + value,
            FilterOperator.Greater => ">" + value,
            FilterOperator.GreaterOrEqual => ">=" + value,
            FilterOperator.Less => "<" + value,
            FilterOperator.LessOrEqual => "<=" + value,
            FilterOperator.Contains => "*" + value + "*",
            FilterOperator.NotContains => "<>*" + value + "*",
            FilterOperator.BeginsWith => value + "*",
            FilterOperator.EndsWith => "*" + value,
            _ => null,
        };
    }

    public FilterConditionDraft Read(string? criteria, bool dates = false)
    {
        var draft = Parse(criteria);
        return IsPattern(draft.Condition) ? draft : draft with { Value = values.Display(draft.Value, dates) };
    }

    private bool IsPattern(FilterOperator condition) =>
        condition is FilterOperator.Contains or FilterOperator.NotContains or FilterOperator.BeginsWith or FilterOperator.EndsWith;

    private FilterConditionDraft Parse(string? criteria)
    {
        var text = (criteria ?? string.Empty).Trim();
        if (text.Length == 0) return new FilterConditionDraft(FilterOperator.None, string.Empty);
        if (text.StartsWith("<>*", StringComparison.Ordinal) && text.EndsWith('*') && text.Length > 4)
            return new(FilterOperator.NotContains, text[3..^1]);
        foreach (var (prefix, condition) in new[]
        {
            (">=", FilterOperator.GreaterOrEqual), ("<=", FilterOperator.LessOrEqual), ("<>", FilterOperator.NotEqual),
            (">", FilterOperator.Greater), ("<", FilterOperator.Less), ("=", FilterOperator.Equal),
        })
            if (text.StartsWith(prefix, StringComparison.Ordinal)) return new(condition, text[prefix.Length..]);
        if (text.Length > 2 && text.StartsWith('*') && text.EndsWith('*')) return new(FilterOperator.Contains, text[1..^1]);
        if (text.Length > 1 && text.EndsWith('*')) return new(FilterOperator.BeginsWith, text[..^1]);
        if (text.Length > 1 && text.StartsWith('*')) return new(FilterOperator.EndsWith, text[1..]);
        return new(FilterOperator.Equal, text);
    }
}

public sealed class FilterValueItem(FilterValueCount value, bool chosen)
{
    public FilterValueCount Value { get; } = value;

    public bool Chosen { get; set; } = chosen;
}

public sealed class FilterMenuModel
{
    public const int ListedLimit = 1000;

    private readonly List<FilterValueItem> _items;
    private string _search = string.Empty;
    private IReadOnlyList<FilterValueItem>? _visible;

    public FilterMenuModel(FilterColumnFacts facts, ColumnFilter? current)
    {
        ArgumentNullException.ThrowIfNull(facts);
        Facts = facts;
        _items = [.. facts.Values.Select(value => new FilterValueItem(value, current?.AllowedValues is null || current.AllowedValues.Contains(value.Value)))];
        Blanks = current?.IncludeBlanks ?? true;
    }

    public event EventHandler? Changed;

    public event EventHandler? SelectionChanged;

    public FilterColumnFacts Facts { get; }

    public bool Blanks { get; set; }

    public bool IncludesBlanks => Blanks && _search.Length == 0;

    public IReadOnlyList<FilterValueItem> Items => _items;

    public IReadOnlyList<FilterValueItem> Visible => _visible ??= _search.Length == 0 ? _items : [.. _items.Where(item => Matches(Label(item)))];

    public IReadOnlyList<FilterValueItem> Listed => Visible.Count <= ListedLimit ? Visible : [.. Visible.Take(ListedLimit)];

    public int Unlisted => Math.Max(0, Visible.Count - ListedLimit);

    public bool IsDateColumn => _items.Count > 0 && _items.All(item => item.Value.Date is not null);

    public string Label(FilterValueItem item) =>
        item?.Value.Date is { } date ? date.ToString("d", CultureInfo.CurrentCulture) : item?.Value.Shown ?? item?.Value.Value ?? string.Empty;

    public IReadOnlyList<FilterDateGroup> DateGroups()
    {
        if (!IsDateColumn) return [];
        var culture = CultureInfo.CurrentCulture;
        return [.. Visible.GroupBy(item => item.Value.Date!.Value.Year).OrderBy(year => year.Key)
            .Select(year => new FilterDateGroup(year.Key.ToString(CultureInfo.InvariantCulture), 0, year.Key, year.Key.ToString(culture), [.. year],
                [.. year.GroupBy(item => item.Value.Date!.Value.Month).OrderBy(month => month.Key)
                    .Select(month => new FilterDateGroup(
                        year.Key.ToString(CultureInfo.InvariantCulture) + "-" + month.Key.ToString(CultureInfo.InvariantCulture), 1, month.Key,
                        culture.TextInfo.ToTitleCase(culture.DateTimeFormat.GetMonthName(month.Key)), [.. month],
                        [.. month.OrderBy(item => item.Value.Date)
                            .Select(item => new FilterDateGroup(item.Value.Value, 2, item.Value.Date!.Value.Day, item.Value.Date!.Value.Day.ToString(culture), [item], []))]))]))];
    }

    public bool? Chosen(FilterDateGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var chosen = group.Items.Count(item => item.Chosen);
        return chosen == group.Items.Count ? true : chosen == 0 ? false : null;
    }

    public void Choose(FilterDateGroup group, bool chosen)
    {
        ArgumentNullException.ThrowIfNull(group);
        foreach (var item in group.Items) item.Chosen = chosen;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Search
    {
        get => _search;
        set
        {
            _search = value ?? string.Empty;
            _visible = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool? AllVisibleChosen
    {
        get
        {
            var visible = Visible;
            if (visible.Count == 0) return false;
            var chosen = visible.Count(item => item.Chosen);
            return chosen == visible.Count ? true : chosen == 0 ? false : null;
        }
    }

    public void ChooseAllVisible(bool chosen)
    {
        foreach (var item in Visible) item.Chosen = chosen;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Choose(FilterValueItem item, bool chosen)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Chosen = chosen;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlySet<string>? Allowed()
    {
        var chosen = Visible.Where(item => item.Chosen).Select(item => item.Value.Value).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        return chosen.Count == _items.Count ? null : chosen;
    }

    public int ShownRows()
    {
        var allowed = Allowed();
        var rows = _items.Where(item => allowed is null || allowed.Contains(item.Value.Value)).Sum(item => item.Value.Count);
        return rows + (IncludesBlanks ? Facts.Blanks : 0);
    }

    private bool Matches(string value) => _search.Length == 0 || Fold(value).Contains(Fold(_search), StringComparison.Ordinal);

    public bool ExpandsDatesOnSearch => _search.Length > 0 && Visible.Count <= ListedLimit;

    private string Fold(string text)
    {
        var decomposed = text.ToLower(CultureInfo.CurrentCulture).Normalize(NormalizationForm.FormD);
        return new string([.. decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)]);
    }
}
