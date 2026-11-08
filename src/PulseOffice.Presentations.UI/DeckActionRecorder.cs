using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckActionRecorder(Presentation deck, UndoStack history, Func<bool>? readOnly, Rendering.ITextFrameFitting fitting)
{
    public event Action<Slide>? FocusRequested;

    public bool IsReadOnly => readOnly?.Invoke() == true;

    public void Do(string description, Action redo, Action undo)
    {
        if (IsReadOnly) return;

        redo();
        history.Push(new DelegateAction(description, redo, undo));
    }

    public T Fitted<T>(Slide slide, T element) where T : SlideElement => (T)fitting.Fit(deck, slide, element);

    public void Focus(Slide slide) => FocusRequested?.Invoke(slide);
}
