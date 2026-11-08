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

internal sealed class DeckProofing(DeckViewState state, IDeckSpelling? spelling, PulseOffice.Text.Spelling.IAutoSpellSettings? autoSpell, PulseOffice.Text.Spelling.IUserDictionary? userWords)
{
    private readonly DeckViewState _state = state;
    private readonly Presentation _deck = state.Deck;
    private readonly SlideView _canvas = state.Canvas;
    private readonly IDeckSpelling? _spelling = spelling;
    private readonly PulseOffice.Text.Spelling.IAutoSpellSettings? _autoSpell = autoSpell;
    private readonly PulseOffice.Text.Spelling.IUserDictionary? _userWords = userWords;

    private readonly Dictionary<string, IReadOnlyList<PulseOffice.Text.Spelling.WordSpan>> _proofed = [];

    public void StartProofing(PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges? DictionaryChanges)
    {
        if (_autoSpell is not null) _autoSpell.Changed += OnProofingChanged;
        if (_userWords is not null) _userWords.Changed += OnProofingChanged;
        if (DictionaryChanges is not null) DictionaryChanges.Changed += OnProofingChanged;
        ApplyProofing();
    }

    private void OnProofingChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(ApplyProofing);

    private void ApplyProofing()
    {
        _proofed.Clear();
        _canvas.Proofing = _spelling is { } spelling && _autoSpell is { Enabled: true }
            ? text =>
            {
                var language = spelling.LanguageOf(_deck);
                var key = language.Canonical + "\u0001" + text;
                if (_proofed.TryGetValue(key, out var found)) return found;
                var words = spelling.Misspelled(text, language);
                if (_proofed.Count > 512) _proofed.Clear();
                _proofed[key] = words;
                return words;
            }
            : null;
    }

    public void StopProofing(PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges? DictionaryChanges)
    {
        if (_autoSpell is not null) _autoSpell.Changed -= OnProofingChanged;
        if (_userWords is not null) _userWords.Changed -= OnProofingChanged;
        if (DictionaryChanges is not null) DictionaryChanges.Changed -= OnProofingChanged;
    }
}
