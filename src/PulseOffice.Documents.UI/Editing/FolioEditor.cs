using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Document.Tables;
using System.Globalization;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.UI.Editing;

public enum CharacterToggle { Bold, Italic, Underline }

public sealed class FolioEditor : IDisposable
{
    private readonly FolioEditingServices _services;
    private readonly PendingCharacterFormat _pending = new();
    private readonly IDocumentLayoutEngine _engine;
    private readonly IPositionTracker _tracker;
    private readonly TrackedPosition _anchor;
    private readonly TrackedPosition _caret;
    private DocumentLayout? _layout;
    private DocumentLayout? _live;
    private int? _preeditCursor;
    private DocumentPosition _liveAt;
    private double? _preferredX;
    private bool _dirty;

    public FolioEditor(TextDocument document, UndoStack history, FolioEditingServices services, Func<bool> readOnly)
    {
        Document = document;
        History = history;
        _services = services;
        ReadOnly = readOnly;
        _engine = services.Engines();
        _tracker = services.Trackers.Create(document);
        var first = new DocumentPosition(services.Order.StoryOf(document, FirstBodyParagraph().Id)[0].Id, 0);
        _anchor = _tracker.Track(first);
        _caret = _tracker.Track(first);
        document.Changed += OnDocumentChanged;
        services.Assist.Learn(document);
    }

    public TextDocument Document { get; }

    public UndoStack History { get; }

    public Func<bool> ReadOnly { get; }

    public event EventHandler? Changed;

    public DocumentPosition Anchor => _anchor.Position;

    public DocumentPosition Caret => _caret.Position;

    public bool HasSelection => Anchor != Caret;

    public DocumentRange Selection =>
        _services.Order.Compare(Document, Anchor, Caret) <= 0 ? new DocumentRange(Anchor, Caret) : new DocumentRange(Caret, Anchor);

    public DocumentLayout Layout
    {
        get
        {
            if (_layout is null) _layout = _engine.Layout(Document);
            else if (_dirty) _layout = _engine.Relayout(Document, _layout);
            _dirty = false;
            return _layout;
        }
    }

    public string? Preedit { get; private set; }

    public DocumentPosition PreeditAt => Selection.Start;

    public DocumentLayout DisplayLayout
    {
        get
        {
            if (Preedit is not { } preedit) return Layout;
            var committed = Layout;
            if (_live is null || _liveAt != PreeditAt)
            {
                _liveAt = PreeditAt;
                _live = _services.Preedit.Compose(_engine, Document, committed, PreeditAt, preedit);
            }
            return _live;
        }
    }

    public DocumentPosition DisplayCaret =>
        Preedit is { } preedit ? PreeditAt with { Offset = PreeditAt.Offset + Math.Clamp(_preeditCursor ?? preedit.Length, 0, preedit.Length) } : Caret;

