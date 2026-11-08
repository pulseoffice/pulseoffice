using System.Text.Json;
using Microsoft.Extensions.Logging;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Settings;

namespace PulseOffice.Shell;

public sealed record RecoverySnapshot(
    string Id, ModuleKind Module, string Title, string? OriginalPath, DateTimeOffset SavedAt, bool ReadOnly = false)
{
    public string DataFileName => Id + ".dati";
}

public interface IRecoveryCapture
{
    Task WriteAsync(Stream destination, CancellationToken ct = default);
}

public interface IDocumentSnapshotProvider
{
    ModuleKind Module { get; }

    IRecoveryCapture Capture(OpenDocument document);

    Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default);
}

public interface IDocumentRecovery
{
    IReadOnlyList<RecoverySnapshot> Pending { get; }

    Task SnapshotAsync(OpenDocument document, CancellationToken ct = default);

    Task WhenIdleAsync();

    Task<OpenDocument?> RestoreAsync(RecoverySnapshot snapshot, CancellationToken ct = default);

    void Discard(string id);

    void DiscardFor(OpenDocument document);

    void DiscardAll();
}

public sealed class DocumentRecovery : IDocumentRecovery
{
    private readonly IReadOnlyList<IDocumentSnapshotProvider> _providers;
    private readonly ILogger<DocumentRecovery> _logger;
    private readonly string _directory;
    private readonly Dictionary<OpenDocument, string> _identifiers = [];
    private readonly IDocumentPasswordGate? _passwords;
    private readonly IPrivateFiles _privateFiles;
    private readonly Lock _queueGate = new();
    private Task _queueTail = Task.CompletedTask;

    public DocumentRecovery(
        IEnumerable<IDocumentSnapshotProvider> providers,
        ILogger<DocumentRecovery> logger,
        string directory,
        IDocumentPasswordGate? passwords = null,
        IPrivateFiles? privateFiles = null)
    {
        _passwords = passwords;
        _privateFiles = privateFiles ?? new PrivateFilesFactory().Create();
        _providers = [.. providers];
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _directory = directory;

        Pending = Read();
    }

    public IReadOnlyList<RecoverySnapshot> Pending { get; private set; }

    public Task SnapshotAsync(OpenDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        lock (_queueGate)
        {
            var previous = _queueTail;
            var next = SnapshotAfterAsync(previous, document, ct);
            _queueTail = next;
            return next;
        }
    }

