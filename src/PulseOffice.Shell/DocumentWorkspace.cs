using Avalonia.Controls;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public interface IPendingEditsOwner
{
    void CommitPendingEdits();
}

public sealed class OpenDocument
{
    private readonly DocumentFileState _file;
    private string _title;

    public OpenDocument(
        ModuleKind module, string title, Control view, PulseOffice.Document.Commands.UndoStack history)
        : this(module, title, view, history, new DocumentFileState(history))
    {
    }

    private OpenDocument(
        ModuleKind module, string title, Control view, PulseOffice.Document.Commands.UndoStack history, DocumentFileState file)
    {
        ArgumentNullException.ThrowIfNull(history);

        Module = module;
        View = view;
        History = history;
        _title = title;
        _file = file;

        history.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        history.ChangedOutsideHistory += (_, _) => MarkModified();
        file.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public OpenDocument CreateWindow(Control view, string title) =>
        new(Module, title, view, History, _file) { Model = Model };

    public ModuleKind Module { get; }
    public Control View { get; }

    public PulseOffice.Document.Commands.UndoStack History { get; }

    public object? Model { get; init; }

    public PulseOffice.Formats.Packaging.Encryption.OdfProtection? Protection
    {
        get => _file.Protection;
        set => _file.Protection = value;
    }

    public string Title
    {
        get => _title;
        private set
        {
            if (_title == value) return;
            _title = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? Path => _file.Path;

    public bool IsModified => _file.IsModified;

    public void CommitPendingEdits() => (View as IPendingEditsOwner)?.CommitPendingEdits();

    public bool IsReadOnly
    {
        get => _file.IsReadOnly;
        set => _file.IsReadOnly = value;
    }

    public void MarkModified() => _file.MarkModified();

    public string DisplayName => IsModified ? Title + " •" : Title;

    public event EventHandler? Changed;

    public void RestoreLocation(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _file.Path = path;
    }

    public DocumentSavePoint CapturePoint() => _file.Capture();

    public void MarkSaved(string path, string fileName) => MarkSaved(path, fileName, CapturePoint());

    public void MarkSaved(string path, string fileName, DocumentSavePoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        Title = System.IO.Path.GetFileNameWithoutExtension(fileName);
        _file.MarkSaved(path, point);
    }

    private sealed class DocumentFileState(PulseOffice.Document.Commands.UndoStack history)
    {
        private object? _savedPoint = history.StatePoint;
        private bool _forcedModified;
        private long _forcedMarks;
        private bool _readOnly;
        private string? _path;

        public PulseOffice.Formats.Packaging.Encryption.OdfProtection? Protection { get; set; }

        public bool IsModified => _forcedModified || !ReferenceEquals(history.StatePoint, _savedPoint);

        public string? Path
        {
            get => _path;
            set => Set(ref _path, value);
        }

        public bool IsReadOnly
        {
            get => _readOnly;
            set => Set(ref _readOnly, value);
        }

        public event EventHandler? Changed;

        public DocumentSavePoint Capture() => new(history.StatePoint, _forcedMarks);

        public void MarkModified()
        {
            _forcedMarks++;
            if (_forcedModified) return;

            _forcedModified = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void MarkSaved(string path, DocumentSavePoint point)
        {
            _forcedModified = _forcedMarks != point.ForcedMarks;
            _savedPoint = point.StatePoint;
            _path = path;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;

            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed record DocumentSavePoint(object? StatePoint, long ForcedMarks);

public interface IDocumentWorkspace
{
    IReadOnlyList<OpenDocument> Documents { get; }
    OpenDocument? Active { get; }

    void Open(OpenDocument document);
    void Close(OpenDocument document);
    void Activate(OpenDocument document);

    IDisposable Hold(OpenDocument document);

    CommandContext Context { get; }

    void UpdateContext(Func<CommandContext, CommandContext> update);

    event EventHandler? ContextChanged;
    event EventHandler<OpenDocument>? ActiveChanged;

    event EventHandler<OpenDocument>? ActiveUpdated;
    event EventHandler<OpenDocument>? DocumentOpened;
    event EventHandler<OpenDocument>? DocumentClosed;
}

public sealed class DocumentWorkspace : IDocumentWorkspace
{
    private readonly List<OpenDocument> _documents = [];
    private readonly Dictionary<OpenDocument, EventHandler> _listeners = new(ReferenceEqualityComparer.Instance);
    private readonly List<OpenDocument> _deferredClosures = [];
    private OpenDocument? _held;
    private int _holds;
    private OpenDocument? _deferredActivation;

    public IReadOnlyList<OpenDocument> Documents => _documents;

    public OpenDocument? Active { get; private set; }

    public CommandContext Context { get; private set; } = CommandContext.Empty;

    public event EventHandler? ContextChanged;
    public event EventHandler<OpenDocument>? ActiveChanged;
    public event EventHandler<OpenDocument>? ActiveUpdated;
    public event EventHandler<OpenDocument>? DocumentOpened;
    public event EventHandler<OpenDocument>? DocumentClosed;

    public void Open(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_documents.Contains(document))
        {
            Activate(document);
            return;
        }

        _documents.Add(document);

        EventHandler listener = (_, _) =>
        {
            if (!ReferenceEquals(Active, document)) return;

            PublishFacts(document, keepView: true);
            ActiveUpdated?.Invoke(this, document);
        };

        _listeners[document] = listener;
        document.Changed += listener;

        DocumentOpened?.Invoke(this, document);
        Activate(document);
    }

    public void Close(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_holds > 0 && ReferenceEquals(_held, document))
        {
            if (_documents.Contains(document) && !_deferredClosures.Contains(document)) _deferredClosures.Add(document);
            return;
        }

        if (!_documents.Remove(document)) return;
        if (ReferenceEquals(_deferredActivation, document)) _deferredActivation = null;

        if (_listeners.Remove(document, out var listener)) document.Changed -= listener;

        if (ReferenceEquals(Active, document))
        {
            if (_documents.Count > 0) Activate(_documents[^1]);
            else
            {
                Active = null;
                SetContext(CommandContext.Empty);
            }
        }

        DocumentClosed?.Invoke(this, document);
    }

    private const DocumentState DocumentFacts =
        DocumentState.DocumentOpen | DocumentState.Modified
        | DocumentState.CanUndo | DocumentState.CanRedo | DocumentState.ReadOnly;

    public void Activate(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.Contains(document)) return;
        if (_holds > 0 && !ReferenceEquals(_held, document))
        {
            _deferredActivation = document;
            return;
        }

        var keepView = ReferenceEquals(Active, document);
        Active = document;

        PublishFacts(document, keepView);

        ActiveChanged?.Invoke(this, document);
    }

    public IDisposable Hold(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_documents.Contains(document)) throw new InvalidOperationException("Only an open document can be held.");
        if (_holds > 0 && !ReferenceEquals(_held, document)) throw new InvalidOperationException("Another document is already held.");

        if (!ReferenceEquals(Active, document)) Activate(document);
        _held = document;
        _holds++;
        return new Release(this);
    }

    private void EndHold()
    {
        if (_holds == 0 || --_holds > 0) return;

        _held = null;
        var closures = _deferredClosures.ToList();
        _deferredClosures.Clear();
        var activation = _deferredActivation;
        _deferredActivation = null;

        foreach (var document in closures) Close(document);
        if (activation is not null) Activate(activation);
    }

    private sealed class Release(DocumentWorkspace owner) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            owner.EndHold();
        }
    }

    private void PublishFacts(OpenDocument document, bool keepView)
    {
        var sameModule = keepView && Context.Module == document.Module;

        var facts = DocumentState.DocumentOpen
            | (document.IsModified ? DocumentState.Modified : DocumentState.None)
            | (document.History.CanUndo ? DocumentState.CanUndo : DocumentState.None)
            | (document.History.CanRedo ? DocumentState.CanRedo : DocumentState.None)
            | (document.IsReadOnly ? DocumentState.ReadOnly : DocumentState.None);

        SetContext(new CommandContext(
            document.Module,
            sameModule ? Context.Selection : SelectionKind.None,
            facts | (sameModule ? Context.State & ~DocumentFacts : DocumentState.None)));
    }

    public void UpdateContext(Func<CommandContext, CommandContext> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        SetContext(update(Context));
    }

    private void SetContext(CommandContext context)
    {
        if (context == Context) return;

        Context = context;
        ContextChanged?.Invoke(this, EventArgs.Empty);
    }
}
