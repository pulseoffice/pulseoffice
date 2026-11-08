namespace PulseOffice.UI.Icons;

public sealed class RibbonControlIcons() : DictionaryIconCatalog(Shapes)
{
    private const string FontColour = "M7 16L12 4l5 12 M9 12h6";
    private const string Fill = "M5 11l6-6 7 7-6 6z M18 13c1 2 2 3 2 4a2 2 0 0 1-4 0c0-1 1-2 2-4z";
    private const string Size = "M4 18L8 6l4 12 M5.5 14h5 M15 9l2.5-3 2.5 3 M15 15l2.5 3 2.5-3";
    private const string Family = "M4 18L9 5l5 13 M6 14h6 M16 18V9 M16 13a3 3 0 1 1 0 0.1";
    private const string Spacing = "M10 6h10 M10 12h10 M10 18h10 M5 4v16 M3 6l2-2 2 2 M3 18l2 2 2-2";
    private const string Line = "M4 20L20 4 M15 4h5v5";
    private const string Zoom = "M10 10m-6 0a6 6 0 1 0 12 0a6 6 0 1 0-12 0 M15 15l5 5 M7 10h6 M10 7v6";
    private const string Numbers = "M4 6h3v12 M4 18h6 M13 9a3 3 0 1 1 6 0c0 3-6 4-6 9h6";

    private static readonly (string, string)[] Shapes =
    [
        ("ledger.font.family", Family),
        ("ledger.font.size", Size),
        ("ledger.font.color", FontColour),
        ("ledger.fill.color", Fill),
        ("ledger.border.apply", "M4 4h16v16H4z M12 4v16 M4 12h16"),
        ("ledger.number.format", Numbers),
        ("ledger.view.zoomTo", Zoom),
        ("deck.format.fontFamily", Family),
        ("deck.format.fontSize", Size),
        ("deck.format.color", FontColour),
        ("deck.format.highlight", "M8 14l7-9 4 4-9 7z M8 14l-1 3 2 1 1-2 M4 21h16"),
        ("deck.paragraph.lineSpacing", Spacing),
        ("deck.object.fill", Fill),
        ("deck.object.lineColor", Line),
        ("deck.object.quickStyle", "M4 6h7v7H4z M13 6h7v7h-7z M4 15h7v5H4z M13 15h7v5h-7z"),
        ("deck.zoom.set", Zoom),
        ("border.none", "M4 4h2 M9 4h2 M14 4h2 M19 4h1v1 M20 9v2 M20 14v2 M20 19v1h-1 M15 20h-2 M10 20H8 M5 20H4v-1 M4 15v-2 M4 10V8 M12 9v2 M12 14v2 M9 12h-2 M17 12h-2"),
        ("border.all", "M4 4h16v16H4z M12 4v16 M4 12h16"),
        ("border.outside", "M4 4h16v16H4z M12 8v1 M12 11v2 M12 15v1 M8 12h1 M11 12h2 M15 12h1"),
        ("border.thickOutside", "M4 4h16v16H4z M5.5 5.5h13v13h-13z"),
        ("border.inside", "M12 4v16 M4 12h16 M4 4h1 M8 4h1 M15 4h1 M19 4h1 M4 20h1 M8 20h1 M15 20h1 M19 20h1"),
        ("border.insideHorizontal", "M4 12h16 M4 4h1 M8 4h1 M12 4h1 M16 4h1 M4 20h1 M8 20h1 M12 20h1 M16 20h1 M4 8v1 M20 8v1 M4 16v1 M20 16v1"),
        ("border.insideVertical", "M12 4v16 M4 4h1 M8 4h1 M16 4h1 M19 4h1 M4 20h1 M8 20h1 M16 20h1 M19 20h1 M4 8v1 M20 8v1 M4 16v1 M20 16v1"),
        ("border.left", "M4 4v16 M8 4h1 M12 4h1 M16 4h1 M20 4v1 M20 8v1 M20 12v1 M20 16v1 M8 20h1 M12 20h1 M16 20h1"),
        ("border.right", "M20 4v16 M4 4h1 M8 4h1 M12 4h1 M16 4h1 M4 8v1 M4 12v1 M4 16v1 M4 20h1 M8 20h1 M12 20h1 M16 20h1"),
        ("border.top", "M4 4h16 M4 8v1 M4 12v1 M4 16v1 M20 8v1 M20 12v1 M20 16v1 M4 20h1 M8 20h1 M12 20h1 M16 20h1"),
        ("border.bottom", "M4 20h16 M4 4h1 M8 4h1 M12 4h1 M16 4h1 M20 4v1 M4 8v1 M4 12v1 M4 16v1 M20 8v1 M20 12v1 M20 16v1"),
        ("border.topBottom", "M4 4h16 M4 20h16 M4 8v1 M4 12v1 M4 16v1 M20 8v1 M20 12v1 M20 16v1"),
        ("border.doubleBottom", "M4 18h16 M4 21h16 M4 4h1 M8 4h1 M12 4h1 M16 4h1 M4 8v1 M4 12v1 M20 8v1 M20 12v1"),
        ("border.thickBottom", "M4 19h16 M4 20.5h16 M4 4h1 M8 4h1 M12 4h1 M16 4h1 M4 8v1 M4 12v1 M20 8v1 M20 12v1"),
        ("border.diagonalUp", "M4 20L20 4 M4 4h1 M8 4h1 M12 4h1 M4 8v1 M4 12v1 M20 12v1 M20 16v1 M12 20h1 M16 20h1"),
        ("border.diagonalDown", "M4 4l16 16 M12 4h1 M16 4h1 M20 8v1 M4 12v1 M4 16v1 M4 20h1 M8 20h1 M20 4v1"),
    ];
}
