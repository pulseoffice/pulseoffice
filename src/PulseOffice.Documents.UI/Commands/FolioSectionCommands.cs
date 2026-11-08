using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Pages;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioSectionCommands(IActiveFolioContext context, ISectionEditing sections, IFolioForms forms, ITextCatalog texts,
    PulseOffice.Documents.Protection.IProtectionKeys keys, PulseOffice.UI.Controls.Services.IStatusBarService status,
    IColumnLayoutEditors columns, ITabbedForms tabbed) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.insert.section") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => InsertAsync(ct) },
        Editing("folio.format.sections") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && sections.Sections(editor.Document).Count > 0,
            Execute = (_, ct) => EditAsync(ct),
        },
    ];

    private IReadOnlyList<FormField> Fields(SectionSettings settings, string prefix = "")
    {
        var columnsTitle = texts["folio.page.tab.columns"];
        var protection = texts["folio.section.group.protection"];
        return
        [
            new TextField(prefix + "name", texts["folio.section.name"], settings.Name),
            new ColumnLayoutField(prefix + "columnLayout", columnsTitle, new ColumnLayout(settings.Columns.Count, settings.Columns.Gap.ToCentimeters(), settings.Columns.Separator), "cm", columns)
            {
                Section = columnsTitle,
            },
            new CheckField(prefix + "balanced", texts["folio.section.balanced"], settings.Columns.Balanced) { Section = columnsTitle },
            new CheckField(prefix + "protected", texts["folio.section.protected"], settings.Protected) { Section = protection },
            new TextField(prefix + "password", texts["folio.section.password"], string.Empty, texts["folio.section.password.placeholder"])
            {
                Secret = true,
                Section = protection,
                ShownWhen = values => values.Flag(prefix + "protected"),
            },
            new CheckField(prefix + "hidden", texts["folio.section.hidden"], settings.Hidden) { Section = protection },
        ];
    }

    private SectionSettings Read(IReadOnlyDictionary<string, object> values, string fallback, byte[]? previous = null, string prefix = "")
    {
        var layout = values.TryGetValue(prefix + "columnLayout", out var chosen) && chosen is ColumnLayout picked ? picked : new ColumnLayout(1, 1.25, false);
        var balanced = values.Flag(prefix + "balanced");
        var sectionColumns = layout.Count <= 1 && !layout.Separator
            ? SectionColumns.Single with { Balanced = balanced }
            : new SectionColumns(Math.Clamp(layout.Count, 1, 10), Twip.FromCentimeters(layout.Gap)) { Separator = layout.Separator, Balanced = balanced };
        return new SectionSettings(values.Text(prefix + "name").Trim() is { Length: > 0 } name ? name : fallback, sectionColumns, values.Flag(prefix + "protected"), values.Flag(prefix + "hidden"))
        {
            ProtectionKey = values.Text(prefix + "password") is { Length: > 0 } password ? keys.Digest(password) : previous,
        };
    }

    private async Task InsertAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var name = texts.Format("folio.section.default", sections.Sections(editor.Document).Count + 1);
        var initial = new SectionSettings(name, new SectionColumns(2, Twip.FromCentimeters(1.25)), false, false);
        if (await forms.AskAsync("command.folio.insert.section.title", "folio.section.explanation", Fields(initial), ct).ConfigureAwait(true) is not { } values) return;
        var settings = Read(values, name);
        editor.Change(() => sections.Wrap(editor.Document, editor.History, editor.ParagraphRange(), settings));
    }

    private async Task EditAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var all = sections.Sections(editor.Document);
        if (all.Count == 0) return;
        var around = sections.Around(editor.Document, editor.Caret.ParagraphId);
        var initial = around is null ? 0 : Math.Max(0, all.ToList().IndexOf(around));
        var tabs = all.Select((section, index) =>
        {
            var prefix = Prefix(index);
            var fields = new List<FormField>();
            if (section.ProtectionKey is not null)
                fields.Add(new TextField(prefix + "unlock", texts["folio.section.unlockField"], string.Empty) { Secret = true, Section = texts["folio.section.group.unlock"] });
            fields.AddRange(Fields(new SectionSettings(section.Name, section.Columns, section.Protected, section.Hidden), prefix));
            fields.Add(new CheckField(prefix + "remove", texts["folio.section.remove"], false) { Section = texts["folio.section.group.protection"] });
            return new FormTab(section.Name, texts["folio.section.explanation"], fields);
        }).ToList();
        if (await tabbed.AskAsync("command.folio.format.sections.title", tabs, ct, initial).ConfigureAwait(true) is not { } values) return;
        var refused = false;
        var changes = new List<Action>();
        for (var index = 0; index < all.Count; index++)
        {
            var section = all[index];
            var prefix = Prefix(index);
            var before = new SectionSettings(section.Name, section.Columns, section.Protected, section.Hidden) { ProtectionKey = section.ProtectionKey };
            var after = Read(values, section.Name, section.ProtectionKey, prefix);
            var remove = values.Flag(prefix + "remove");
            if (!remove && Same(before, after)) continue;
            if (section.ProtectionKey is { } key && !keys.Matches(key, values.Text(prefix + "unlock")))
            {
                refused = true;
                continue;
            }
            var id = section.Id;
            changes.Add(remove
                ? () => sections.Unwrap(editor.Document, editor.History, id)
                : () => sections.Change(editor.Document, editor.History, id, after));
        }
        if (changes.Count > 0)
            editor.Change(() =>
            {
                using var transaction = editor.History.BeginTransaction("sections");
                foreach (var change in changes) change();
            });
        if (refused) status.Report(texts["protect.wrongPassword"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error);
    }

    private string Prefix(int index) => $"section{index}.";

    private bool Same(SectionSettings before, SectionSettings after) =>
        before.Name == after.Name && before.Columns == after.Columns && before.Protected == after.Protected && before.Hidden == after.Hidden
        && ReferenceEquals(before.ProtectionKey, after.ProtectionKey);
}
