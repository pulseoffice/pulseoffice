namespace PulseOffice.UI.Icons;

public sealed class FormulaIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("ledger.formula.wizard", "M8 4h8 M8 4l4 7-4 9h8 M16 11h-4"),
        ("ledger.formula.cycleReference", "M6 8h9a4 4 0 0 1 0 8H8 M9 5L6 8l3 3 M11 13l-3 3 3 3"),
        ("ledger.name.define", "M4 6h16v12H4z M8 15V9l8 6V9"),
        ("ledger.name.manage", "M4 5h16v14H4z M8 9h8 M8 13h5 M15 15l4 4 M19 15l-4 4"),
        ("ledger.name.fromLabels", "M3 5h18v5H3z M3 12h6v7H3z M11 12h10 M11 16h10 M11 19h6"),
        ("ledger.sheet.labels", "M4 4h10l6 6v10H4z M14 4v6h6 M8 14h8 M8 17h5"),
        ("ledger.trace.precedents", "M4 5h6v6H4z M14 13h6v6h-6z M10 8h4a3 3 0 0 1 3 3v2 M14 11l3 3 3-3"),
        ("ledger.trace.dependents", "M4 13h6v6H4z M14 5h6v6h-6z M10 16h4a3 3 0 0 0 3-3V9 M14 12l3-3 3 3"),
        ("ledger.trace.clearPrecedents", "M4 5h6v6H4z M10 8h7v5 M14 11l3 3 3-3 M16 18l5 5 M21 18l-5 5"),
        ("ledger.trace.clearDependents", "M4 13h6v6H4z M10 16h7v-5 M14 12l3-3 3 3 M16 3l5 5 M21 3l-5 5"),
        ("ledger.trace.errors", "M12 4l9 16H3z M12 10v5 M12 18h.01"),
        ("ledger.trace.markInvalid", "M5 5h14v14H5z M9 9l6 6 M15 9l-6 6"),
        ("ledger.view.formulas", "M4 5h16v14H4z M8 16V8h4 M8 12h3 M14 10l4 6 M18 10l-4 6"),
        ("ledger.view.valueHighlight", "M4 5h16v14H4z M7 9h4 M13 9h4 M7 14h10"),
        ("ledger.calc.recalculate", "M20 12a8 8 0 1 1-2.3-5.6 M20 3v6h-6"),
        ("ledger.calc.recalculateAll", "M20 12a8 8 0 1 1-2.3-5.6 M20 3v6h-6 M4 12a8 8 0 0 0 2.3 5.6 M4 21v-6h6"),
        ("ledger.calc.automatic", "M5 4h14v16H5z M9 8h6 M9 12h6 M9 16h3 M17 3l-3 5h4l-3 5"),
        ("ledger.calc.iterations", "M12 4a8 8 0 1 0 8 8 M20 4v5h-5 M9 12h6 M12 9v6"),
        ("ledger.calc.findLabels", "M4 6h7l3 3H4z M4 9v9h16V9 M8 13h8 M8 16h5"),
        ("ledger.calc.formulaToValue", "M5 8h6 M5 16h6 M8 5v6 M15 8h5 M15 16h5 M17 13l3 3-3 3"),
    ];
}
