using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.StylePanels;

namespace PulseOffice.Documents.UI.StyleCatalog;

public interface IFolioStyleActions
{
    string? Current(FolioEditor editor, StyleFamily family);

    bool CanApply(FolioEditor editor, StyleFamily family);

    void Apply(FolioEditor editor, StyleFamily family, string name);

    Task NewAsync(FolioEditor editor, StyleFamily family, CancellationToken ct);

    Task DeriveAsync(FolioEditor editor, StyleFamily family, string parent, CancellationToken ct);

    void Update(FolioEditor editor, StyleFamily family);

    void Delete(FolioEditor editor, StyleFamily family, string name);

    Task RenameAsync(FolioEditor editor, StyleFamily family, string name, CancellationToken ct);
}

public sealed class FolioStyleActions(
    IStyleEditing styles,
    IStyleFromSelection selection,
    IFolioStyleDialogs dialogs,
    IFolioStyleNames names,
    IFolioPageStyles pages,
    ITableFormatting tables) : IFolioStyleActions
{
    public string? Current(FolioEditor editor, StyleFamily family) => family switch
    {
        StyleFamily.Paragraph => editor.CurrentParagraph().StyleName ?? DefaultStyleNames.Standard,
        StyleFamily.Character => editor.EffectiveRun().StyleName,
        StyleFamily.List => editor.EffectiveParagraph().ListStyleName,
        StyleFamily.Page => pages.Current(editor).Name,
        StyleFamily.Frame => SelectedObject(editor)?.StyleName,
        StyleFamily.Table => editor.TableAt()?.Table.StyleName,
        _ => null,
    };

    public bool CanApply(FolioEditor editor, StyleFamily family) => !editor.Locked() && family switch
    {
        StyleFamily.Frame => SelectedObject(editor) is not null,
        StyleFamily.Table => editor.TableAt() is not null,
        _ => true,
    };

    public void Apply(FolioEditor editor, StyleFamily family, string name)
    {
        if (editor.RefusesEdit()) return;
        switch (family)
        {
            case StyleFamily.Paragraph:
                editor.Apply((documents, range) => documents.ApplyParagraphStyle(editor.Document, editor.History, range, name), editor.ParagraphRange());
                break;
            case StyleFamily.Character:
                editor.Apply((documents, range) => documents.ApplyCharacterStyle(editor.Document, editor.History, range, name), editor.CharacterRange());
                break;
            case StyleFamily.List:
                editor.Apply((documents, range) => documents.SetList(editor.Document, editor.History, range, name, editor.CurrentParagraph().BulletLevel), editor.ParagraphRange());
                break;
            case StyleFamily.Page:
                var anchor = pages.Anchor(editor);
                editor.Change(() => editor.Services.Editor.SetPageStyle(editor.Document, editor.History, anchor, name));
                break;
            case StyleFamily.Frame when SelectedObject(editor) is { } item:
                editor.Change(() => editor.Services.Editor.ChangeObject(editor.Document, editor.History, item.Id, target => target.StyleName = name));
                break;
            case StyleFamily.Table when editor.TableAt() is { } at:
                editor.Change(() => tables.ApplyStyle(editor.Document, editor.History, at.Table, name));
                break;
        }
    }

    public async Task NewAsync(FolioEditor editor, StyleFamily family, CancellationToken ct)
    {
        if (editor.RefusesDocumentEdit()) return;
        var basis = Current(editor, family) ?? Fallback(family);
        var suggestion = Unique(editor.Document.Styles, family, names.Title(basis));
        if (await dialogs.NameAsync(editor, family, suggestion, basis, ct).ConfigureAwait(true) is not { } name) return;
        switch (family)
        {
            case StyleFamily.Paragraph:
                editor.Change(() => selection.NewParagraphStyle(editor.Document, editor.History, editor.Caret.ParagraphId, name));
                break;
            case StyleFamily.Character:
                editor.Change(() => selection.NewCharacterStyle(editor.Document, editor.History, editor.CharacterRange(), name));
                break;
            default:
                AddCopy(editor, family, basis, name, asChild: false);
                break;
        }
    }

    public async Task DeriveAsync(FolioEditor editor, StyleFamily family, string parent, CancellationToken ct)
    {
        if (editor.RefusesDocumentEdit()) return;
        var suggestion = Unique(editor.Document.Styles, family, names.Title(parent));
        if (await dialogs.NameAsync(editor, family, suggestion, parent, ct).ConfigureAwait(true) is not { } name) return;
        AddCopy(editor, family, parent, name, asChild: true);
    }

    public void Update(FolioEditor editor, StyleFamily family)
    {
        if (editor.RefusesDocumentEdit()) return;
        switch (family)
        {
            case StyleFamily.Paragraph:
                editor.Change(() => selection.UpdateParagraphStyle(editor.Document, editor.History, editor.Caret.ParagraphId));
                break;
            case StyleFamily.Character:
                editor.Change(() => selection.UpdateCharacterStyle(editor.Document, editor.History, editor.CharacterRange()));
                break;
        }
    }

    public void Delete(FolioEditor editor, StyleFamily family, string name)
    {
        if (names.IsBuiltIn(family, name) || editor.RefusesDocumentEdit()) return;
        editor.Change(() => styles.Delete(editor.Document, editor.History, family, name));
    }

    public async Task RenameAsync(FolioEditor editor, StyleFamily family, string name, CancellationToken ct)
    {
        if (names.IsBuiltIn(family, name) || editor.RefusesDocumentEdit()) return;
        if (await dialogs.RenameAsync(family, name, ct).ConfigureAwait(true) is not { } chosen || chosen == name) return;
        if (editor.RefusesDocumentEdit() || names.IsBuiltIn(family, name)) return;
        var target = Taken(editor.Document.Styles, family, chosen) ? Unique(editor.Document.Styles, family, chosen) : chosen;
        editor.Change(() => styles.Rename(editor.Document, editor.History, family, name, target));
    }

    private AnchoredObject? SelectedObject(FolioEditor editor) =>
        editor.SelectedObjectId is { } id ? editor.Document.Objects.Find(id) : null;

    private string Fallback(StyleFamily family) => family switch
    {
        StyleFamily.List => DefaultStyleNames.Numbering123,
        StyleFamily.Frame => DefaultStyleNames.Frame,
        StyleFamily.Table => DefaultStyleNames.DefaultTableStyle,
        StyleFamily.Character => DefaultStyleNames.Emphasis,
        _ => DefaultStyleNames.Standard,
    };

    private void AddCopy(FolioEditor editor, StyleFamily family, string basis, string name, bool asChild)
    {
        var sheet = editor.Document.Styles;
        var parent = asChild ? basis : null;
        switch (family)
        {
            case StyleFamily.Paragraph when sheet.Paragraph.Find(basis) is { } paragraph:
                var newParagraph = new ParagraphStyle(name) { ParentName = parent ?? paragraph.ParentName, NextStyleName = paragraph.NextStyleName };
                if (!asChild)
                {
                    newParagraph.Paragraph = paragraph.Paragraph;
                    newParagraph.Character = paragraph.Character;
                }
                editor.Change(() => styles.Add(editor.Document, editor.History, sheet.Paragraph, newParagraph, "style-new-paragraph"));
                break;
            case StyleFamily.Character when sheet.Character.Find(basis) is { } character:
                var newCharacter = new CharacterStyle(name) { ParentName = parent ?? character.ParentName };
                if (!asChild) newCharacter.Character = character.Character;
                editor.Change(() => styles.Add(editor.Document, editor.History, sheet.Character, newCharacter, "style-new-character"));
                break;
            case StyleFamily.List when sheet.List.Find(basis) is { } list:
                editor.Change(() => styles.Add(editor.Document, editor.History, sheet.List, new ListStyle(name, list.Levels), "style-new-list"));
                break;
            case StyleFamily.Page when sheet.Page.Find(basis) is { } page:
                var created = new PageStyle(name);
                Copy((PageStyle)page.Clone(), created);
                editor.Change(() => styles.Add(editor.Document, editor.History, sheet.Page, created, "style-new-page"));
                break;
            case StyleFamily.Frame when sheet.Frame.Find(basis) is { } frame:
                var newFrame = new FrameStyle(name) { ParentName = parent ?? frame.ParentName, Frame = asChild ? FrameProperties.None : frame.Frame };
                editor.Change(() => styles.Add(editor.Document, editor.History, sheet.Frame, newFrame, "style-new-frame"));
                break;
            case StyleFamily.Table when sheet.Table.Find(basis) is { } table:
                var newTable = new TableStyle(name) { ParentName = parent ?? table.ParentName, Table = asChild ? TableProperties.None : table.Table };
                editor.Change(() => styles.Add(editor.Document, editor.History, sheet.Table, newTable, "style-new-table"));
                break;
        }
    }

    private string Unique(StyleSheet sheet, StyleFamily family, string basis)
    {
        for (var index = 1; ; index++)
        {
            var candidate = $"{basis} {index}";
            if (!Taken(sheet, family, candidate)) return candidate;
        }
    }

    private bool Taken(StyleSheet sheet, StyleFamily family, string name) => family switch
    {
        StyleFamily.Paragraph => sheet.Paragraph.Contains(name),
        StyleFamily.Character => sheet.Character.Contains(name),
        StyleFamily.List => sheet.List.Contains(name) || name == StyleSheet.OutlineStyleName,
        StyleFamily.Frame => sheet.Frame.Contains(name),
        StyleFamily.Table => sheet.Table.Contains(name),
        _ => sheet.Page.Contains(name),
    };

    private void Copy(PageStyle source, PageStyle target)
    {
        target.Size = source.Size;
        target.Orientation = source.Orientation;
        target.Margins = source.Margins;
        target.Columns = source.Columns;
        target.Usage = source.Usage;
        target.Background = source.Background;
        target.Borders = source.Borders;
        target.Watermark = source.Watermark;
        target.NumberFormat = source.NumberFormat;
        target.FirstPageDifferent = source.FirstPageDifferent;
        target.EvenPagesDifferent = source.EvenPagesDifferent;
        target.Header.Enabled = source.Header.Enabled;
        target.Header.Spacing = source.Header.Spacing;
        target.Header.MinimumHeight = source.Header.MinimumHeight;
        target.Footer.Enabled = source.Footer.Enabled;
        target.Footer.Spacing = source.Footer.Spacing;
        target.Footer.MinimumHeight = source.Footer.MinimumHeight;
    }
}

