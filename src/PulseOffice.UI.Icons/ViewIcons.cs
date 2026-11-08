namespace PulseOffice.UI.Icons;

public sealed class ViewIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("ledger.view.normal", "M3 5h18v14H3z M3 9h18 M8 9v10"),
        ("ledger.view.pageBreaks", "M5 3h14v7H5z M5 14h14v7H5z M2 12h4 M9 12h6 M18 12h4"),
        ("shell.view.fullScreen", "M4 9V4h5 M20 9V4h-5 M4 15v5h5 M20 15v5h-5"),
        ("ledger.view.formulaBar", "M3 4h18v6H3z M6 7h3 M11 7h7 M3 13h18v7H3z"),
        ("shell.view.statusBar", "M3 4h18v12H3z M3 18h18v3H3z"),
        ("ledger.view.sheetTabs", "M3 5h18v10H3z M3 15h5l1 4h4l1-4 M15 15h3l1 4"),
        ("shell.view.toolbars", "M3 4h18v5H3z M6 6.5h.01 M10 6.5h.01 M14 6.5h.01 M3 12h18v8H3z"),
        ("shell.view.sidebar", "M3 4h18v16H3z M15 4v16 M17 9h2 M17 13h2"),
        ("ledger.view.navigator", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M15 9l-2 4-4 2 2-4z"),
        ("ledger.view.gallery", "M3 4h8v7H3z M13 4h8v7h-8z M3 13h8v7H3z M13 13h8v7h-8z"),
        ("ledger.view.hiddenMarkers", "M4 5h16v14H4z M9 5v14 M11 9l2 3-2 3 M15 9l2 3-2 3"),
        ("ledger.view.rowColumnHighlight", "M3 5h18v14H3z M3 10h18 M9 5v14 M3 5h6v5H3z"),
        ("ledger.view.snapToGrid", "M4 4v16 M10 4v16 M16 4v16 M4 8h16 M4 14h16 M4 20h16 M10 14l6-6"),
        ("ledger.view.gridLines", "M3 5h18v14H3z M3 10h18 M3 15h18 M9 5v14 M15 5v14"),
        ("ledger.view.headers", "M3 5h18v14H3z M3 9h18 M7 5v14 M3 5h18v4H3z M3 9h4v10H3z"),
        ("ledger.sheet.rightToLeft", "M20 6h-8a4 4 0 0 0 0 8h8 M20 6v16 M16 6v8 M7 10l-3 4 3 4"),
        ("ledger.view.zoomIn", "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M16 16l5 5 M11 8v6 M8 11h6"),
        ("ledger.view.zoomOut", "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M16 16l5 5 M8 11h6"),
        ("ledger.view.zoomReset", "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M16 16l5 5 M8 11h6 M11 8v6 M4 4h3"),
        ("ledger.view.zoom100", "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M16 16l5 5 M9 8v6 M12 9a1.5 2 0 1 1 3 0v4a1.5 2 0 1 1-3 0z"),
        ("ledger.zoom.optimal", "M4 4h16v16H4z M8 8h8v8H8z M8 8L4 4 M16 8l4-4 M8 16l-4 4 M16 16l4 4"),
        ("ledger.zoom.page", "M7 3h10v18H7z M4 6v12 M20 6v12"),
        ("ledger.zoom.pageWidth", "M3 6h18v12H3z M6 12h12 M9 9l-3 3 3 3 M15 9l3 3-3 3"),
        ("ledger.view.splitAtSelection", "M3 5h18v14H3z M3 12h18 M9 9l3-3 3 3 M9 15l3 3 3-3"),
        ("ledger.view.unsplit", "M3 5h18v14H3z M12 9l-3 3 3 3 M12 9h-3 M12 15h-3"),
        ("ledger.window.new", "M4 4h12v12H4z M8 8h12v12H8z"),
        ("ledger.window.close", "M4 4h16v16H4z M9 9l6 6 M15 9l-6 6"),
        ("ledger.worksheet.goTo", "M4 5h16v14H4z M8 12h8 M13 9l3 3-3 3"),
    ];
}
