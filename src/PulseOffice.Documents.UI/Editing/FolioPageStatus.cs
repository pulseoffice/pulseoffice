using System.Globalization;
using System.Runtime.CompilerServices;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioPageJump
{
    int PageOf(FolioEditor editor, DocumentPosition position);

    bool Go(FolioEditor editor, int page);
}

public sealed class FolioPageJump : IFolioPageJump
{
    public int PageOf(FolioEditor editor, DocumentPosition position)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var starts = _indexes.GetValue(editor.Layout, IndexOf);
        if (!starts.TryGetValue(position.ParagraphId, out var lines)) return 1;
        var page = 1;
        foreach (var line in lines)
            if (line.Start <= position.Offset && line.Page > page) page = line.Page;
        return page;
    }

    private readonly ConditionalWeakTable<DocumentLayout, Dictionary<string, List<PageLineStart>>> _indexes = new();

    private static Dictionary<string, List<PageLineStart>> IndexOf(DocumentLayout layout)
    {
        var index = new Dictionary<string, List<PageLineStart>>(StringComparer.Ordinal);
        var pages = layout.Pages;
        for (var page = 0; page < pages.Count; page++)
            foreach (var line in pages[page].Lines)
            {
                if (!index.TryGetValue(line.ParagraphId, out var starts)) index[line.ParagraphId] = starts = [];
                starts.Add(new PageLineStart(line.Line.Start, page + 1));
            }
        return index;
    }

    private readonly record struct PageLineStart(int Start, int Page);

    public bool Go(FolioEditor editor, int page)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var pages = editor.Layout.Pages;
        if (pages.Count == 0) return false;
        var index = Math.Clamp(page, 1, pages.Count) - 1;
        if (pages[index].Lines.FirstOrDefault(line => editor.Services.Order.Contains(editor.Document, line.ParagraphId)) is not { } line) return false;
        var at = new DocumentPosition(line.ParagraphId, line.Line.Start);
        editor.Select(at, at);
        return true;
    }
}

public interface IFolioPageStatus
{
    StatusSegment Segment { get; }

    void Update();
}

public sealed class FolioPageStatus : IFolioPageStatus
{
    public const string SegmentId = "folio.page";

    private readonly IActiveFolioContext _context;
    private readonly IFolioPageJump _jump;
    private readonly ITextCatalog _texts;
    private FolioEditor? _watched;

    public FolioPageStatus(IActiveFolioContext context, IStatusBarService status, IFolioPageJump jump, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(status);
        _context = context;
        _jump = jump;
        _texts = texts;
        Segment = status.Register(SegmentId, StatusSegmentKind.Contextual);
        Segment.Accept = Accept;
        context.Changed += (_, _) => Watch();
        texts.LanguageChanged += (_, _) => Update();
        Watch();
    }

    public StatusSegment Segment { get; }

    public void Update()
    {
        Segment.AccessibleName = _texts["folio.goToPage.status"];
        Segment.Tooltip = _texts["folio.goToPage.tooltip"];
        if (_context.Target?.Editor is not { } editor || editor.Layout.Pages.Count == 0)
        {
            Segment.IsVisible = false;
            return;
        }
        Segment.IsVisible = true;
        Segment.Text = _texts.Format("folio.goToPage.position", _jump.PageOf(editor, editor.Caret), editor.Layout.Pages.Count);
    }

    private bool Accept(string typed)
    {
        if (_context.Target?.Editor is not { } editor) return false;
        var digits = new string(typed.SkipWhile(character => !char.IsDigit(character)).TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.CurrentCulture, out var page)) return false;
        var moved = _jump.Go(editor, page);
        Update();
        return moved;
    }

    private void Watch()
    {
        var editor = _context.Target?.Editor;
        if (!ReferenceEquals(editor, _watched))
        {
            if (_watched is not null) _watched.Changed -= OnEditorChanged;
            _watched = editor;
            if (editor is not null) editor.Changed += OnEditorChanged;
        }
        Update();
    }

    private void OnEditorChanged(object? sender, EventArgs e) => Update();
}
