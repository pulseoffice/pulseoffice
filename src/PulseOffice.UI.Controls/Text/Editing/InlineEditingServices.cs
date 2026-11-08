using PulseOffice.Text.Input;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Editing;

public sealed record InlineEditingServices(
    ICaretRowNavigation Navigation,
    IPreeditComposer Preedit,
    ITypingAutoCorrection? Typing);
