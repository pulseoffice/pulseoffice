using PulseOffice.Foundation.Zoom;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Zoom;

namespace PulseOffice.Presentations.UI;

public enum DeckViewMode { Normal, Sorter, Outline, Notes, Master, NotesMaster, HandoutMaster }

public sealed record DeckTarget(
    Presentation Deck,
    DeckActions Actions,
    Func<Slide?> CurrentSlide,
    Func<int> CurrentIndex,
    Action Play,
    Action<Func<RichText, (int Start, int End)?, RichText>> ApplyText,
    Func<bool> HasTextTarget,
    Func<SlideElement?> Selected,
    Func<IReadOnlyList<SlideElement>> SelectedMany,
    Action<IReadOnlyList<SlideElement>> Select,
    Action<int> GoToSlide,
    Func<string?> SelectedText,
    Func<System.Threading.Tasks.Task> InsertFormula,
    Action<DeckViewMode> SetViewMode,
    Func<DeckViewMode> ViewMode,
    Func<int?> LastEditedIndex,
    Func<ViewZoom> Zoom,
    Action<ViewZoom> SetZoom,
    Func<double> EffectiveZoom,
    Func<bool> IsTyping,
    Func<string, bool> TypeAtCaret,
    DeckViewOptions Options,
    Action<ViewZoom> RestoreZoom,
    Func<byte[], string, bool> InsertImageBytes,
    Func<byte[], string, string, bool> InsertLinkedImage,
    Func<TextSelection?> CommitSelection,
    Func<TextElement?> TextTarget,
    Func<(int Start, int End)?> TextRange)
{
    public Func<int?> TextCaret { get; init; } = () => null;

    public Func<string?> MasterLayoutId { get; init; } = () => null;

    public Action<string?> SelectMasterLayout { get; init; } = _ => { };

    public ZoomSubject ZoomSubject => new(Zoom, EffectiveZoom, SetZoom, ZoomLimits.Slides)
    {
        PageAspect = Deck.Size.Width.Value > 0 ? (double)Deck.Size.Height.Value / Deck.Size.Width.Value : 0.5625,
    };

    public Action<string> EditText { get; init; } = _ => { };

    public Func<IReadOnlyList<TextParagraph>, bool> InsertParagraphsAtCaret { get; init; } = _ => false;

    public Func<RichText?> ShapeText { get; init; } = () => null;

    public Func<(int Start, int End)?> ShapeRange { get; init; } = () => null;

    public RichText? ReadText() => ShapeText() ?? TextTarget()?.Text;

    public (int Start, int End)? ReadRange() =>
        ShapeText() is not null ? ShapeRange() : TextRange() ?? (TextCaret() is { } caret ? (caret, caret) : null);
}

public sealed record TextSelection(string ElementId, int Start, int End);

public interface IActiveDeckContext
{
    DeckTarget? Target { get; }

    bool HasDeck { get; }

    void Attach(DeckTarget target);

    void Detach(DeckTarget target);

    void Reannounce(DeckTarget target);

    event EventHandler? Changed;
}

public sealed class ActiveDeckContext : IActiveDeckContext
{
    private DeckTarget? _target;

    public DeckTarget? Target => _target;

    public bool HasDeck => _target is not null;

    public event EventHandler? Changed;

    public void Attach(DeckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reannounce(DeckTarget target)
    {
        if (ReferenceEquals(_target, target)) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Detach(DeckTarget target)
    {
        if (!ReferenceEquals(_target, target)) return;
        _target = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
