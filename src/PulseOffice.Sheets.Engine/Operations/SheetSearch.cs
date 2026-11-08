using System.Text.RegularExpressions;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record StyleFilter
{
    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public string? BackgroundHex { get; init; }

    public int? NumberFormatId { get; init; }

    public bool IsEmpty =>
        Bold is null && Italic is null && FontFamily is null
        && FontSize is null && BackgroundHex is null && NumberFormatId is null;

    public bool Matches(Styling.CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        if (Bold is { } bold && style.Font.Bold != bold) return false;
        if (Italic is { } italic && style.Font.Italic != italic) return false;

        if (FontFamily is { } family
            && !string.Equals(style.Font.Family, family, StringComparison.OrdinalIgnoreCase))
            return false;

        if (FontSize is { } size && Math.Abs(style.Font.Size - size) > 0.01) return false;

        if (BackgroundHex is { } background
            && !string.Equals(style.BackgroundHex, background, StringComparison.OrdinalIgnoreCase))
            return false;

        if (NumberFormatId is { } format && style.NumberFormatId != format) return false;

        return true;
    }
}

public sealed record SearchOptions
{
    public bool MatchCase { get; init; }

    public bool WholeCell { get; init; }

    public bool SearchFormulas { get; init; }

    public bool UseRegex { get; init; }

    public bool AllSheets { get; init; }

    public StyleFilter? Style { get; init; }

    public string HistoryLabel { get; init; } = "Replace";

    public static readonly SearchOptions Default = new();
}

public sealed record SearchMatch(CellAddress Address, string Text);

public interface ISheetSearch
{
    IReadOnlyList<SearchMatch> FindAll(
        Worksheet sheet, string query, SearchOptions? options = null, CellRange? scope = null, Styling.StyleCatalog? styles = null);

    int ReplaceAll(
        Worksheet sheet, string query, string replacement, SearchOptions? options = null, CellRange? scope = null, UndoStack? history = null);
}

public sealed class SheetSearch : ISheetSearch
{
    private readonly TextMatchers _matchers = new();

    public IReadOnlyList<SearchMatch> FindAll(
        Worksheet sheet, string query, SearchOptions? options = null, CellRange? scope = null,
        Styling.StyleCatalog? styles = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        options ??= SearchOptions.Default;

        var byStyle = options.Style is { IsEmpty: false } && styles is not null;
        if (string.IsNullOrEmpty(query) && !byStyle) return [];

        var matcher = string.IsNullOrEmpty(query) ? new AnyText() : _matchers.For(query, options);
        var matches = new List<SearchMatch>();

        foreach (var (address, cell) in sheet.NonEmptyCells())
        {
            if (scope is { } range && !range.Contains(address)) continue;
            if (byStyle && !options.Style!.Matches(styles!.Get(sheet.StyleIdAt(address)))) continue;

            var text = TextOf(cell, options);

            if (text.Length == 0)
            {
                if (byStyle && string.IsNullOrEmpty(query)) matches.Add(new SearchMatch(address, text));
                continue;
            }

            if (matcher.Matches(text)) matches.Add(new SearchMatch(address, text));
        }

        return matches;
    }

    public int ReplaceAll(
        Worksheet sheet, string query, string replacement,
        SearchOptions? options = null, CellRange? scope = null, UndoStack? history = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (string.IsNullOrEmpty(query)) return 0;

        options ??= SearchOptions.Default;

        var matches = FindAll(sheet, query, options, scope);
        if (matches.Count == 0) return 0;

        var matcher = _matchers.For(query, options);
        using var transaction = history?.BeginTransaction(options.HistoryLabel);
        var replaced = 0;

        foreach (var match in matches)
        {
            var previous = sheet[match.Address];
            var updated = Replace(previous, matcher, replacement, options);

            if (previous == updated) continue;

            sheet[match.Address] = updated;
            replaced++;

            var address = match.Address;
            history?.Push(new DelegateAction(
                options.HistoryLabel,
                redo: () => sheet[address] = updated,
                undo: () => sheet[address] = previous));
        }

        return replaced;
    }

    private Cell Replace(Cell cell, TextMatcher matcher, string replacement, SearchOptions options)
    {
        if (cell.HasFormula && !options.SearchFormulas) return cell;

        var text = TextOf(cell, options);
        var updated = matcher.Substitute(text, replacement);

        if (updated == text) return cell;
        if (cell.HasFormula) return cell with { Formula = updated };

        return cell with
        {
            Value = double.TryParse(updated, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number)
                ? CellValue.Number(number)
                : CellValue.Text(updated),
            Formula = null,
        };
    }

    private string TextOf(Cell cell, SearchOptions options) =>
        options.SearchFormulas && cell.HasFormula ? cell.Formula! : cell.Value.ToString();
}

internal sealed class TextMatchers
{
    public TextMatcher For(string query, SearchOptions options)
    {
        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.CurrentCultureIgnoreCase;

        if (options.UseRegex) return Regex(query, options.MatchCase);
        return options.WholeCell ? new WholeCellText(query, comparison) : new ContainedText(query, comparison);
    }

    private TextMatcher Regex(string query, bool matchCase)
    {
        try
        {
            return new RegexText(new System.Text.RegularExpressions.Regex(
                query,
                RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase),
                TimeSpan.FromSeconds(1)));
        }
        catch (ArgumentException)
        {
            return new NoText();
        }
    }
}

internal abstract class TextMatcher
{
    public abstract bool Matches(string text);

    public abstract string Substitute(string text, string replacement);
}

internal sealed class AnyText : TextMatcher
{
    public override bool Matches(string text) => true;
    public override string Substitute(string text, string replacement) => text;
}

internal sealed class NoText : TextMatcher
{
    public override bool Matches(string text) => false;
    public override string Substitute(string text, string replacement) => text;
}

internal sealed class WholeCellText(string query, StringComparison comparison) : TextMatcher
{
    public override bool Matches(string text) => text.Equals(query, comparison);
    public override string Substitute(string text, string replacement) => Matches(text) ? replacement : text;
}

internal sealed class ContainedText(string query, StringComparison comparison) : TextMatcher
{
    public override bool Matches(string text) => text.Contains(query, comparison);
    public override string Substitute(string text, string replacement) => text.Replace(query, replacement, comparison);
}

internal sealed class RegexText(Regex regex) : TextMatcher
{
    public override bool Matches(string text)
    {
        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public override string Substitute(string text, string replacement)
    {
        try
        {
            return regex.Replace(text, replacement);
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
    }
}
