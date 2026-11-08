using PulseOffice.Localization;
using PulseOffice.UI.Controls.StylePanels;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerStylesSource : IStylesPanelSource
{
    private const string Family = "cell";

    private readonly IActiveSheetContext _context;
    private readonly LedgerStyleCommands _commands;
    private readonly ITextCatalog _texts;

    public LedgerStylesSource(IActiveSheetContext context, LedgerStyleCommands commands, ITextCatalog texts)
    {
        _context = context;
        _commands = commands;
        _texts = texts;
        context.TargetChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<StyleFamilyChoice> Families => [new StyleFamilyChoice(Family, _texts["panel.styles.cells"]) { Icon = "panel.family.cell", TracksUsage = true }];

    public bool CanApply(string family) => _context.Workbook is not null;

    public IReadOnlyList<StyleListEntry> Styles(string family)
    {
        if (_context.Workbook is not { } workbook) return [];
        var used = workbook.Sheets
            .SelectMany(sheet => sheet.NonEmptyCells())
            .Select(pair => pair.Value.StyleName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var all = workbook.NamedStyles.All;
        var known = all.Select(named => named.Name).ToHashSet(StringComparer.Ordinal);
        var children = all.ToLookup(named => named.Parent is { } parent && known.Contains(parent) ? parent : string.Empty);
        var result = new List<StyleListEntry>();
        void Walk(string parent, int depth)
        {
            foreach (var named in children[parent])
            {
                var style = workbook.NamedStyles.StyleOf(named);
                result.Add(new StyleListEntry(named.Name, _commands.Title(named), depth)
                {
                    Custom = !named.IsBuiltIn,
                    InUse = used.Contains(named.Name),
                    Preview = new StylePreview
                    {
                        Font = style.Font.Family,
                        Size = style.Font.Size,
                        Bold = style.Font.Bold,
                        Italic = style.Font.Italic,
                        Underline = style.Font.Underline || style.Font.DoubleUnderline,
                        Strikethrough = style.Font.Strikethrough,
                        Foreground = style.Font.ColorHex,
                        Background = style.BackgroundHex,
                    },
                });
                if (depth < 8) Walk(named.Name, depth + 1);
            }
        }
        Walk(string.Empty, 0);
        return result;
    }

    public string? Current(string family) => _commands.CurrentName();

    public bool Can(StylePanelAction action, string family, string? style) => _context.Workbook is { } workbook && action switch
    {
        StylePanelAction.New => true,
        StylePanelAction.Update => _commands.CurrentName() is { Length: > 0 },
        StylePanelAction.Delete => style is not null && workbook.NamedStyles.Find(style) is { IsBuiltIn: false },
        StylePanelAction.Derive => style is not null && workbook.NamedStyles.Find(style) is not null,
        StylePanelAction.Rename => style is not null && _commands.CanRename(style),
        _ => false,
    };

    public void Apply(string family, string style) => _commands.ApplyNamed(style);

    public async Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct)
    {
        switch (action)
        {
            case StylePanelAction.New:
                await _commands.CreateAsync(ct).ConfigureAwait(true);
                break;
            case StylePanelAction.Derive when style is not null:
                await _commands.DeriveAsync(style, ct).ConfigureAwait(true);
                break;
            case StylePanelAction.Update:
                _commands.UpdateFromSelection();
                break;
            case StylePanelAction.Delete when style is not null:
                _commands.Delete(style);
                break;
            case StylePanelAction.Rename when style is not null:
                await _commands.RenameAsync(style, ct).ConfigureAwait(true);
                break;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
