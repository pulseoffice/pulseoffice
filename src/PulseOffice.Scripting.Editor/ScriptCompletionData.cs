using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace PulseOffice.Scripting.Editor;

public sealed class ScriptCompletionData(CompletionItem item) : ICompletionData
{
    public CompletionItem Item { get; } = item;

    public IImage? Image => null;

    public string Text => Item.InsertText ?? Item.Label;

    public object Content => Item.Label;

    public object Description => string.IsNullOrEmpty(Item.Documentation)
        ? Item.Detail ?? string.Empty
        : Item.Detail is { Length: > 0 } detail
            ? detail + "\n\n" + Item.Documentation
            : Item.Documentation;

    public double Priority => Item.IsApiSymbol ? 1 : 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        ArgumentNullException.ThrowIfNull(textArea);

        textArea.Document.Replace(completionSegment, Text);
    }
}
