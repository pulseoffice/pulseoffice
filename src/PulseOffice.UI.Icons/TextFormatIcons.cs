namespace PulseOffice.UI.Icons;

public sealed class TextFormatIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("text.style.regular", "M6 20l6-16 6 16 M8.5 14h7"),
        ("text.style.bold", "M7 4h6a4 4 0 0 1 0 8H7z M7 12h7a4 4 0 0 1 0 8H7z"),
        ("text.style.italic", "M10 4h8 M6 20h8 M14.5 4l-5 16"),
        ("text.style.boldItalic", "M9 4h6a3.5 3.5 0 0 1 0 7H8z M8 11h6.5a3.5 3.5 0 0 1 0 7H6z"),
        ("text.underline.none", "M7 3v7a5 5 0 0 0 10 0V3"),
        ("text.underline.single", "M7 3v7a5 5 0 0 0 10 0V3 M5 21h14"),
        ("text.underline.double", "M7 3v6a5 5 0 0 0 10 0V3 M5 18h14 M5 21h14"),
        ("text.position.normal", "M4 6l8 12 M12 6l-8 12 M15 18h5"),
        ("text.position.superscript", "M4 6l8 12 M12 6l-8 12 M16 9a2 2 0 1 1 4 0c0 1.5-4 2.5-4 4h4"),
        ("text.position.subscript", "M4 6l8 12 M12 6l-8 12 M16 15a2 2 0 1 1 4 0c0 1.5-4 2.5-4 4h4"),
        ("text.align.left", "M4 5h16 M4 10h10 M4 15h16 M4 20h10"),
        ("text.align.center", "M4 5h16 M7 10h10 M4 15h16 M7 20h10"),
        ("text.align.right", "M4 5h16 M10 10h10 M4 15h16 M10 20h10"),
        ("text.align.justify", "M4 6h16 M4 10h16 M4 14h16 M4 18h16"),
        ("page.orientation.portrait", "M7 3h10v18H7z M10 7h4 M10 10h4"),
        ("page.orientation.landscape", "M3 7h18v10H3z M7 10h6 M7 13h6"),
    ];
}