public sealed class FolioStylesSource : IStylesPanelSource
{
    private readonly IActiveFolioContext _context;
    private readonly IFolioStyleActions _actions;
    private readonly IFolioStyleDialogs _dialogs;
    private readonly IFolioStyleNames _names;
    private readonly IStyleResolver _resolver;
    private readonly IStyleUsage _usage;
    private readonly ITextCatalog _texts;
    private (TextDocument Document, long Revision, StyleUsageReport Report)? _usageCache;

    public FolioStylesSource(IActiveFolioContext context, IFolioStyleActions actions, IFolioStyleDialogs dialogs, IFolioStyleNames names,
        IStyleResolver resolver, IStyleUsage usage, ITextCatalog texts)
    {
        _context = context;
        _actions = actions;
        _dialogs = dialogs;
        _names = names;
        _resolver = resolver;
        _usage = usage;
        _texts = texts;
        _context.Changed += (_, _) => Rehook();
        Rehook();
    }

    private readonly IReadOnlyList<(StyleFamily Family, string Key)> _families =
    [
        (StyleFamily.Paragraph, "paragraph"),
        (StyleFamily.Character, "character"),
        (StyleFamily.Frame, "frame"),
        (StyleFamily.Page, "page"),
        (StyleFamily.List, "list"),
        (StyleFamily.Table, "table"),
    ];

