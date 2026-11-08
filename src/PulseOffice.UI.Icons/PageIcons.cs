namespace PulseOffice.UI.Icons;

public sealed class PageIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("shell.file.printPreview", "M6 3h12v5H6z M4 8h16v8H4z M6 16h12v5H6z M9 18h6"),
        ("ledger.print.setRange", "M4 4h16v16H4z M8 8h8v8H8z"),
        ("ledger.print.addRange", "M4 4h12v12H4z M18 14v6 M15 17h6"),
        ("ledger.print.clearRange", "M4 4h12v12H4z M15 15l6 6 M21 15l-6 6"),
        ("ledger.print.repeatRows", "M3 5h18v4H3z M3 12h18 M3 16h18 M3 20h18"),
        ("ledger.break.insertRow", "M4 4h16v6H4z M4 14h16v6H4z M2 12h4 M9 12h6 M18 12h4"),
        ("ledger.break.insertColumn", "M4 4h6v16H4z M14 4h6v16h-6z M12 2v4 M12 9v6 M12 18v4"),
        ("ledger.break.removeRow", "M4 4h16v6H4z M4 14h16v6H4z M8 12h8"),
        ("ledger.break.removeColumn", "M4 4h6v16H4z M14 4h6v16h-6z M12 8v8"),
        ("ledger.worksheet.colourTab", "M4 4h16v16H4z"),
    ];
}
