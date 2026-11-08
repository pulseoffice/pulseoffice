using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Protection;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;

namespace PulseOffice.Documents.UI.Editing;

public sealed class FolioStateWitness(IActiveFolioContext context, IBlockTree tree) : IDocumentStateWitness
{
    public IDocumentStateMark? Mark(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Module != ModuleKind.Documents || context.Target?.Editor.Document is not { } text) return null;
        return new ProtectionMark(this, text, Protections(text));
    }

    private ProtectionState Protections(TextDocument text) => new(
        text.Protection,
        text.Marks,
        text.Changes.ProtectionKey,
        [.. tree.Blocks(text.Body).Select(Locks)]);

    private static string Locks(Block block) => block switch
    {
        SectionBlock { Protected: true } section => $"s:{section.Id}:{section.ProtectionKey?.Length ?? -1}",
        TableBlock table when table.Rows.SelectMany(row => row.Cells).Count(cell => cell.Protected) is > 0 and var locked => $"t:{table.Id}:{locked}",
        _ => string.Empty,
    };

    private sealed record ProtectionState(DocumentProtection Document, MarkProtection Marks, byte[]? ChangesKey, IReadOnlyList<string> Blocks)
    {
        public bool Same(ProtectionState other) =>
            Equals(Document, other.Document) && Equals(Marks, other.Marks) && ReferenceEquals(ChangesKey, other.ChangesKey)
            && Blocks.Where(lockKey => lockKey.Length > 0).SequenceEqual(other.Blocks.Where(lockKey => lockKey.Length > 0), StringComparer.Ordinal);
    }

    private sealed class ProtectionMark(FolioStateWitness witness, TextDocument text, ProtectionState before) : IDocumentStateMark
    {
        public string? Broken() => witness.Protections(text).Same(before) ? null : "status.dialog.stale.protection";
    }
}
