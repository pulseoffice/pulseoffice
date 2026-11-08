namespace PulseOffice.UI.Icons;

public interface IIconCatalog
{
    IconGeometry? Find(string key);

    bool Contains(string key);

    IReadOnlyCollection<string> Keys { get; }
}

public sealed class IconCatalog : IIconCatalog
{
    public IconGeometry? Find(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var resolved = Aliases.TryGetValue(key, out var alias) ? alias : key;
        return Shapes.TryGetValue(resolved, out var geometry) ? geometry : null;
    }

    public bool Contains(string key) => Find(key) is not null;

    public IReadOnlyCollection<string> Keys => [.. Shapes.Keys, .. Aliases.Keys];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["ledger.fill.red"] = "fill",
        ["ledger.fill.green"] = "fill",
        ["ledger.fill.yellow"] = "fill",
        ["ledger.fill.none"] = "fill.none",
        ["ledger.number.integer"] = "number.integer",
        ["ledger.number.decimal"] = "number.decimal",
        ["ledger.view.freezeFirstRow"] = "freeze.row",
        ["ledger.view.freezeFirstColumn"] = "freeze.column",
        ["ledger.view.freezeAtSelection"] = "freeze.selection",
        ["ledger.view.unfreeze"] = "freeze.none",
        ["shell.file.new.documents"] = "module.documents",
        ["shell.file.new.sheets"] = "module.sheets",
        ["shell.file.new.presentations"] = "module.presentations",
    };

    private static readonly Dictionary<string, IconGeometry> Shapes = new(StringComparer.Ordinal)
    {
        ["ledger.worksheet.insert"] = new("M4 4h10l6 6v10H4z M14 4v6h6 M9 13h6 M12 10v6"),
        ["ledger.worksheet.duplicate"] = new("M8 3h9l4 4v10H8z M4 7v14h11"),
        ["ledger.worksheet.delete"] = new("M4 4h16v16H4z M9 9l6 6 M15 9l-6 6"),
        ["ledger.worksheet.rename"] = new("M4 20h16 M6 15l9-9 3 3-9 9H6z"),
        ["ledger.worksheet.moveLeft"] = new("M20 4v16H8V4z M6 12H2 M4 9l-2 3 2 3"),
        ["ledger.worksheet.moveRight"] = new("M4 4v16h12V4z M18 12h4 M20 9l2 3-2 3"),
        ["ledger.worksheet.colour"] = new("M12 3a9 9 0 1 0 0 18h2a3 3 0 0 0 0-6h-1a2 2 0 0 1 0-4h2a4 4 0 0 0-3-8z"),
        ["ledger.worksheet.protect"] = new("M6 11V8a6 6 0 0 1 12 0v3 M5 11h14v10H5z M12 15v3"),
        ["ledger.worksheet.hide"] = new("M3 3l18 18 M10 6a9 9 0 0 1 11 6 9 9 0 0 1-3 4 M6 7a9 9 0 0 0-3 5 9 9 0 0 0 11 6"),
        ["ledger.worksheet.show"] = new("M2 12s4-7 10-7 10 7 10 7-4 7-10 7-10-7-10-7z M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"),
        ["ledger.data.refresh"] = new("M20 11a8 8 0 1 0-2.3 5.7 M20 5v6h-6"),
        ["ledger.chart.gallery"] = new("M4 20h16 M7 20V9 M12 20V5 M17 20v-8 M3 4l4 4 5-5 5 6"),
        ["ledger.connector.elbow"] = new("M4 6h6v12h10 M17 15l3 3-3 3"),
        ["ledger.connector.straight"] = new("M4 19L17 6 M12 6h5v5"),
        ["ledger.connector.curved"] = new("M4 18c6 0 10-4 10-12 M11 4h3v3"),
        ["ledger.object.alignLeft"] = new("M3 3v18 M6 7h12 M6 13h7"),
        ["ledger.object.alignCentre"] = new("M12 3v18 M6 7h12 M8 13h8"),
        ["ledger.object.alignRight"] = new("M21 3v18 M6 7h12 M11 13h7"),
        ["ledger.object.alignTop"] = new("M3 3h18 M7 6v12 M13 6v7"),
        ["ledger.object.alignMiddle"] = new("M3 12h18 M7 6v12 M13 8v8"),
        ["ledger.object.alignBottom"] = new("M3 21h18 M7 6v12 M13 11v7"),
        ["ledger.object.spreadHorizontally"] = new("M3 3v18 M21 3v18 M10 7h4v10h-4z"),
        ["ledger.object.spreadVertically"] = new("M3 3h18 M3 21h18 M7 10v4h10v-4z"),
        ["ledger.object.group"] = new("M3 3h6v6H3z M15 15h6v6h-6z M9 6h6v3 M6 9v6h3"),
        ["ledger.object.ungroup"] = new("M3 3h7v7H3z M14 14h7v7h-7z M12 5h2 M19 12v2"),
        ["ledger.object.toFront"] = new("M8 3h13v13H8z M3 8h3v10h10v3H3z"),
        ["ledger.object.forward"] = new("M8 3h13v13H8z M3 10h5v8h8v3H3z"),
        ["ledger.object.backward"] = new("M3 8h13v13H3z M8 3h13v13"),
        ["ledger.object.toBack"] = new("M3 8h13v13H3z M8 3h13v13h-3V8H8z"),
        ["ledger.image.gallery"] = new("M12 3l2.5 5.5 6 .5-4.5 4 1.4 5.9L12 15.8 6.6 18.9 8 13l-4.5-4 6-.5z"),
        ["ledger.image.svg"] = new("M4 5h16v14H4z M4 15l4-4 4 4 3-3 5 5"),
        ["styles"] = new("M4 5h16 M4 10h16 M4 15h10 M4 20h10 M17 16l3 3 M20 16l-3 3"),
        ["styles.add"] = new("M4 5h16 M4 10h16 M4 15h9 M4 20h9 M18 15v6 M15 18h6"),
        ["ledger.format.bold"] = new("M7 4h6a4 4 0 0 1 0 8H7z M7 12h7a4 4 0 0 1 0 8H7z"),
        ["ledger.format.italic"] = new("M10 4h8 M6 20h8 M14.5 4l-5 16"),
        ["ledger.format.underline"] = new("M7 3v7a5 5 0 0 0 10 0V3 M5 21h14"),
        ["ledger.format.strikethrough"] = new("M8 5.5A4 4 0 0 1 12 3h1a4 4 0 0 1 3.6 2.2 M4 12h16 M7.5 16A4 4 0 0 0 11.5 21h1a4 4 0 0 0 4-4"),
        ["ledger.format.grow"] = new("M3 20l6-16 6 16 M5.5 14h7 M19 9v7 M15.5 12.5h7"),
        ["ledger.format.shrink"] = new("M3 20l6-16 6 16 M5.5 14h7 M15.5 12.5h7"),

        ["ledger.align.left"] = new("M4 5h16 M4 10h10 M4 15h16 M4 20h10"),
        ["ledger.align.center"] = new("M4 5h16 M7 10h10 M4 15h16 M7 20h10"),
        ["ledger.align.right"] = new("M4 5h16 M10 10h10 M4 15h16 M10 20h10"),
        ["ledger.align.general"] = new("M4 5h16 M4 10h8 M4 15h16 M12 20h8"),
        ["ledger.align.indent"] = new("M4 5h16 M10 10h10 M10 15h10 M4 20h16 M4 10.5l3 2-3 2z") { Filled = false },
        ["ledger.align.outdent"] = new("M4 5h16 M10 10h10 M10 15h10 M4 20h16 M7 10.5l-3 2 3 2z"),
        ["ledger.align.wrap"] = new("M4 5h16 M4 11h13a3.5 3.5 0 0 1 0 7h-4 M12 15l-3 3 3 3"),

        ["ledger.number.general"] = new("M9 4L7 20 M17 4l-2 16 M4 9h16 M4 15h16"),
        ["ledger.number.currency"] = new("M12 3v18 M16.5 7A4 4 0 0 0 13 5h-1a3.5 3.5 0 0 0 0 7h2a3.5 3.5 0 0 1 0 7h-1a4 4 0 0 1-3.5-2"),
        ["ledger.number.percent"] = new("M7 5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z M17 14a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z M18 5L6 19"),
        ["ledger.number.moreDecimals"] = new("M4 19h1.5 M9 6v13 M13.5 6v13 M17 11l4 2.5-4 2.5"),
        ["ledger.number.fewerDecimals"] = new("M4 19h1.5 M9 6v13 M13.5 6v13 M21 11l-4 2.5 4 2.5"),
        ["ledger.number.date"] = new("M4 6h16v15H4z M4 11h16 M8 3v5 M16 3v5"),
        ["ledger.number.time"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M12 7v5.5l3.5 2"),
        ["ledger.number.text"] = new("M5 7V4h14v3 M12 4v16 M9 20h6"),
        ["ledger.number.scientific"] = new("M4 8l7 10 M11 8L4 18 M15 5h5 M17.5 2.5v5"),
        ["number.integer"] = new("M5 8l3-2.5V19 M13 7.5a3 3 0 1 1 5.5 2L13 19h6"),
        ["number.decimal"] = new("M4 19h1.5 M9 7.5a3 3 0 1 1 5.5 2L9 19h6 M19 7v12"),

        ["ledger.border.all"] = new("M4 4h16v16H4z M12 4v16 M4 12h16"),
        ["ledger.border.bottom"] = new("M4 4h4 M10 4h4 M16 4h4 M20 8v4 M20 14v2 M4 8v4 M4 14v2 M4 20h16"),
        ["ledger.border.none"] = new("M4 4h4 M10 4h4 M16 4h4 M20 8v4 M20 14v2 M4 8v4 M4 14v2 M4 20h4 M10 20h4 M16 20h4"),

        ["fill"] = new("M6 11l6.5-6.5 6.5 6.5-6.5 6.5z M19.5 15c0 1.4 1 2.1 1 3.1a1.5 1.5 0 1 1-3 0c0-1 1-1.7 1-3.1z M3 21h18"),
        ["fill.none"] = new("M6 11l6.5-6.5 6.5 6.5-6.5 6.5z M3 21h18 M4 4l16 16"),

        ["ledger.clipboard.copy"] = new("M9 9h11v11H9z M15 9V4H4v11h5"),
        ["ledger.clipboard.cut"] = new("M7 4l10 13 M17 4L7 17 M6.5 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z M17.5 21a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z"),
        ["ledger.clipboard.paste"] = new("M9 3h6v3H9z M7 5H5v16h14V5h-2"),
        ["ledger.clipboard.pasteValues"] = new("M9 3h6v3H9z M7 5H5v16h14V5h-2 M9 13h6 M9 17h6"),
        ["ledger.clipboard.pasteStyles"] = new("M9 3h6v3H9z M7 5H5v16h14V5h-2 M9 15l3-3 3 3-3 3z"),

        ["shell.edit.undo"] = new("M4 10h10a5.5 5.5 0 0 1 0 11h-3 M4 10l4.5-4.5 M4 10l4.5 4.5"),
        ["shell.edit.redo"] = new("M20 10H10a5.5 5.5 0 0 0 0 11h3 M20 10l-4.5-4.5 M20 10l-4.5 4.5"),

        ["deck.present"] = new("M8 5v14l11-7z"),
        ["shell.file.open"] = new("M3 7h6l2 2.5h10V20H3z M3 7v13"),
        ["shell.file.save"] = new("M5 4h11l4 4v12H5z M8 4v6h8V4 M8 20v-6h8v6"),
        ["shell.file.saveAs"] = new("M5 4h10l4 4v5 M8 4v6h7V4 M13 21l6.5-6.5 2.5 2.5L15.5 21z"),
        ["shell.file.templates"] = new("M4 3h16v18H4z M4 8h16 M10 8v13 M13 12h4 M13 16h4"),
        ["shell.file.saveAsOffice"] = new("M5 4h10l4 4v4 M8 4v6h7V4 M5 4v16h6 M14 15h7 M18 12l3 3-3 3"),
        ["shell.file.exportPdf"] = new("M6 3h8l5 5v13H6z M14 3v5h5 M9 13h2a1.5 1.5 0 0 1 0 3H9 M9 16v3"),
        ["shell.file.exportPdfDirect"] = new("M6 3h8l5 5v13H6z M14 3v5h5 M9 13h2a1.5 1.5 0 0 1 0 3H9 M9 16v3 M14 14l3 2.5-3 2.5"),

        ["ledger.data.sortAscending"] = new("M6 20V5 M3 8l3-3 3 3 M11 6h9 M11 12h6 M11 18h3"),
        ["ledger.data.sortDescending"] = new("M6 5v15 M3 17l3 3 3-3 M11 6h3 M11 12h6 M11 18h9"),
        ["ledger.data.filterClear"] = new("M3 5h18l-7 8v7l-4-2v-5z M16 17l5 5 M21 17l-5 5"),
        ["ledger.data.findReplace"] = new("M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M16.2 16.2L21 21"),

        ["ledger.chart.column"] = new("M4 20h16 M7 20V11 M12 20V5 M17 20v-6"),
        ["ledger.chart.bar"] = new("M4 20V4 M4 7h12 M4 12h7 M4 17h15"),
        ["ledger.chart.line"] = new("M4 20h16 M5 16l4.5-5 4 3L20 6"),
        ["ledger.chart.area"] = new("M4 20h16 M4 16l5-5 4 3 7-8v14H4z"),
        ["ledger.chart.pie"] = new("M12 3a9 9 0 1 0 9 9h-9z M12 3v9h9a9 9 0 0 0-9-9z"),
        ["ledger.chart.doughnut"] = new("M12 3a9 9 0 1 0 9 9h-4.5a4.5 4.5 0 1 1-4.5-4.5z M12 3v4.5a4.5 4.5 0 0 1 4.5 4.5H21a9 9 0 0 0-9-9z"),
        ["ledger.chart.delete"] = new("M4 20h16 M7 20V11 M12 20V7 M16 6l5 5 M21 6l-5 5"),

        ["ledger.conditional.greater"] = new("M12 3l2.6 5.6 6 .8-4.4 4.2 1.1 6-5.3-2.9-5.3 2.9 1.1-6L3.4 9.4l6-.8z"),
        ["ledger.conditional.negative"] = new("M12 4v15 M6 13l6 6 6-6"),
        ["ledger.conditional.dataBars"] = new("M4 5h9v4H4z M4 11h16v4H4z M4 17h6v4H4z"),
        ["ledger.conditional.colorScale"] = new("M4 6h4v12H4z M10 6h4v12h-4z M16 6h4v12h-4z"),
        ["ledger.conditional.clear"] = new("M9 20H5l-2.5-2.5 9.5-9.5 6 6-6 6z M20 20h-8 M8 11l6 6"),

        ["freeze.row"] = new("M4 4h16v16H4z M4 9h16 M9.5 9v11 M15 9v11"),
        ["freeze.column"] = new("M4 4h16v16H4z M9 4v16 M9 9.5h11 M9 15h11"),
        ["freeze.selection"] = new("M4 4h16v16H4z M4 10h16 M10 4v16"),
        ["freeze.none"] = new("M4 4h16v16H4z M8 8l8 8 M16 8l-8 8"),

        ["shell.tools.options"] = new("M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M12 3v2.5 M12 18.5V21 M3 12h2.5 M18.5 12H21 M5.6 5.6l1.8 1.8 M16.6 16.6l1.8 1.8 M18.4 5.6l-1.8 1.8 M7.4 16.6l-1.8 1.8"),
        ["shell.tools.macros"] = new("M7 4H5v7l-2 1 2 1v7h2 M17 4h2v7l2 1-2 1v7h-2 M10 10l2 2-2 2 M14 14h1.5"),
        ["shell.help.about"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M12 11v6 M12 7.5h.01"),

        ["module.documents"] = new("M6 3h8l5 5v13H6z M14 3v5h5 M9 13h6 M9 17h4"),
        ["module.sheets"] = new("M4 4h16v16H4z M4 10h16 M4 15h16 M10 4v16"),
        ["module.presentations"] = new("M4 5h16v11H4z M12 16v3 M8.5 19h7"),

        ["shell.file.print"] = new("M7 9V4h10v5 M7 18H5v-6h14v6h-2 M7 14h10v6H7z M17 12.5h.01"),

        ["ledger.chart.edit"] = new("M4 19V5 M4 19h16 M8 16v-5 M12 16V8 M16 16v-7 M18 4l2 2-5 5h-2v-2z"),
        ["ledger.style.apply"] = new("M4 5h16v5H4z M4 13h7v6H4z M14 13h6v6h-6z"),
        ["ledger.style.createFromSelection"] = new("M4 5h16v5H4z M4 13h7v6H4z M17 13v6 M14 16h6"),
        ["ledger.comment.edit"] = new("M4 5h16v11H9l-5 4z"),
        ["ledger.comment.delete"] = new("M4 5h16v11H9l-5 4z M9 9l6 4 M15 9l-6 4"),
        ["ledger.data.pivotCreate"] = new("M4 4h16v16H4z M4 9h16 M9 9v11 M12 12h5 M12 16h5"),
        ["ledger.data.pivotRefresh"] = new("M4 4h16v16H4z M4 9h16 M9 9v11 M16 12a3 3 0 1 0 2 5 M18 12v3h-3"),
        ["ledger.pivot.calculatedField"] = new("M4 4h16v16H4z M4 9h16 M9 9v11 M12 14h6 M15 11v6"),
        ["ledger.pivot.delete"] = new("M4 4h16v16H4z M4 9h16 M9 9v11 M12 12l5 5 M17 12l-5 5"),
        ["ledger.data.removeDuplicates"] = new("M4 4h9v9H4z M11 11h9v9h-9z M14 15l3 3 M17 15l-3 3"),
        ["ledger.print.pageSetup"] = new("M6 3h12v18H6z M9 7h6 M9 11h6 M9 15h4"),
        ["ledger.trace.clear"] = new("M5 19L19 5 M5 5l14 14"),
        ["shell.help.shortcuts"] = new("M3 7h18v10H3z M7 11h.01 M11 11h.01 M15 11h.01 M8 14h8"),
        ["shell.view.commandPalette"] = new("M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M16.2 16.2L21 21 M8 11h6"),

        ["ui.refresh"] = new("M20 11a8 8 0 1 0-2.3 5.7 M20 4v7h-7"),
        ["ui.dictionary.hyphen"] = new("M4 6h7 M4 12h5 M4 18h7 M13 12h3 M19 6v12"),
        ["ui.dictionary.thesaurus"] = new("M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3z M5 17a3 3 0 0 1 3-3h11 M9 8h6"),
        ["ui.keyboard"] = new("M3 6h18v12H3z M6.5 9.5h1 M10 9.5h1 M13.5 9.5h1 M17 9.5h1 M6.5 12.5h1 M10 12.5h1 M13.5 12.5h1 M17 12.5h1 M8 15.5h8"),
        ["ui.search"] = new("M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M16.2 16.2L21 21"),
        ["ui.close"] = new("M6 6l12 12 M18 6L6 18"),
        ["ui.chevronDown"] = new("M6 9l6 6 6-6"),
        ["ui.chevronUp"] = new("M6 15l6-6 6 6"),
        ["ui.galleryMore"] = new("M6 6h12M6 11l6 6 6-6"),
        ["ui.check"] = new("M5 12.5l4.5 4.5L19 7"),
        ["ui.reset"] = new("M4 12a8 8 0 1 0 2.4-5.7 M4 4v5h5"),
        ["ui.language"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M3 12h18 M12 3a13 13 0 0 1 0 18 13 13 0 0 1 0-18z"),
        ["ui.add"] = new("M12 5v14 M5 12h14"),
        ["ui.duplicate"] = new("M9 9h11v11H9z M5 15H4V4h11v1"),
        ["ui.delete"] = new("M4 7h16 M9 7V4h6v3 M6 7l1 13h10l1-13 M10 11v6 M14 11v6"),
        ["ui.swap"] = new("M4 8h14 M14 4l4 4-4 4 M20 16H6 M10 12l-4 4 4 4"),
        ["ui.list"] = new("M9 6h11 M9 12h11 M9 18h11 M4.5 6h.01 M4.5 12h.01 M4.5 18h.01"),
        ["ui.folder"] = new("M3 6h6l2 3h10v9H3z"),
        ["ui.file"] = new("M6 3h8l4 4v14H6z M14 3v4h4"),
        ["ui.back"] = new("M20 12H4 M10 6l-6 6 6 6"),
        ["ui.cloud"] = new("M7 18a4 4 0 0 1 0-8 5 5 0 0 1 9.6-1.5A4 4 0 0 1 17 18z"),
        ["ui.pickRange"] = new("M3 4h13v11H3z M3 9.5h13 M9.5 4v11 M14 13l7 2.6-3 1.4-1.4 3z"),
        ["ui.notice.info"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M12 11v6 M12 7.5h.01"),
        ["ui.notice.success"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M8 12.5l2.8 2.8L16 9.5"),
        ["ui.notice.warning"] = new("M12 3.5L21.5 20h-19z M12 10v4.5 M12 17.5h.01"),
        ["ui.notice.history"] = new("M6 16V11a6 6 0 0 1 12 0v5l1.5 2h-15z M10 20.5a2 2 0 0 0 4 0"),
        ["ui.notice.error"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M9 9l6 6 M15 9l-6 6"),
        ["ui.server"] = new("M4 5h16v6H4z M4 13h16v6H4z M8 8h.01 M8 16h.01"),
        ["link.kind.web"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M3.5 9h17 M3.5 15h17 M12 3c-2.6 2.6-3.5 5.6-3.5 9s.9 6.4 3.5 9 M12 3c2.6 2.6 3.5 5.6 3.5 9s-.9 6.4-3.5 9"),
        ["link.kind.place"] = new("M6 3h9l4 4v14H6z M14 3v5h5 M9 12h7 M9 16h4 M17.5 15.5l2 2-2 2"),
        ["link.kind.file"] = new("M3 6h6l2 3h10v10H3z M12 13l3 3-3 3 M8 16h7"),
        ["link.kind.mail"] = new("M3 6h18v12H3z M3 7l9 6 9-6"),
        ["link.place.top"] = new("M5 4h14 M12 20V8 M7 13l5-5 5 5"),
        ["link.place.heading"] = new("M6 5v14 M18 5v14 M6 12h12"),
        ["link.place.bookmark"] = new("M7 3h10v18l-5-4-5 4z"),
        ["link.place.table"] = new("M4 5h16v14H4z M4 10h16 M4 15h16 M10 5v14"),
        ["link.place.section"] = new("M4 4h16v6H4z M4 14h16v6H4z"),
        ["link.place.slide"] = new("M3 5h18v12H3z M8 21h8 M12 17v4"),
        ["link.place.sheet"] = new("M4 4h16v16H4z M4 9h16 M4 14h16 M9 4v16"),
        ["link.place.range"] = new("M4 4h16v16H4z M9 9h6v6H9z"),
        ["link.place.next"] = new("M5 12h12 M13 7l5 5-5 5"),
        ["link.place.previous"] = new("M19 12H7 M11 7l-5 5 5 5"),
        ["link.place.first"] = new("M6 5v14 M19 12H9 M13 7l-5 5 5 5"),
        ["link.place.last"] = new("M18 5v14 M5 12h10 M11 7l5 5-5 5"),
        ["size.lock"] = new("M9 7V5a3 3 0 0 1 6 0v2 M9 17v2a3 3 0 0 0 6 0v-2 M12 9v6"),
        ["folio.field.category.document"] = new("M6 3h9l4 4v14H6z M14 3v5h5 M9 13h2 M13 13h3 M9 17h7"),
        ["folio.field.category.info"] = new("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M12 11v6 M12 7.5h.01"),
        ["folio.field.category.functions"] = new("M9 4C7 4 7 6 7 8v2c0 1-1 2-2 2 1 0 2 1 2 2v2c0 2 0 4 2 4 M15 4c2 0 2 2 2 4v2c0 1 1 2 2 2-1 0-2 1-2 2v2c0 2 0 4-2 4"),
        ["folio.field.category.variables"] = new("M5 7h4l6 10h4 M15 7h4 M5 17h4"),
        ["folio.field.category.database"] = new("M4 6c0-1.7 3.6-3 8-3s8 1.3 8 3-3.6 3-8 3-8-1.3-8-3z M4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6 M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"),
        ["folio.reference.caption"] = new("M4 4h16v11H4z M7 12l3-3 3 3 2-2 2 2 M6 19h12"),
        ["folio.reference.footnote"] = new("M5 5h14 M5 9h14 M5 13h9 M5 18h6 M17 15v6 M15.5 16.5L17 15"),
        ["folio.reference.endnote"] = new("M5 4h14 M5 8h14 M5 12h14 M5 19h6 M17 16v6 M15.5 17.5L17 16"),
    };
}
