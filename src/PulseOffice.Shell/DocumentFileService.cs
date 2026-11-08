using PulseOffice.Foundation.Modules;

namespace PulseOffice.Shell;

public interface IDocumentFileService
{
    ModuleKind Module { get; }

    Task SaveAsync(OpenDocument document, CancellationToken ct = default);

    Task SaveAsAsync(OpenDocument document, CancellationToken ct = default);

    Task SaveInterchangeAsync(OpenDocument document, CancellationToken ct = default);

    Task<bool> SaveCopyAsync(OpenDocument document, CancellationToken ct = default);

    Task<bool> SaveToAsync(OpenDocument document, string path, CancellationToken ct = default);
}

public interface IDocumentFileServices
{
    IDocumentFileService? For(OpenDocument? document);
}

public sealed class DocumentFileServices(IEnumerable<IDocumentFileService> services) : IDocumentFileServices
{
    private readonly IReadOnlyList<IDocumentFileService> _services = [.. services];

    public IDocumentFileService? For(OpenDocument? document) =>
        document is null ? null : _services.FirstOrDefault(service => service.Module == document.Module);
}