    public void SetPreedit(string? text, int? cursor)
    {
        var next = string.IsNullOrEmpty(text) || ReadOnly() ? null : text;
        if (next == Preedit && cursor == _preeditCursor) return;
        Preedit = next;
        _preeditCursor = cursor;
        _live = null;
        PreeditChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? PreeditChanged;

    public string? TextOf(string paragraphId)
    {
        try
        {
            return Paragraph(paragraphId).Text;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public PulseOffice.Text.Rich.TextParagraph? ContentOf(string paragraphId)
    {
        try
        {
            return Paragraph(paragraphId).Content;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    public DocumentLayout? CurrentLayout => _layout;

    public void Dispose()
    {
        Document.Changed -= OnDocumentChanged;
        _tracker.Dispose();
    }

    public string? SelectedObjectId
    {
        get;
        private set
        {
            field = value;
            _others.Clear();
        }
    }

    private readonly List<string> _others = [];

    public IReadOnlyList<string> SelectedObjectIds => SelectedObjectId is { } primary ? [primary, .. _others] : [];

    public void ToggleObject(string objectId)
    {
        if (Document.Objects.Find(objectId) is null) return;
        if (SelectedObjectId is null)
        {
            SelectObject(objectId);
            return;
        }
        if (objectId == SelectedObjectId)
        {
            var rest = _others.ToList();
            SelectedObjectId = rest.FirstOrDefault();
            _others.AddRange(rest.Skip(1));
        }
        else if (!_others.Remove(objectId)) _others.Add(objectId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SelectObject(string? objectId)
    {
        var next = objectId is not null && Document.Objects.Find(objectId) is not null ? objectId : null;
        if (next == SelectedObjectId && _others.Count == 0) return;
        SelectedObjectId = next;
        if (next is not null)
        {
            LeaveObjectStory(next);
            _anchor.Position = _caret.Position;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void LeaveObjectStory(string objectId)
    {
        if (!_services.Order.Contains(Document, _caret.Position.ParagraphId)) return;
        var story = _services.Stories.Find(Document, _caret.Position.ParagraphId).Story;
        if (!Document.Objects.All.Any(item => ReferenceEquals(item.Content.Story, story))) return;
        var paragraph = Document.Objects.Find(objectId)?.Anchor switch
        {
            PulseOffice.Documents.Objects.ParagraphAnchor anchor => anchor.ParagraphId,
            PulseOffice.Documents.Objects.CharacterAnchor anchor => anchor.ParagraphId,
            PulseOffice.Documents.Objects.AsCharacterAnchor anchor => anchor.ParagraphId,
            _ => null,
        };
        _caret.Position = paragraph is not null && _services.Order.Contains(Document, paragraph)
            ? new DocumentPosition(paragraph, 0)
            : new DocumentPosition(FirstBodyParagraph().Id, 0);
    }

    public bool EnterFrame(string objectId, DocumentPosition? at)
    {
        if (Document.Objects.Find(objectId) is not { Content.Story: { } box }) return false;
        var frames = Document.Objects.All.Select(item => item.Content.Story).OfType<PulseOffice.Documents.Blocks.BlockContainer>().ToList();
        if (at is { } hit && frames.Any(story => ReferenceEquals(story, _services.Stories.Find(Document, hit.ParagraphId).Story)))
        {
            PlaceCaret(hit, false);
            return true;
        }
        if (box.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().FirstOrDefault() is not { } first) return false;
        var last = _services.Order.StoryOf(Document, first.Id);
        PlaceCaret(new DocumentPosition(last[^1].Id, last[^1].Length), false);
        return true;
    }

    public bool DeleteSelectedObject()
    {
        if (ReadOnly() || SelectedObjectId is not { } id || Document.Objects.Find(id) is { } item && ObjectLocked(item)) return false;
        SelectedObjectId = null;
        _services.Editor.RemoveObject(Document, History, id);
        Normalize();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool ResizeSelectedObject(PulseOffice.Foundation.Units.DocumentSize size) =>
        ChangeSelectedObject(item => item.Size != size, item => item.Size = size);

    public bool RotateSelectedObject(double degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        return ChangeSelectedObject(item => Math.Abs(item.RotationDegrees - normalized) > 0.01, item => item.RotationDegrees = normalized);
    }

    public bool ObjectLocked(PulseOffice.Documents.Objects.AnchoredObject item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (DocumentLocked()) return true;
        return item.Anchor.Accept(new PulseOffice.Documents.Objects.AnchorParagraph()) is { } paragraph
            && (Generated(paragraph) || _services.Guard.Check(Document, paragraph) != PulseOffice.Documents.Protection.EditVerdict.Allowed);
    }

    public void Refresh() => Changed?.Invoke(this, EventArgs.Empty);

    public void Reflow()
    {
        _engine.Invalidate();
        _layout = null;
        _live = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool ChangeSelectedObject(Func<PulseOffice.Documents.Objects.AnchoredObject, bool> differs, Action<PulseOffice.Documents.Objects.AnchoredObject> change)
    {
        if (ReadOnly() || SelectedObjectId is not { } id || Document.Objects.Find(id) is not { } item || ObjectLocked(item) || !differs(item)) return false;
        _services.Editor.ChangeObject(Document, History, id, change);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public IReadOnlyList<DocumentRange> BlockRanges { get; private set; } = [];

    public void SelectBlock(IReadOnlyList<DocumentRange> ranges)
    {
        if (ranges.Count == 0) return;
        Select(ranges[0].Start, ranges[^1].End);
        BlockRanges = ranges;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Select(DocumentPosition anchor, DocumentPosition caret)
    {
        BlockRanges = [];
        SelectedObjectId = null;
        _anchor.Position = Clamp(anchor);
        _caret.Position = Clamp(caret);
        _preferredX = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void PlaceCaret(DocumentPosition position, bool extend) => Select(extend ? Anchor : position, position);

    public void SelectWord(DocumentPosition position)
    {
        var text = Paragraph(position.ParagraphId).Text;
        var (start, end) = _services.Words.Enclosing(text, position.Offset);
        Select(position with { Offset = start }, position with { Offset = end });
    }

    public void SelectParagraph(DocumentPosition position) =>
        Select(position with { Offset = 0 }, position with { Offset = Paragraph(position.ParagraphId).Length });

    public void SelectAll()
    {
        var story = _services.Order.StoryOf(Document, Caret.ParagraphId);
        Select(new DocumentPosition(story[0].Id, 0), new DocumentPosition(story[^1].Id, story[^1].Length));
    }

    public void Move(CaretMotion motion, bool extend, int pageLines = 20)
    {
        BlockRanges = [];
        if (!extend && HasSelection && motion is CaretMotion.Left or CaretMotion.Right)
        {
            var edge = motion == CaretMotion.Left ? Selection.Start : Selection.End;
            Select(edge, edge);
            return;
        }
        var target = _services.Navigator.Move(Document, Layout, Caret, motion, _preferredX, pageLines);
        _anchor.Position = extend ? Anchor : target.Position;
        _caret.Position = target.Position;
        _preferredX = target.PreferredX;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Locked() => ReadOnly() || Generated(Anchor.ParagraphId) || Generated(Caret.ParagraphId);

    public event EventHandler<PulseOffice.Documents.Protection.EditVerdict>? Refused;

    public bool RefusesEdit()
    {
        if (ReadOnly()) return true;
        var verdict = Verdict();
        if (verdict == PulseOffice.Documents.Protection.EditVerdict.Allowed) return false;
        Refused?.Invoke(this, verdict);
        return true;
    }

    public bool RefusesEdit(string paragraphId)
    {
        if (ReadOnly()) return true;
        var verdict = _services.Guard.Check(Document, paragraphId);
        if (verdict == PulseOffice.Documents.Protection.EditVerdict.Allowed) return false;
        Refused?.Invoke(this, verdict);
        return true;
    }

    public bool DocumentLocked() => ReadOnly() || Document.Protection.Restriction is PulseOffice.Documents.Protection.DocumentRestriction.ReadOnly
        or PulseOffice.Documents.Protection.DocumentRestriction.Comments or PulseOffice.Documents.Protection.DocumentRestriction.Forms;

    public bool RefusesEdit(DocumentRange range) => RefusesEdit() || RefusesRange(range);

    public bool RefusesRange(DocumentRange range)
    {
        if (ReadOnly()) return true;
        var verdict = _services.Order.Between(Document, range.Start, range.End).Select(paragraph => _services.Guard.Check(Document, paragraph.Id))
            .FirstOrDefault(candidate => candidate != PulseOffice.Documents.Protection.EditVerdict.Allowed);
        if (verdict == PulseOffice.Documents.Protection.EditVerdict.Allowed) return false;
        Refused?.Invoke(this, verdict);
        return true;
    }

    public bool RefusesDocumentEdit()
    {
        if (ReadOnly()) return true;
        if (!DocumentLocked()) return false;
        Refused?.Invoke(this, PulseOffice.Documents.Protection.EditVerdict.ProtectedDocument);
        return true;
    }

    private bool Generated(string paragraphId) => _services.Guard.Check(Document, paragraphId) != PulseOffice.Documents.Protection.EditVerdict.Allowed;

    public PulseOffice.Documents.Protection.EditVerdict Verdict() =>
        new[] { Anchor.ParagraphId, Caret.ParagraphId }.Select(id => _services.Guard.Check(Document, id))
            .FirstOrDefault(verdict => verdict != PulseOffice.Documents.Protection.EditVerdict.Allowed);

    private bool FillsForm() => !ReadOnly() && _services.Controls.Forms.Fills(Document, HasSelection ? Selection : DocumentRange.At(Caret));

    private bool RefusesFilling() => !FillsForm() && RefusesEdit();

    private bool FormRefuses(DocumentRange range) => _services.Controls.Forms.Restricts(Document) && !_services.Controls.Forms.Fills(Document, range);

    public bool Type(string text) =>
        Insert(text, () => History.BeginTransaction("typing", TypingRun.Of(HasSelection ? Selection.Start : Caret, text)), assisted: true);

    public bool PasteText(string text) =>
        Insert(text.ReplaceLineEndings("\n"), () => History.BeginTransaction("paste-unformatted"), assisted: false);

    private bool Insert(string text, Func<IDisposable> transaction, bool assisted)
    {
        SetPreedit(null, null);
        if (RefusesFilling() || text.Length == 0) return false;
        if (FillPlaceholder(text)) return true;
        if (!HasSelection && !Typable(Caret, text)) return !_services.Controls.Forms.Restricts(Document) && Beside(text);
        var pending = HasSelection ? null : _pending.Take(Caret);
        using (transaction())
        {
            DeleteSelection();
            var start = Caret;
            _services.Editor.InsertText(Document, History, start, text);
            if (pending is not null && !text.Contains('\n') && !text.Contains('\r'))
                _services.Editor.SetCharacter(Document, History, new DocumentRange(start, start with { Offset = start.Offset + text.Length }), pending);
            Recalculate();
        }
        Collapse();
        if (!assisted) return true;
        if (text.Length == 1 && _services.Boundary.IsBoundary(text[0])) Correct();
        _services.Assist.AfterTyping(this, text);
        return true;
    }

    public PulseOffice.Documents.Controls.ContentControlSpan? ControlAt(DocumentPosition position) => _services.Controls.Catalog.At(Document, position);

    private bool Typable(DocumentPosition at, string text)
    {
        if (_services.Controls.Guard.LocksTyping(Document, at)) return false;
        if (ControlAt(at) is not { } span) return true;
        var breaks = text.Contains('\u2028') || text.Contains('\n') || text.Contains('\r');
        return !breaks || span.Control.Kind == PulseOffice.Documents.Controls.ContentControlKind.RichText
            || span.Control is { Kind: PulseOffice.Documents.Controls.ContentControlKind.PlainText, MultiLine: true };
    }

    private bool Beside(string text)
    {
        if (ControlAt(Caret) is not { } span || !span.InOneParagraph || Caret != span.Start && Caret != span.End) return false;
        var outer = Caret == span.End ? span.End : span.Start;
        _services.Controls.Editing.InsertBeside(Document, History, span.Id, outer, text);
        var after = outer with { Offset = outer.Offset + text.Length };
        Select(after, after);
        return true;
    }

    private bool FillPlaceholder(string text)
    {
        var at = HasSelection ? Selection.Start : Caret;
        if (ControlAt(at) is not { Control.ShowingPlaceholder: true } span || !_services.Controls.Guard.AcceptsTyping(span.Control) || !span.InOneParagraph) return false;
        if (HasSelection && (Selection.Start.Offset < span.Start.Offset || Selection.End.ParagraphId != span.End.ParagraphId || Selection.End.Offset > span.End.Offset)) return false;
        var value = text.Replace('\n', '\u2028');
        _services.Controls.Editing.Fill(Document, History, span.Id, value);
        var end = new DocumentPosition(span.Start.ParagraphId, span.Start.Offset + value.Length);
        Select(end, end);
        return true;
    }

    private void Refill() =>
        _services.Controls.Editing.Refill(Document, History, _services.Controls.Placeholders.For(PulseOffice.Documents.Controls.ContentControlKind.RichText));

    public bool Backspace()
    {
        if (RefusesFilling() || !HasSelection && Caret.Offset == 0 && Neighbour(Caret.ParagraphId, -1) is { } previous && Generated(previous.Id)) return false;
        using (History.BeginTransaction("delete"))
        {
            var done = BackspaceCore();
            if (done)
            {
                Recalculate();
                Refill();
            }
            return done;
        }
    }

    private bool BackspaceCore()
    {
        if (HasSelection) return !Shielded(Selection) && Erase();
        var caret = Caret;
        if (caret.Offset > 0)
        {
            var text = Paragraph(caret.ParagraphId).Text;
            var from = _services.Clusters.Previous(text, caret.Offset);
            if (Shielded(DocumentRange.In(caret.ParagraphId, from, caret.Offset))) return false;
            _services.Editor.Delete(Document, History, DocumentRange.In(caret.ParagraphId, from, caret.Offset));
            Collapse();
            return true;
        }
        if (Neighbour(caret.ParagraphId, -1) is not { } previous || FormRefuses(new DocumentRange(new DocumentPosition(previous.Id, previous.Length), caret))) return false;
        _services.Editor.Delete(Document, History, new DocumentRange(new DocumentPosition(previous.Id, previous.Length), caret));
        Collapse();
        return true;
    }

    public bool DeleteForward()
    {
        if (RefusesFilling() || !HasSelection && Caret.Offset >= Paragraph(Caret.ParagraphId).Length && Neighbour(Caret.ParagraphId, 1) is { } next && Generated(next.Id)) return false;
        using (History.BeginTransaction("delete"))
        {
            var done = DeleteForwardCore();
            if (done)
            {
                Recalculate();
                Refill();
            }
            return done;
        }
    }

    private bool DeleteForwardCore()
    {
        if (HasSelection) return !Shielded(Selection) && Erase();
        var caret = Caret;
        var text = Paragraph(caret.ParagraphId).Text;
        if (caret.Offset < text.Length)
        {
            var to = _services.Clusters.Next(text, caret.Offset);
            if (Shielded(DocumentRange.In(caret.ParagraphId, caret.Offset, to))) return false;
            _services.Editor.Delete(Document, History, DocumentRange.In(caret.ParagraphId, caret.Offset, to));
            Collapse();
            return true;
        }
        if (Neighbour(caret.ParagraphId, 1) is not { } next || FormRefuses(new DocumentRange(caret, new DocumentPosition(next.Id, 0)))) return false;
        _services.Editor.Delete(Document, History, new DocumentRange(caret, new DocumentPosition(next.Id, 0)));
        Collapse();
        return true;
    }

    public bool SplitParagraph()
    {
        if (RefusesFilling()) return false;
        if (!HasSelection && ControlAt(Caret) is { } inside && inside.Control.Kind != PulseOffice.Documents.Controls.ContentControlKind.RichText)
            return inside.Control is { Kind: PulseOffice.Documents.Controls.ContentControlKind.PlainText, MultiLine: true } && Type("\u2028");
        if (_services.Controls.Forms.Restricts(Document)) return false;
        if (_services.Assist.AcceptCompletion(this) || _services.Assist.EndEmptyListItem(this)) return true;
        using (History.BeginTransaction("split-paragraph"))
        {
            DeleteSelection();
            var formatted = Caret.ParagraphId;
            var applied = _services.Assist.FormatBeforeSplit(this);
            _services.Editor.SplitParagraph(Document, History, Caret);
            _services.Assist.SettleAfterSplit(this, applied, formatted);
        }
        Collapse();
        return true;
    }

    public bool LineBreak() => Type("\u2028");

    public bool Replace(DocumentRange range, string text)
    {
        if (ReadOnly() || _services.Guard.Check(Document, range.Start.ParagraphId) != PulseOffice.Documents.Protection.EditVerdict.Allowed || Shielded(range)) return false;
        var end = _tracker.Track(range.Start);
        try
        {
            using (History.BeginTransaction("replace"))
            {
                if (!range.IsCollapsed) _services.Editor.Delete(Document, History, range);
                if (text.Length > 0) _services.Editor.InsertText(Document, History, range.Start, text);
            }
            Select(end.Position, end.Position);
        }
        finally
        {
            _tracker.Release(end);
        }
        return true;
    }

    public string SelectedText()
    {
        if (BlockRanges.Count > 0)
            return string.Join('\n', BlockRanges.Select(range => Paragraph(range.Start.ParagraphId).Text[Math.Min(range.Start.Offset, Paragraph(range.Start.ParagraphId).Length)..Math.Min(range.End.Offset, Paragraph(range.Start.ParagraphId).Length)]));
        if (SelectedObjectId is not null || !HasSelection) return string.Empty;
        var range = Selection;
        var paragraphs = _services.Order.Between(Document, range.Start, range.End);
        return string.Join('\n', paragraphs.Select(paragraph =>
        {
            var from = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
            var to = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length;
            return paragraph.Text[Math.Min(from, paragraph.Length)..Math.Clamp(to, from, paragraph.Length)].Replace('\u2028', '\n');
        }));
    }

    public DocumentFragment? Copy() =>
        BlockRanges.Count > 0 ? BlockFragment()
        : SelectedObjectId is { } id ? _services.Objects.Of(Document, id) : HasSelection ? _services.Clipboard.Copy(Document, Selection) : null;

    private DocumentFragment BlockFragment() =>
        new(new PulseOffice.Documents.Blocks.BlockContainer(SelectedText().Split('\n').Select((line, index) => new PulseOffice.Documents.Blocks.ParagraphBlock("block" + index, line))),
            Document.Styles, [], [], []);

    public DocumentFragment? Cut()
    {
        if (RefusesEdit()) return null;
        if (SelectedObjectId is { } id)
        {
            var copied = _services.Objects.Of(Document, id);
            DeleteSelectedObject();
            return copied;
        }
        if (BlockRanges.Count > 0)
        {
            var block = BlockFragment();
            using (History.BeginTransaction("cut"))
                DeleteSelection();
            Collapse();
            return block;
        }
        if (!HasSelection || Shielded(Selection)) return null;
        var fragment = _services.Clipboard.Cut(Document, History, Selection);
        Collapse();
        return fragment;
    }

    public bool Paste(DocumentFragment fragment)
    {
        if (RefusesEdit() || fragment.IsEmpty) return false;
        SelectedObjectId = null;
        using (History.BeginTransaction("paste"))
        {
            DeleteSelection();
            _services.Clipboard.Paste(Document, History, Caret, fragment);
            Recalculate();
        }
        Collapse();
        return true;
    }

    public bool Toggle(CharacterToggle toggle)
    {
        var range = HasSelection ? Selection : WordAt(Caret);
        if (RefusesEdit(range)) return false;
        if (range.IsCollapsed)
        {
            _pending.Toggle(toggle, !IsOn(toggle), Caret);
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        var on = !IsOn(toggle, range);
        var properties = toggle switch
        {
            CharacterToggle.Bold => new CharacterProperties { Bold = on },
            CharacterToggle.Italic => new CharacterProperties { Italic = on },
            _ => new CharacterProperties { Underline = on },
        };
        _services.Editor.SetCharacter(Document, History, range, properties);
        return true;
    }

    public bool IsOn(CharacterToggle toggle) =>
        (HasSelection ? null : _pending.State(toggle, Caret)) ?? IsOn(toggle, HasSelection ? Selection : DocumentRange.At(Caret));

    public DocumentRange CharacterRange() => HasSelection ? Selection : WordAt(Caret);

    public DocumentRange ParagraphRange() => HasSelection ? Selection : DocumentRange.At(Caret);

    public FolioEditingServices Services => _services;

    public bool Apply(Action<IDocumentEditor, DocumentRange> change, DocumentRange range)
    {
        if (RefusesEdit(range)) return false;
        change(_services.Editor, range);
        Normalize();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public FormatSample? Brush { get; private set; }

    public event EventHandler? BrushChanged;

    public void Arm(FormatSample? sample)
    {
        Brush = sample;
        BrushChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool PaintBrush()
    {
        if (Brush is not { } sample) return false;
        var collapsed = !HasSelection;
        var range = collapsed ? CharacterRange() : Selection;
        Arm(null);
        if (RefusesEdit(range)) return false;
        return Change(() => _services.Brush.Paint(Document, History, range, sample, collapsed));
    }

    public bool Change(Action change)
    {
        if (ReadOnly()) return false;
        change();
        Normalize();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Insert(Action<IDocumentEditor, DocumentPosition> change, string description) => Insert(change, description, null);

    public bool Insert(Action<IDocumentEditor, DocumentPosition> change, string description, PulseOffice.Document.Commands.IUndoMergeKey? mergeKey)
    {
        if (RefusesEdit()) return false;
        using (History.BeginTransaction(description, mergeKey))
        {
            DeleteSelection();
            change(_services.Editor, Caret);
        }
        Collapse();
        return true;
    }

    public void EnterNote(PulseOffice.Documents.Annotations.Note note)
    {
        if (note.Content.Blocks.OfType<ParagraphBlock>().LastOrDefault() is not { } paragraph) return;
        var end = new DocumentPosition(paragraph.Id, paragraph.Length);
        Select(end, end);
    }

    public PulseOffice.Text.Rich.TextRun EffectiveRun()
    {
        var position = Clamp(HasSelection ? Selection.Start : Caret);
        var paragraph = Paragraph(position.ParagraphId);
        var content = paragraph.Content;
        var source = content.Runs.Count == 0 ? [new PulseOffice.Text.Rich.TextRun(content.Text)] : content.Runs;
        var offset = HasSelection ? position.Offset + 1 : position.Offset;
        var at = 0;
        var chosen = source[0];
        foreach (var run in source)
        {
            chosen = run;
            at += run.Text.Length;
            if (offset <= at) break;
        }
        return _services.Styles.EffectiveRun(Document.Styles, content, chosen);
    }

    public PulseOffice.Text.Rich.TextParagraph EffectiveParagraph() =>
        _services.Styles.EffectiveParagraph(Document.Styles, Paragraph(Clamp(Caret).ParagraphId).Content);

    public PulseOffice.Text.Rich.TextParagraph CurrentParagraph() => Paragraph(Clamp(Caret).ParagraphId).Content;

    public FolioTableServices Tables => _services.Tables;

    public TableSelection? TableAt() => _services.Tables.Selections.Select(Document, Anchor, Caret);

    public bool InTable => _services.Tables.Editing.Locate(Document, Caret.ParagraphId) is not null;

    public TableSelection? CellRange() => HasSelection && TableAt() is { Range.IsSingleCell: false } selection ? selection : null;

    public CultureInfo Culture() => _services.Cultures.For(EffectiveRun().Language, Document.Language);

    public void SelectCells(TableBlock table, TableCellRange range)
    {
        var first = table.Cell(range.Top, range.Left).Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault();
        var corner = table.Rows[range.Bottom].Cells[range.Right];
        if (corner.Covered)
            corner = Enumerable.Range(0, range.Right + 1).Reverse().Select(column => table.Rows[range.Bottom].Cells[column]).FirstOrDefault(cell => !cell.Covered) ?? corner;
        var last = corner.Content.Blocks.OfType<ParagraphBlock>().LastOrDefault();
        if (first is null || last is null) return;
        Select(new DocumentPosition(first.Id, 0), new DocumentPosition(last.Id, last.Length));
    }

    public bool MoveToCell(bool backwards)
    {
        if (_services.Tables.Editing.Locate(Document, Caret.ParagraphId) is not { } at) return false;
        _services.Tables.Recognition.Leave(this, at);
        var table = at.Table;
        var row = at.Row;
        var column = at.Column;
        do
        {
            column += backwards ? -1 : 1;
            if (column >= table.Rows[row].Cells.Count)
            {
                column = 0;
                row++;
            }
            else if (column < 0)
            {
                row--;
                if (row < 0) return true;
                column = table.Rows[row].Cells.Count - 1;
            }
            if (row >= table.Rows.Count)
            {
                if (RefusesEdit()) return true;
                _services.Tables.Editing.InsertRow(Document, History, at with { Row = table.Rows.Count - 1 }, true);
                Normalize();
            }
        }
        while (table.Rows[row].Cells[column].Covered);
        var paragraphs = table.Rows[row].Cells[column].Content.Blocks.OfType<ParagraphBlock>().ToList();
        if (paragraphs.Count == 0) return true;
        Select(new DocumentPosition(paragraphs[0].Id, 0), new DocumentPosition(paragraphs[^1].Id, paragraphs[^1].Length));
        return true;
    }

    public bool ResizeColumn(TableBlock table, int column, double points)
    {
        if (TableParagraphs(table).Any(RefusesEdit)) return false;
        _services.Tables.Geometry.ResizeColumn(Document, History, table, column, points);
        Normalize();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private IEnumerable<string> TableParagraphs(TableBlock table) =>
        ReadOnly() ? [Caret.ParagraphId] : table.Rows.SelectMany(row => row.Cells).Select(cell => cell.Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault()?.Id).OfType<string>();

    private void Recalculate()
    {
        if (_services.Tables.Editing.Locate(Document, Caret.ParagraphId) is not { } at) return;
        if (!at.Table.Rows.Any(row => row.Cells.Any(cell => cell.Formula is not null))) return;
        _services.Tables.Formulas.Recalculate(Document, History, at.Table, Culture());
    }

    public void Normalize()
    {
        _anchor.Position = Clamp(_anchor.Position);
        _caret.Position = Clamp(_caret.Position);
    }

    private bool IsOn(CharacterToggle toggle, DocumentRange range)
    {
        var paragraphs = _services.Order.Between(Document, range.Start, range.End);
        var runs = new List<PulseOffice.Text.Rich.TextRun>();
        foreach (var paragraph in paragraphs)
        {
            var content = paragraph.Content;
            var from = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
            var to = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length;
            var position = 0;
            var source = content.Runs.Count == 0 ? [new PulseOffice.Text.Rich.TextRun(content.Text)] : content.Runs;
            foreach (var run in source)
            {
                var end = position + run.Text.Length;
                var touches = from == to ? from >= position && from <= end : end > from && position < to;
                if (touches) runs.Add(_services.Styles.EffectiveRun(Document.Styles, content, run));
                position = end;
            }
        }
        return runs.Count > 0 && runs.All(run => toggle switch
        {
            CharacterToggle.Bold => run.Bold,
            CharacterToggle.Italic => run.Italic,
            _ => run.Underline,
        });
    }

    private DocumentRange WordAt(DocumentPosition position)
    {
        var (start, end) = _services.Words.Around(Paragraph(position.ParagraphId).Text, position.Offset);
        return DocumentRange.In(position.ParagraphId, start, end);
    }

    private void Correct()
    {
        if (!_services.AutoCorrect.Enabled) return;
        var caret = Caret;
        var text = Paragraph(caret.ParagraphId).Text;
        var before = text.Remove(caret.Offset - 1, 1);
        var options = _services.AutoCorrect.Options with { Language = Document.Language };
        if (_services.Boundary.AfterTyping(before, text, caret.Offset, options) is not { } correction) return;
        using (History.BeginTransaction("autocorrect"))
        {
            _services.Editor.Delete(Document, History, DocumentRange.In(caret.ParagraphId, correction.Start, correction.Start + correction.Length));
            _services.Editor.InsertText(Document, History, new DocumentPosition(caret.ParagraphId, correction.Start), correction.Replacement);
        }
        _caret.Position = new DocumentPosition(caret.ParagraphId, correction.Caret);
        Collapse();
    }

    private bool Erase()
    {
        DeleteSelection();
        Collapse();
        return true;
    }

    public bool Shielded(DocumentRange range) =>
        !range.IsCollapsed && (FormRefuses(range) || _services.Guard.Protects(Document, _services.Order.Between(Document, range.Start, range.End), range) || _services.Controls.Guard.Protects(Document, range));

    private void DeleteSelection()
    {
        if (BlockRanges.Count > 0)
        {
            var blocks = BlockRanges;
            BlockRanges = [];
            foreach (var piece in blocks.Reverse().Where(candidate => !candidate.IsCollapsed && !Shielded(candidate))) _services.Editor.Delete(Document, History, piece);
            _anchor.Position = blocks[0].Start;
            _caret.Position = blocks[0].Start;
            return;
        }
        if (!HasSelection) return;
        var range = Selection;
        if (Shielded(range))
        {
            _anchor.Position = range.End;
            _caret.Position = range.End;
            return;
        }
        _services.Editor.Delete(Document, History, range);
        _anchor.Position = range.Start;
        _caret.Position = range.Start;
    }

    private void Collapse()
    {
        Normalize();
        _anchor.Position = _caret.Position;
        _preferredX = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private ParagraphBlock? Neighbour(string paragraphId, int direction)
    {
        var location = _services.Stories.Find(Document, paragraphId).Location;
        var index = location.Index + direction;
        return index >= 0 && index < location.Container.Count && location.Container[index] is ParagraphBlock paragraph ? paragraph : null;
    }

    private ParagraphBlock Paragraph(string id) => _services.Stories.Find(Document, id).Paragraph;

    private ParagraphBlock FirstBodyParagraph() =>
        Document.Body.Blocks.OfType<ParagraphBlock>().FirstOrDefault() ?? _services.Stories.Paragraphs(Document).First();

    private DocumentPosition Clamp(DocumentPosition position)
    {
        if (!_services.Order.Contains(Document, position.ParagraphId)) return new DocumentPosition(FirstBodyParagraph().Id, 0);
        var length = Paragraph(position.ParagraphId).Length;
        return position with { Offset = Math.Clamp(position.Offset, 0, length) };
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (SelectedObjectId is { } selected && Document.Objects.Find(selected) is null) SelectedObjectId = null;
        _dirty = true;
        _live = null;
        Normalize();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
