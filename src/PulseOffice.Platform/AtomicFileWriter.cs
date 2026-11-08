namespace PulseOffice.Platform;

public interface IAtomicFileWriter
{
    Task ReplaceAsync(string path, Stream content, CancellationToken ct = default);

    Task WriteAsync(ChosenFile file, Stream content, CancellationToken ct = default);

    T Replace<T>(string path, Func<Stream, T> produce, Predicate<T> keep);

    Task<T> WriteAsync<T>(ChosenFile file, Func<Stream, T> produce, Predicate<T> keep, CancellationToken ct = default);

    Task<T> WriteThroughPathAsync<T>(ChosenFile file, Func<string, CancellationToken, Task<T>> produce, Predicate<T> keep, CancellationToken ct = default);
}

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    private const int MaximumTemporaryStem = 64;

    public async Task ReplaceAsync(string path, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        await CommitAsync(path, async (temporary, token) =>
        {
            await using (var target = OpenTemporary(temporary, path))
            {
                await content.CopyToAsync(target, token).ConfigureAwait(false);
                target.Flush(flushToDisk: true);
            }
            return true;
        }, kept => kept, ct).ConfigureAwait(false);
    }

    public async Task WriteAsync(ChosenFile file, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(content);

        if (IsLocal(file))
        {
            await ReplaceAsync(file.Path, content, ct).ConfigureAwait(false);
            return;
        }

        await using var stream = await file.OpenWrite(ct).ConfigureAwait(true);
        await content.CopyToAsync(stream, ct).ConfigureAwait(true);
    }

    public T Replace<T>(string path, Func<Stream, T> produce, Predicate<T> keep)
    {
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(keep);
        return CommitAsync(path, (temporary, _) =>
        {
            using var target = OpenTemporary(temporary, path);
            var result = produce(target);
            target.Flush(flushToDisk: true);
            return Task.FromResult(result);
        }, keep, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<T> WriteAsync<T>(ChosenFile file, Func<Stream, T> produce, Predicate<T> keep, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(keep);

        if (IsLocal(file)) return Replace(file.Path, produce, keep);

        using var buffer = new MemoryStream();
        var result = produce(buffer);
        if (!keep(result)) return result;
        buffer.Position = 0;
        await using var stream = await file.OpenWrite(ct).ConfigureAwait(true);
        await buffer.CopyToAsync(stream, ct).ConfigureAwait(true);
        return result;
    }

    public async Task<T> WriteThroughPathAsync<T>(ChosenFile file, Func<string, CancellationToken, Task<T>> produce, Predicate<T> keep, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(produce);
        ArgumentNullException.ThrowIfNull(keep);

        if (IsLocal(file)) return await CommitAsync(file.Path, produce, keep, ct).ConfigureAwait(true);

        var scratch = Path.Combine(Path.GetTempPath(), $"pulseoffice-{Guid.NewGuid():N}{Path.GetExtension(file.Name)}");
        try
        {
            var result = await produce(scratch, ct).ConfigureAwait(true);
            if (!keep(result) || !File.Exists(scratch)) return result;
            await using var source = File.OpenRead(scratch);
            await using var stream = await file.OpenWrite(ct).ConfigureAwait(true);
            await source.CopyToAsync(stream, ct).ConfigureAwait(true);
            return result;
        }
        finally
        {
            if (File.Exists(scratch)) File.Delete(scratch);
        }
    }

    private static bool IsLocal(ChosenFile file) =>
        Path.IsPathFullyQualified(file.Path) && Directory.Exists(Path.GetDirectoryName(file.Path));

    private static FileStream OpenTemporary(string temporary, string destination)
    {
        var stream = new FileStream(temporary, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        });
        if (!OperatingSystem.IsWindows() && File.Exists(destination))
            File.SetUnixFileMode(temporary, File.GetUnixFileMode(destination));
        return stream;
    }

    private static string TemporaryStem(string full)
    {
        var stem = Path.GetFileNameWithoutExtension(full);
        return stem.Length <= MaximumTemporaryStem ? stem : char.IsHighSurrogate(stem[MaximumTemporaryStem - 1]) ? stem[..(MaximumTemporaryStem - 1)] : stem[..MaximumTemporaryStem];
    }

    private static async Task<T> CommitAsync<T>(string path, Func<string, CancellationToken, Task<T>> produce, Predicate<T> keep, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var full = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(full) ?? ".",
            $".{TemporaryStem(full)}.{Guid.NewGuid():N}{Path.GetExtension(full)}");
        try
        {
            var result = await produce(temporary, ct).ConfigureAwait(false);
            if (keep(result) && File.Exists(temporary)) File.Move(temporary, full, overwrite: true);
            return result;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
