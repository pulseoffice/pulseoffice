using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PulseOffice.Documents.UI.Shell.Snapshot;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;

namespace PulseOffice.Documents.UI.Shell;

public interface IFolioSnapshotCodec
{
    Task WriteAsync(TextDocument document, Stream destination, CancellationToken ct = default);

    Task<TextDocument?> ReadAsync(Stream source, CancellationToken ct = default);
}

public sealed class FolioSnapshotCodec : IFolioSnapshotCodec
{
    public const string FormatName = "pulseoffice-folio-snapshot";
    public const int Version = 2;

    private readonly JsonSerializerOptions _options;
    private readonly IFolioSnapshotValidator _validator;
    private readonly ILogger<FolioSnapshotCodec> _logger;

    public FolioSnapshotCodec(ISnapshotTypeCatalog catalog, IFolioSnapshotValidator validator, ILogger<FolioSnapshotCodec> logger)
    {
        _validator = validator;
        _logger = logger;
        var contracts = new AbstractTypeContracts(catalog);
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(contracts.Apply);
        resolver.Modifiers.Add(new DefaultValueOmission().Apply);
        _options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = resolver,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            IncludeFields = false,
            MaxDepth = 512,
            Converters =
            {
                new JsonStringEnumConverter(),
                new TextDocumentConverter(),
                new DocumentPropertiesConverter(),
                new StyleSheetConverter(),
                new DocumentStyleConverter(),
                new HeaderFooterConverter(),
                new IndexLevelStylesConverter(),
                new BlockContainerConverter(),
                new BlockConverter(),
                new TableRowConverter(),
                new TableCellConverter(),
                new CommentConverter(),
                new NoteConverter(),
                new TrackedChangeConverter(),
                new AnchoredObjectConverter(),
                new AnchoredContentConverter(),
            },
        };
    }

    public async Task WriteAsync(TextDocument document, Stream destination, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        await using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { MaxDepth = _options.MaxDepth, Encoder = _options.Encoder });
        writer.WriteStartObject();
        writer.WriteString("format", FormatName);
        writer.WriteNumber("version", Version);
        writer.WritePropertyName("document");
        JsonSerializer.Serialize(writer, document, _options);
        writer.WriteEndObject();
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    public async Task<TextDocument?> ReadAsync(Stream source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            var node = await JsonNode.ParseAsync(source, documentOptions: new JsonDocumentOptions { MaxDepth = 512 }, cancellationToken: ct).ConfigureAwait(false);
            if (node is not JsonObject envelope) return null;
            if (!Matches(envelope["format"], FormatName) || !Matches(envelope["version"], Version)) return null;
            return envelope["document"]?.Deserialize<TextDocument>(_options) is { } document && _validator.IsSound(document) ? document : null;
        }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        {
            _logger.LogWarning(error, "Folio snapshot could not be read; it is ignored");
            return null;
        }
    }

    private bool Matches<T>(JsonNode? node, T expected) =>
        node is JsonValue value && value.TryGetValue<T>(out var actual) && EqualityComparer<T>.Default.Equals(actual, expected);
}

public sealed class FolioSnapshotProvider(IFolioSnapshotCodec codec, IFolioDocumentFactory views) : IDocumentSnapshotProvider
{
    public ModuleKind Module => ModuleKind.Documents;

    public IRecoveryCapture Capture(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new FolioRecoveryCapture(codec, document.Model is TextDocument text ? text.Clone() : null);
    }

    public async Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default)
    {
        var document = await codec.ReadAsync(source, ct).ConfigureAwait(true);
        return document is null ? null : views.CreateFrom(document, title);
    }
}

public sealed class FolioRecoveryCapture(IFolioSnapshotCodec codec, TextDocument? copy) : IRecoveryCapture
{
    public Task WriteAsync(Stream destination, CancellationToken ct = default) =>
        copy is null ? Task.CompletedTask : codec.WriteAsync(copy, destination, ct);
}
