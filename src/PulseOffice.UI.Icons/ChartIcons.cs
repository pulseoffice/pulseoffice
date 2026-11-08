namespace PulseOffice.UI.Icons;

public sealed class ChartIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("chart.family.recommended", "M12 3l2.6 5.6 6.1.7-4.5 4.2 1.2 6L12 16.6 6.6 19.5l1.2-6L3.3 9.3l6.1-.7z"),
        ("chart.family.column", "M4 20h16 M7 20V11 M12 20V5 M17 20v-6"),
        ("chart.family.bar", "M4 20V4 M4 7h12 M4 12h7 M4 17h15"),
        ("chart.family.line", "M4 20h16 M5 16l4.5-5 4 3L20 6"),
        ("chart.family.area", "M4 20h16 M4 16l5-5 4 3 7-8v14H4z"),
        ("chart.family.pie", "M12 3a9 9 0 1 0 9 9h-9z M12 3v9h9a9 9 0 0 0-9-9z"),
        ("chart.family.scatter", "M4 4v16h16 M8 15h.01 M11 11h.01 M14 13h.01 M17 7h.01 M9 8h.01"),
        ("chart.family.statistical", "M4 20h16 M7 5v4 M7 15v3 M5 9h4v6H5z M15 7v3 M15 16v2 M13 10h4v6h-4z"),
        ("chart.family.financial", "M4 20h16 M7 5v14 M5 8h2 M7 15h2 M12 4v12 M10 7h2 M12 12h2 M17 8v10 M15 10h2 M17 15h2"),
        ("chart.family.radar", "M12 3l8 6-3 10H7L4 9z M12 8l4 3-1.5 5h-5L8 11z"),
        ("chart.family.surface", "M4 4h16v16H4z M4 9.3h16 M4 14.6h16 M9.3 4v16 M14.6 4v16"),
        ("chart.family.hierarchy", "M4 4h16v16H4z M12 4v16 M12 11h8 M16 11v9"),
        ("chart.family.indicator", "M4 17a8 8 0 1 1 16 0 M12 17l4-5 M12 17h.01"),
    ];
}
