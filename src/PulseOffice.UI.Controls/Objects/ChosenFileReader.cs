using PulseOffice.Platform;

namespace PulseOffice.UI.Controls.Objects;

public enum ChosenFileReadStatus
{
    Read,
    TooLarge,
    Unreadable,
}

public sealed record ChosenFileContent(ChosenFileReadStatus Status, byte[] Bytes)
{
    public bool Succeeded => Status == ChosenFileReadStatus.Read;
}

public interface IChosenFileReader
{
    long ImageLimit { get; }

    long VectorLimit { get; }

    long DocumentLimit { get; }

    Task<ChosenFileContent> ReadAsync(ChosenFile file, long maximumBytes, CancellationToken ct);
}

public sealed class ChosenFileReader : IChosenFileReader
{
    public long ImageLimit => 64L * 1024 * 1024;

    public long VectorLimit => 16L * 1024 * 1024;

    public long DocumentLimit => 512L * 1024 * 1024;

    public async Task<ChosenFileContent> ReadAsync(ChosenFile file, long maximumBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            await using var stream = await file.OpenRead(ct).ConfigureAwait(true);
            if (stream.CanSeek && stream.Length > maximumBytes) return new ChosenFileContent(ChosenFileReadStatus.TooLarge, []);

            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(true)) > 0)
            {
                if (buffer.Length + read > maximumBytes) return new ChosenFileContent(ChosenFileReadStatus.TooLarge, []);
                buffer.Write(chunk, 0, read);
            }

            return new ChosenFileContent(ChosenFileReadStatus.Read, buffer.ToArray());
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return new ChosenFileContent(ChosenFileReadStatus.Unreadable, []);
        }
    }
}