    private readonly NaturalTitleComparer _order = new();

    private FolioEditor? _editor;

    public IReadOnlyList<StyleFamilyChoice> Families =>
        [.. _families.Select(entry => new StyleFamilyChoice(entry.Key, _texts[$"folio.styles.family.{entry.Key}"]) { Icon = $"panel.family.{entry.Key}", TracksUsage = true })];

    public bool CanApply(string family) => Editor is { } editor && _actions.CanApply(editor, Family(family));

    public event EventHandler? Changed;

    private FolioEditor? Editor => _context.Target?.Editor;

    public IReadOnlyList<StyleListEntry> Styles(string family)
    {
        if (Editor is not { } editor) return [];
        var sheet = editor.Document.Styles;
        var kind = Family(family);
        var usage = Usage(editor.Document);
        return kind switch
        {
            StyleFamily.Paragraph => Tree(kind, sheet.Paragraph.All, usage, style => Preview(_resolver.ResolveParagraphStyle(sheet, style.Name).Character)),
            StyleFamily.Character => Tree(kind, sheet.Character.All, usage, style => Preview(_resolver.ResolveCharacterStyle(sheet, style.Name))),
            StyleFamily.List => Flat(kind, sheet.List.All, usage),
            StyleFamily.Page => Flat(kind, sheet.Page.All, usage),
            StyleFamily.Frame => Tree(kind, sheet.Frame.All, usage, style => FramePreview(_resolver.ResolveFrameStyle(sheet, style.Name))),
            StyleFamily.Table => Tree(kind, sheet.Table.All, usage, style => TablePreview(_resolver.ResolveTableStyle(sheet, style.Name))),
            _ => [],
        };
    }

    public string? Current(string family) => Editor is { } editor ? _actions.Current(editor, Family(family)) : null;

