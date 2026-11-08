using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Spelling;

namespace PulseOffice.Sheets.UI;

public sealed record SheetMistake(CellAddress Address, SpellingMistake Mistake);

public interface ISheetSpelling
{
    bool Enabled { get; set; }

    LanguageTag LanguageOf(Workbook workbook, Worksheet sheet, CellAddress address);

    IReadOnlyList<SpellingMistake> MistakesIn(
        Workbook workbook, Worksheet sheet, CellAddress address, int suggestions = 0);

    IEnumerable<SheetMistake> Walk(Workbook workbook, Worksheet sheet, CellAddress from);

    event EventHandler? Changed;
}

public sealed class SheetSpelling(
    ISpellChecker checker, IStyleCatalogAccess styles, IAutoSpellSettings settings, ITextLanguageResolver languages,
    PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges dictionaries) : ISheetSpelling
{
    public bool Enabled
    {
        get => settings.Enabled;
        set => settings.Enabled = value;
    }

    public event EventHandler? Changed
    {
        add
        {
            settings.Changed += value;
            dictionaries.Changed += value;
        }
        remove
        {
            settings.Changed -= value;
            dictionaries.Changed -= value;
        }
    }

    public LanguageTag LanguageOf(Workbook workbook, Worksheet sheet, CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        return languages.Resolve(styles.StyleOf(workbook, sheet, address)?.Language, workbook.TextLanguage);
    }

    public IReadOnlyList<SpellingMistake> MistakesIn(
        Workbook workbook, Worksheet sheet, CellAddress address, int suggestions = 0)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var cell = sheet[address];

        if (cell.HasFormula || cell.Value.Kind != ValueKind.Text) return [];

        return checker.Check(cell.Value.AsText(), LanguageOf(workbook, sheet, address), suggestions);
    }

    public IEnumerable<SheetMistake> Walk(Workbook workbook, Worksheet sheet, CellAddress from)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var cells = sheet.NonEmptyCells()
            .Select(pair => pair.Key)
            .OrderBy(address => address.Row)
            .ThenBy(address => address.Column)
            .ToList();

        var start = cells.FindIndex(address =>
            address.Row > from.Row || (address.Row == from.Row && address.Column >= from.Column));

        if (start < 0) start = 0;

        for (var step = 0; step < cells.Count; step++)
        {
            var address = cells[(start + step) % cells.Count];

            foreach (var mistake in MistakesIn(workbook, sheet, address, suggestions: 6))
                yield return new SheetMistake(address, mistake);
        }
    }
}

public interface IStyleCatalogAccess
{
    CellStyle? StyleOf(Workbook workbook, Worksheet sheet, CellAddress address);
}

public sealed class StyleCatalogAccess : IStyleCatalogAccess
{
    public CellStyle? StyleOf(Workbook workbook, Worksheet sheet, CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var id = sheet[address].StyleId;

        return id == StyleCatalog.DefaultStyleId ? null : workbook.Styles.Get(id);
    }
}
