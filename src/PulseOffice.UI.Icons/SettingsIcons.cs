namespace PulseOffice.UI.Icons;

public sealed class SettingsIcons() : DictionaryIconCatalog(Shapes)
{
    private static readonly (string, string)[] Shapes =
    [
        ("settings.page", "M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M12 3v2.5 M12 18.5V21 M3 12h2.5 M18.5 12H21 M5.6 5.6l1.8 1.8 M16.6 16.6l1.8 1.8 M18.4 5.6l-1.8 1.8 M7.4 16.6l-1.8 1.8"),
        ("settings.page.general", "M4 6h16 M4 12h16 M4 18h16 M8 4v4 M15 10v4 M10 16v4"),
        ("settings.page.language", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M3 12h18 M12 3a13 13 0 0 1 0 18 13 13 0 0 1 0-18z"),
        ("settings.page.appearance", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z M12 3v18a9 9 0 0 0 0-18z"),
        ("settings.page.documents", "M6 3h8l4 4v14H6z M14 3v4h4 M9 12h6 M9 16h6"),
        ("settings.page.devices", "M4 14h16v5H4z M6 14l2-6h8l2 6 M8 17h2"),
        ("settings.page.security", "M12 3l7 3v6c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6z M9 12l2 2 4-4"),
        ("settings.page.agent", "M8 7h8a3 3 0 0 1 3 3v6a3 3 0 0 1-3 3H8a3 3 0 0 1-3-3v-6a3 3 0 0 1 3-3z M12 7V4 M10 3h4 M9.5 12v1.5 M14.5 12v1.5 M2 12v3 M22 12v3"),
        ("settings.page.macros", "M8 8l-4 4 4 4 M16 8l4 4-4 4 M13 5l-2 14"),
        ("settings.page.diagnostics", "M3 12h4l2-5 4 10 2-5h6"),
        ("settings.page.module.sheets", "M4 4h16v16H4z M4 9h16 M4 14h16 M10 4v16"),
        ("settings.page.module.presentations", "M3 5h18v11H3z M12 16v4 M8 20h8"),
        ("settings.page.module.documents", "M6 3h12v18H6z M9 7h6 M9 11h6 M9 15h4"),
    ];
}
