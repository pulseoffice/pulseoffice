namespace PulseOffice.Text.Input;

public sealed record PreeditComposition(string Text, int Start, int Length, int Caret)
{
    public bool IsActive => Length > 0;
}

public interface IPreeditComposer
{
    PreeditComposition Compose(string text, int selectionStart, int selectionEnd, string? preedit, int? cursor);
}

public sealed class PreeditComposer : IPreeditComposer
{
    public PreeditComposition Compose(string text, int selectionStart, int selectionEnd, string? preedit, int? cursor)
    {
        ArgumentNullException.ThrowIfNull(text);
        var from = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var to = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        if (string.IsNullOrEmpty(preedit)) return new PreeditComposition(text, from, 0, Math.Clamp(selectionEnd, 0, text.Length));

        var display = string.Concat(text.AsSpan(0, from), preedit, text.AsSpan(to));
        var caret = from + Math.Clamp(cursor ?? preedit.Length, 0, preedit.Length);
        return new PreeditComposition(display, from, preedit.Length, caret);
    }
}
