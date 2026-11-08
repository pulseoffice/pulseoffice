using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckTextCorrection(DeckViewState state, PulseOffice.Text.Spelling.IAutoCorrect? autoCorrect, PulseOffice.Text.Spelling.IAutoCorrectSettings? autoCorrectSettings, IDeckSpelling? spelling)
{
    private readonly DeckViewState _state = state;
    private readonly Presentation _deck = state.Deck;
    private readonly PulseOffice.Text.Spelling.IAutoCorrect? _autoCorrect = autoCorrect;
    private readonly PulseOffice.Text.Spelling.IAutoCorrectSettings? _autoCorrectSettings = autoCorrectSettings;
    private readonly IDeckSpelling? _spelling = spelling;

    public string Corrected(string text) =>
        _autoCorrect is { } corrector && _autoCorrectSettings is { Enabled: true } settings
            ? corrector.Apply(text, settings.Options with { Language = TextLanguage() })
            : text;

    public string? TextLanguage() => _spelling?.LanguageOf(_deck).Canonical;
}
