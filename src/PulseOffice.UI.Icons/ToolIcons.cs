namespace PulseOffice.UI.Icons;

public sealed class ToolIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("shell.tools.recordMacro", "M12 5a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z"),
        ("ledger.macro.editor", "M8 8l-4 4 4 4 M16 8l4 4-4 4 M13 5l-2 14"),
        ("deck.macro.editor", "M8 8l-4 4 4 4 M16 8l4 4-4 4 M13 5l-2 14"),
        ("ledger.sheet.events", "M6 9a6 6 0 1 1 12 0c0 5 2 6 2 6H4s2-1 2-6z M10 19a2 2 0 0 0 4 0"),
        ("ledger.form.tabOrder", "M4 5h6v6H4z M14 5h6v6h-6z M4 13h6v6H4z M14 13h6v6h-6z M10 8h4 M17 11v2 M14 16h-4"),
        ("shell.form.automaticFocus", "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M4 8V4h4 M20 8V4h-4 M4 16v4h4 M20 16v4h-4"),
        ("shell.form.designMode", "M4 20l4-1 10-10-3-3L5 16z M14 6l3 3 3-3-3-3z"),
        ("ledger.form.properties", "M5 4h14v16H5z M9 9h6 M9 13h6 M9 17h3"),
        ("ledger.form.insert.textBox", "M3 7h18v10H3z M7 10h8"),
        ("ledger.form.insert.checkBox", "M4 4h16v16H4z M8 12l3 3 5-6"),
        ("ledger.form.insert.optionButton", "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z"),
        ("ledger.form.insert.listBox", "M4 4h16v16H4z M7 8h10 M7 12h10 M7 16h6"),
        ("ledger.form.insert.comboBox", "M3 7h18v10H3z M16 7v10 M17.5 11l1.5 2 1.5-2"),
        ("ledger.form.insert.pushButton", "M3 8h18v8H3z M8 12h8"),
        ("ledger.form.insert.label", "M3 8h14l4 4-4 4H3z"),
        ("shell.tools.autocorrectWhileTyping", "M4 18l3-12 3 12 M5 14h4 M13 14l3 3 5-7"),
        ("shell.tools.customize", "M4 6h10 M18 6h2 M4 12h4 M12 12h8 M4 18h10 M18 18h2 M16 4v4 M10 10v4 M16 16v4"),
    ];
}