    public bool Can(StylePanelAction action, string family, string? style)
    {
        if (Editor is not { } editor || editor.DocumentLocked()) return false;
        var kind = Family(family);
        return action switch
        {
            StylePanelAction.New => true,
            StylePanelAction.Derive => style is not null,
            StylePanelAction.Update => kind is StyleFamily.Paragraph or StyleFamily.Character && _actions.Current(editor, kind) is not null,
            StylePanelAction.Edit => style is not null && kind is StyleFamily.Paragraph or StyleFamily.Character or StyleFamily.List or StyleFamily.Page,
            _ => style is not null && !_names.IsBuiltIn(kind, style),
        };
    }

    public void Apply(string family, string style)
    {
        if (Editor is { } editor) _actions.Apply(editor, Family(family), style);
    }

    public async Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var kind = Family(family);
        switch (action)
        {
            case StylePanelAction.New:
                await _actions.NewAsync(editor, kind, ct).ConfigureAwait(true);
                break;
            case StylePanelAction.Derive when style is not null:
                await _actions.DeriveAsync(editor, kind, style, ct).ConfigureAwait(true);
                break;
            case StylePanelAction.Update:
                _actions.Update(editor, kind);
                break;
            case StylePanelAction.Edit when style is not null:
                await _dialogs.EditAsync(editor, kind, style, ct).ConfigureAwait(true);
                break;
            case StylePanelAction.Delete when style is not null:
                _actions.Delete(editor, kind, style);
                break;
            case StylePanelAction.Rename when style is not null:
                await _actions.RenameAsync(editor, kind, style, ct).ConfigureAwait(true);
                break;
        }
    }

    private void Rehook()
    {
        if (_editor is not null) _editor.Changed -= Relay;
        _editor = Editor;
        if (_editor is not null) _editor.Changed += Relay;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Relay(object? sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

    private StyleFamily Family(string key) => _families.FirstOrDefault(entry => entry.Key == key, _families[0]).Family;

    private StyleUsageReport Usage(TextDocument document)
    {
        if (_usageCache is { } cached && ReferenceEquals(cached.Document, document) && cached.Revision == document.Revision) return cached.Report;
        var report = _usage.Collect(document);
        _usageCache = (document, document.Revision, report);
        return report;
    }

    private StyleListEntry Entry(StyleFamily family, DocumentStyle style, int depth, StyleUsageReport usage, StylePreview? preview) =>
        new(style.Name, _names.Title(style), depth)
        {
            Preview = preview,
            Custom = !_names.IsBuiltIn(family, style.Name),
            InUse = usage.Uses(family, style.Name),
        };

    private IReadOnlyList<StyleListEntry> Flat<TStyle>(StyleFamily family, IReadOnlyList<TStyle> styles, StyleUsageReport usage) where TStyle : DocumentStyle =>
        [.. styles.Where(style => !style.Hidden).Select(style => Entry(family, style, 0, usage, null)).OrderBy(entry => entry.Title, _order)];

    private IReadOnlyList<StyleListEntry> Tree<TStyle>(StyleFamily family, IReadOnlyList<TStyle> styles, StyleUsageReport usage, Func<TStyle, StylePreview?> preview) where TStyle : DocumentStyle
    {
        var visible = styles.Where(style => !style.Hidden).ToList();
        var known = visible.Select(style => style.Name).ToHashSet(StringComparer.Ordinal);
        var children = visible.ToLookup(style => style.ParentName is { } parent && known.Contains(parent) ? parent : string.Empty);
        var result = new List<StyleListEntry>();
        void Walk(string parent, int depth)
        {
            foreach (var style in children[parent].OrderBy(_names.Title, _order))
            {
                result.Add(Entry(family, style, depth, usage, preview(style)));
                if (depth < 12) Walk(style.Name, depth + 1);
            }
        }
        Walk(string.Empty, 0);
        return result;
    }

    private StylePreview Preview(CharacterProperties character) => new()
    {
        Font = character.Font,
        Size = character.FontSize,
        Bold = character.Bold == true,
        Italic = character.Italic == true,
        Underline = character.Underline == true || character.DoubleUnderline == true,
        Strikethrough = character.Strikethrough == true,
        Foreground = Hex(character.Color),
        Background = Hex(character.Highlight),
    };

    private StylePreview FramePreview(FrameProperties frame) => new()
    {
        Swatch = Hex(frame.Background) ?? "#00FFFFFF",
        SwatchBorder = Hex(frame.BorderColor),
    };

    private StylePreview TablePreview(TableProperties table) => new()
    {
        Swatch = Hex(table.HeaderBackground ?? table.Background) ?? "#00FFFFFF",
        SwatchBorder = Hex(table.BorderColor),
    };

    private string? Hex(uint? color) => color is { } value && value >> 24 != 0 ? $"#{value:X8}" : null;
}
