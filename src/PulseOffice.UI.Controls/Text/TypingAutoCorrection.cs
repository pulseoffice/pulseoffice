using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Text.Spelling;

namespace PulseOffice.UI.Controls.Text;

public interface ITypingAutoCorrection
{
    IDisposable Attach(TextBox box, Func<string?> language);

    string Finish(string text, string? language);
}

public sealed class TypingAutoCorrection(IWordBoundaryCorrection boundary, IAutoCorrectSettings settings) : ITypingAutoCorrection
{
    public IDisposable Attach(TextBox box, Func<string?> language)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(language);
        return new Session(box, boundary, settings, language);
    }

    public string Finish(string text, string? language)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!settings.Enabled) return text;
        return boundary.AtEnd(text, settings.Options with { Language = language })?.Text ?? text;
    }

    private sealed class Session : IDisposable
    {
        private readonly TextBox _box;
        private readonly IWordBoundaryCorrection _boundary;
        private readonly IAutoCorrectSettings _settings;
        private readonly Func<string?> _language;
        private string _last;
        private bool _applying;
        private bool _disposed;

        public Session(TextBox box, IWordBoundaryCorrection boundary, IAutoCorrectSettings settings, Func<string?> language)
        {
            _language = language;
            _box = box;
            _boundary = boundary;
            _settings = settings;
            _last = box.Text ?? string.Empty;
            box.TextChanged += OnTextChanged;
        }

        public void Dispose()
        {
            _disposed = true;
            _box.TextChanged -= OnTextChanged;
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            var before = _last;
            var after = _box.Text ?? string.Empty;
            _last = after;
            if (_applying || !_settings.Enabled) return;

            var caret = _box.CaretIndex;
            if (_boundary.AfterTyping(before, after, caret, _settings.Options with { Language = _language() }) is not { } correction) return;
            Dispatcher.UIThread.Post(() => Apply(after, caret, correction), DispatcherPriority.Send);
        }

        private void Apply(string expected, int caret, BoundaryCorrection correction)
        {
            if (_disposed || !string.Equals(_box.Text, expected, StringComparison.Ordinal) || _box.CaretIndex != caret) return;

            _applying = true;
            try
            {
                _box.SelectionStart = correction.Start;
                _box.SelectionEnd = correction.Start + correction.Length;
                _box.SelectedText = correction.Replacement;
                _box.CaretIndex = correction.Caret;
                _box.SelectionStart = correction.Caret;
                _box.SelectionEnd = correction.Caret;
            }
            finally
            {
                _applying = false;
                _last = _box.Text ?? string.Empty;
            }
        }
    }
}
