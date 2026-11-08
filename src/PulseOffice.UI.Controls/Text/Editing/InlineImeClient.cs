using Avalonia;
using Avalonia.Input.TextInput;

namespace PulseOffice.UI.Controls.Text.Editing;

public interface IInlineImeSurface
{
    Visual TextViewVisual { get; }

    Rect CaretRectangle { get; }

    void SetPreedit(string? text, int? cursor);
}

public sealed class InlineImeClient(IInlineImeSurface host) : TextInputMethodClient
{
    private TextInputMethodClient? _inner;

    public override Visual TextViewVisual => host.TextViewVisual;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => _inner?.SupportsSurroundingText ?? false;

    public override string SurroundingText => _inner?.SurroundingText ?? string.Empty;

    public override Rect CursorRectangle => host.CaretRectangle;

    public override Avalonia.Input.TextInput.TextSelection Selection
    {
        get => _inner?.Selection ?? default;
        set
        {
            if (_inner is { } inner) inner.Selection = value;
        }
    }

    public void Wrap(TextInputMethodClient inner)
    {
        if (ReferenceEquals(inner, _inner) || ReferenceEquals(inner, this)) return;
        if (_inner is { } previous)
        {
            previous.SelectionChanged -= OnSelectionChanged;
            previous.SurroundingTextChanged -= OnSurroundingTextChanged;
            previous.ResetRequested -= OnResetRequested;
            previous.InputPaneActivationRequested -= OnInputPaneActivationRequested;
        }
        _inner = inner;
        inner.SelectionChanged += OnSelectionChanged;
        inner.SurroundingTextChanged += OnSurroundingTextChanged;
        inner.ResetRequested += OnResetRequested;
        inner.InputPaneActivationRequested += OnInputPaneActivationRequested;
    }

    public void CaretMoved() => RaiseCursorRectangleChanged();

    public override void SetPreeditText(string? preeditText) => SetPreeditText(preeditText, null);

    public override void SetPreeditText(string? preeditText, int? cursorPos)
    {
        _inner?.SetPreeditText(preeditText, cursorPos);
        host.SetPreedit(preeditText, cursorPos);
    }

    public override void ExecuteContextMenuAction(ContextMenuAction action) => _inner?.ExecuteContextMenuAction(action);

    private void OnSelectionChanged(object? sender, EventArgs e) => RaiseSelectionChanged();

    private void OnSurroundingTextChanged(object? sender, EventArgs e) => RaiseSurroundingTextChanged();

    private void OnResetRequested(object? sender, EventArgs e) => RequestReset();

    private void OnInputPaneActivationRequested(object? sender, EventArgs e) => RaiseInputPaneActivationRequested();
}