    public Task WhenIdleAsync()
    {
        lock (_queueGate) return _queueTail.ContinueWith(_ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private async Task SnapshotAfterAsync(Task previous, OpenDocument document, CancellationToken ct)
    {
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        await WriteSnapshotAsync(document, ct).ConfigureAwait(true);
    }

    private async Task WriteSnapshotAsync(OpenDocument document, CancellationToken ct)
    {
        var provider = _providers.FirstOrDefault(p => p.Module == document.Module);
        if (provider is null) return;

        if (!_identifiers.TryGetValue(document, out var id))
            _identifiers[document] = id = Guid.NewGuid().ToString("N");

        var snapshot = new RecoverySnapshot(
            id, document.Module, document.Title, document.Path, DateTimeOffset.Now, document.IsReadOnly);

        try
        {
            var capture = provider.Capture(document);
            var protection = document.Protection;
            var outcome = await Task.Run(() => PersistAsync(snapshot, capture, protection, ct), ct).ConfigureAwait(true);

            if (outcome == PersistOutcome.Unprotectable)
            {
                _logger.LogWarning("Recovery snapshot skipped for {Title}: the document is password protected and the snapshot cannot be encrypted", snapshot.Title);
                Discard(id);
                return;
            }

            if (!_identifiers.TryGetValue(document, out var current) || current != id) Discard(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Recovery snapshot failed for {Title}", snapshot.Title);
        }
    }

    private enum PersistOutcome
    {
        Written,
        Unprotectable,
    }

    private async Task<PersistOutcome> PersistAsync(
        RecoverySnapshot snapshot, IRecoveryCapture capture, PulseOffice.Formats.Packaging.Encryption.OdfProtection? protection, CancellationToken ct)
    {
        var data = Path.Combine(_directory, snapshot.DataFileName);
        var temporary = data + ".tmp";

        try
        {
            PrivateDirectory();

            using var plain = new MemoryStream();
            await capture.WriteAsync(plain, ct).ConfigureAwait(false);
            plain.Position = 0;

            if (protection is not null)
            {
                if (_passwords is null || !_passwords.CanProtect(plain)) return PersistOutcome.Unprotectable;

                using var encrypted = _passwords.Protect(plain, protection);
                await WritePrivateAsync(temporary, encrypted.ToArray(), ct).ConfigureAwait(false);
            }
            else
            {
                await WritePrivateAsync(temporary, plain.GetBuffer().AsMemory(0, (int)plain.Length), ct).ConfigureAwait(false);
            }

            File.Move(temporary, data, overwrite: true);

            await WritePrivateAsync(
                Path.Combine(_directory, snapshot.Id + ".json"),
                JsonSerializer.SerializeToUtf8Bytes(snapshot),
                ct).ConfigureAwait(false);

            return PersistOutcome.Written;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public async Task<OpenDocument?> RestoreAsync(RecoverySnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var provider = _providers.FirstOrDefault(p => p.Module == snapshot.Module);
        if (provider is null) return null;

        var data = Path.Combine(_directory, snapshot.DataFileName);
        if (!File.Exists(data)) return null;

        try
        {
            var buffer = new MemoryStream(await File.ReadAllBytesAsync(data, ct).ConfigureAwait(true));
            PulseOffice.Formats.Packaging.Encryption.OdfProtection? protection = null;
            if (_passwords is not null)
            {
                var (outcome, unlocked) = await _passwords.UnlockAsync(buffer, snapshot.Title, ct).ConfigureAwait(true);
                if (outcome != UnlockOutcome.Unlocked || unlocked is null) return null;
                buffer = unlocked.Package;
                protection = unlocked.Protection;
            }

            await using var stream = buffer;
            var document = await provider.RestoreAsync(stream, snapshot.Title, ct).ConfigureAwait(true);
            if (document is not null) document.Protection = protection;

            if (document is not null && !string.IsNullOrEmpty(snapshot.OriginalPath))
                document.RestoreLocation(snapshot.OriginalPath);

            if (document is null) return null;

            document.IsReadOnly = snapshot.ReadOnly;
            document.MarkModified();
            _identifiers[document] = snapshot.Id;
            Pending = [.. Pending.Where(s => s.Id != snapshot.Id)];

            return document;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Unreadable recovery snapshot: {Id}", snapshot.Id);
            return null;
        }
    }

    public void Discard(string id)
    {
        if (!IsIdentifier(id)) return;

        foreach (var path in new[]
                 {
                     Path.Combine(_directory, id + ".json"),
                     Path.Combine(_directory, id + ".dati"),
                 })
            TryDelete(path);

        Pending = [.. Pending.Where(s => s.Id != id)];
    }

    public void DiscardFor(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!_identifiers.Remove(document, out var id)) return;
        Discard(id);
    }

    public void DiscardAll()
    {
        foreach (var snapshot in Pending.ToList()) Discard(snapshot.Id);

        foreach (var document in _identifiers.Values.ToList()) Discard(document);
        _identifiers.Clear();
    }

    private IReadOnlyList<RecoverySnapshot> Read()
    {
        string[] files;
        try
        {
            if (!Directory.Exists(_directory)) return [];
            files = Directory.GetFiles(_directory, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unreadable recovery directory: {Directory}", _directory);
            return [];
        }

        var found = new List<RecoverySnapshot>();

        foreach (var file in files)
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(File.ReadAllText(file));

                if (snapshot is not null && IsIdentifier(snapshot.Id) && snapshot.Title is not null
                    && File.Exists(Path.Combine(_directory, snapshot.DataFileName)))
                    found.Add(snapshot);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Unreadable recovery manifest: {File}", file);
            }
        }

        return [.. found.OrderByDescending(s => s.SavedAt)];
    }

    private bool IsIdentifier(string? id) =>
        id is { Length: 32 } && id.All(Uri.IsHexDigit);

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not delete {Path}", path);
        }
    }

    private void PrivateDirectory() => _privateFiles.CreateFolder(_directory);

    private async Task WritePrivateAsync(string path, ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        await using var stream = _privateFiles.Open(path, FileMode.Create);
        await stream.WriteAsync(content, ct).ConfigureAwait(false);
    }
}
