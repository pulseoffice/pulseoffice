namespace PulseOffice.UI.Icons;

public sealed class DataIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("data.connections", "M4 6h16v5H4z M4 13h16v5H4z M7 8.5h.01 M7 15.5h.01"),
        ("data.import", "M4 4h16v6H4z M12 12v8 M8 16l4 4 4-4"),
        ("ledger.data.provider", "M12 3c4 0 7 1.3 7 3v12c0 1.7-3 3-7 3s-7-1.3-7-3V6c0-1.7 3-3 7-3z M5 6c0 1.7 3 3 7 3s7-1.3 7-3 M5 12c0 1.7 3 3 7 3s7-1.3 7-3"),
        ("ledger.data.providerRefresh", "M12 3c4 0 7 1.3 7 3v6 M5 6v12c0 1.7 3 3 7 3 M5 6c0 1.7 3 3 7 3s7-1.3 7-3 M19 21a4 4 0 1 0-3.5-6 M15 12v3h3"),
        ("ledger.data.externalLinks", "M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1 M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1"),
        ("ledger.data.streams", "M4 8h16 M4 12h16 M4 16h16 M9 5l-2 3 2 3 M15 13l2 3-2 3"),
        ("ledger.data.xmlSource", "M8 7l-4 5 4 5 M16 7l4 5-4 5 M13 5l-2 14"),
        ("ledger.data.autoFilter", "M3 5h18l-7 8v6l-4 2v-8z"),
        ("ledger.data.standardFilter", "M3 5h18l-7 8v6l-4 2v-8z M16 18h6"),
        ("ledger.data.advancedFilter", "M3 5h18l-7 8v6l-4 2v-8z M16 16h6 M16 20h6"),
        ("ledger.data.textToColumns", "M4 5h6v14H4z M14 5h6v14h-6z M12 3v18"),
        ("ledger.data.validation", "M5 4h14v16H5z M8 12l3 3 5-6"),
        ("ledger.data.consolidate", "M4 4h6v6H4z M4 14h6v6H4z M14 9h6v6h-6z M10 7h4 M10 17h4 M14 12h-4"),
        ("ledger.data.subtotals", "M4 6h16 M4 11h16 M4 16h10 M14 19h8 M18 16v6"),
        ("ledger.data.random", "M4 4h16v16H4z M8 8h.01 M16 8h.01 M12 12h.01 M8 16h.01 M16 16h.01"),
        ("ledger.data.shuffle", "M4 7h4l8 10h4 M4 17h4l3-4 M16 3l4 4-4 4 M16 13l4 4-4 4"),
        ("ledger.outline.groupRows", "M4 5h16 M4 10h16 M4 15h16 M4 20h16 M21 5v15 M21 5h-3 M21 20h-3"),
        ("ledger.outline.groupColumns", "M5 4v16 M10 4v16 M15 4v16 M20 4v16 M5 21h15 M5 21v-3 M20 21v-3"),
        ("ledger.outline.ungroupRows", "M4 6h16 M4 12h16 M4 18h16 M19 3l3 3-3 3"),
        ("ledger.outline.auto", "M4 5h16 M4 11h12 M4 17h8 M20 14l2 3-2 3 M22 17h-6"),
        ("ledger.outline.expand", "M4 5h16 M4 19h16 M12 8v8 M9 11l3-3 3 3 M9 16l3 3 3-3"),
        ("ledger.outline.collapse", "M4 5h16 M4 19h16 M12 15V9 M9 12l3 3 3-3 M9 9l3-3 3 3"),
        ("ledger.outline.clear", "M4 6h16 M4 12h10 M4 18h16 M16 10l6 6 M22 10l-6 6"),
        ("ledger.analysis.goalSeek", "M12 3v18 M3 12h18 M12 12m-7 0a7 7 0 1 0 14 0 7 7 0 1 0-14 0 M12 12m-3 0a3 3 0 1 0 6 0 3 3 0 1 0-6 0"),
        ("ledger.analysis.solver", "M4 20V4 M4 16l5-5 4 3 7-8 M20 6v4h-4"),
        ("ledger.analysis.scenarios", "M4 6h16v12H4z M8 6V4h8v2 M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"),
        ("ledger.data.multipleOperations", "M4 4h16v16H4z M4 9h16 M9 4v16 M12 13h5 M12 17h5"),
        ("ledger.data.statistics", "M4 20h16 M7 20v-6 M12 20V8 M17 20v-9 M4 5l5 4 4-5 6 6"),
        ("ledger.data.form", "M5 3h14v18H5z M8 8h8 M8 12h8 M8 16h4"),
        ("ledger.insert.table", "M4 5h16v14H4z M4 10h16 M4 15h16 M10 5v14 M15 5v14"),
        ("ledger.database.define", "M12 3c4 0 7 1.3 7 3s-3 3-7 3-7-1.3-7-3 3-3 7-3z M5 6v12c0 1.7 3 3 7 3s7-1.3 7-3V6 M9 13h6"),
    ];
}
