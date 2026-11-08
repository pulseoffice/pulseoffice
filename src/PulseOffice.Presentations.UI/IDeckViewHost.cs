using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal interface IDeckViewHost
{
    Control View { get; }

    IRichTextInsertion? RichInsertion { get; }

    Motion.IDeckMotionEditors? MotionEditors { get; }

    IBrush Accent();

    void AttachContext();

    void Reannounce();

    void NotifyRibbon();

    void FocusDocument();

    void Select(Slide slide);

    void GoToSlide(int index);

    void SelectElements(IReadOnlyList<SlideElement> elements);

    void SetViewMode(DeckViewMode mode);

    void RefreshMode();

    TextElement? LiveEditing();

    TextOffsetMap CommitEditor();
}
