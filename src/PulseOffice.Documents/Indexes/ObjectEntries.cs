using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Indexes;

public interface IIndexObjectClassifier
{
    IndexObjectSources SourceOf(EmbeddedObject value);
}

public sealed class IndexObjectClassifier : IIndexObjectClassifier
{
    public IndexObjectSources SourceOf(EmbeddedObject value) => value switch
    {
        PackagedObject packaged when packaged.MediaType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) || packaged.MediaType.Contains("excel", StringComparison.OrdinalIgnoreCase) => IndexObjectSources.Spreadsheets,
        PackagedObject packaged when packaged.MediaType.Contains("graphics", StringComparison.OrdinalIgnoreCase) || packaged.MediaType.Contains("visio", StringComparison.OrdinalIgnoreCase) => IndexObjectSources.Drawings,
        PackagedObject packaged when packaged.MediaType.Contains("formula", StringComparison.OrdinalIgnoreCase) => IndexObjectSources.Formulas,
        PackagedObject packaged when packaged.MediaType.Contains("chart", StringComparison.OrdinalIgnoreCase) => IndexObjectSources.Charts,
        PackagedObject => IndexObjectSources.Other,
        _ => value.Kind switch
        {
            EmbeddedObjectKind.Chart => IndexObjectSources.Charts,
            EmbeddedObjectKind.Formula => IndexObjectSources.Formulas,
            EmbeddedObjectKind.Table or EmbeddedObjectKind.SheetRange => IndexObjectSources.Spreadsheets,
            _ => IndexObjectSources.None,
        },
    };
}

public sealed class ObjectEntrySource(IIndexScope scope, IIndexObjectClassifier classifier) : IIndexEntrySource
{
    public IReadOnlyCollection<IndexKind> Kinds { get; } = [IndexKind.Objects];

    public IReadOnlyList<IndexEntry> Collect(TextDocument document, IndexDefinition definition)
    {
        var entries = new List<IndexEntry>();
        foreach (var paragraph in scope.Paragraphs(document))
        {
            foreach (var item in document.Objects.AnchoredTo(paragraph.Id))
            {
                if (item.Content is not EmbeddedContent { Value: var value }) continue;
                var source = classifier.SourceOf(value);
                if (source == IndexObjectSources.None || !definition.ObjectSources.HasFlag(source)) continue;
                entries.Add(new IndexEntry(item.Name ?? value.Title ?? value.Name ?? item.Id, 1, [paragraph.Id]));
            }
        }
        return entries;
    }
}
