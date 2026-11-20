using System.Globalization;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Shell;

public interface IDocumentViewSource
{
    ModuleKind Module { get; }

    OpenDocument? CreateSecondView(OpenDocument document, string title);
}

public interface IDocumentWindows
{
    bool CanOpenAnother(OpenDocument document);

    OpenDocument? OpenAnother(OpenDocument document);

    bool HasAnotherWindow(OpenDocument document);

    bool CloseWindow(OpenDocument document);

    string NextTitle(OpenDocument document);
}

public sealed class DocumentWindows(IDocumentWorkspace workspace, IEnumerable<IDocumentViewSource> sources) : IDocumentWindows
{
    private readonly IReadOnlyList<IDocumentViewSource> _sources = [.. sources];

    private IDocumentViewSource? SourceFor(OpenDocument document) =>
        _sources.FirstOrDefault(source => source.Module == document.Module);

    public bool CanOpenAnother(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return SourceFor(document) is not null && workspace.Documents.Contains(document);
    }

    public OpenDocument? OpenAnother(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (SourceFor(document)?.CreateSecondView(document, NextTitle(document)) is not { } second) return null;
        workspace.Open(second);
        return second;
    }

    public bool HasAnotherWindow(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return workspace.Documents.Any(other => !ReferenceEquals(other, document) && ReferenceEquals(other.History, document.History));
    }

    public bool CloseWindow(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!HasAnotherWindow(document)) return false;
        workspace.Close(document);
        return true;
    }

    public string NextTitle(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var windows = workspace.Documents.Where(other => ReferenceEquals(other.History, document.History)).ToList();
        var first = windows.Count > 0 ? windows[0].Title : document.Title;
        var prefix = first + " : ";
        var highest = 1;
        foreach (var window in windows)
            if (window.Title.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(window.Title.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                highest = Math.Max(highest, number);
        return prefix + (highest + 1).ToString(CultureInfo.InvariantCulture);
    }
}
