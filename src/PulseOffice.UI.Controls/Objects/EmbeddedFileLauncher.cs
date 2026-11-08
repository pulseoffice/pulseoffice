using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;

namespace PulseOffice.UI.Controls.Objects;

public interface IEmbeddedFileLauncher
{
    Task<OperationResult<bool>> OpenAsync(ReadOnlyMemory<byte> data, string fileName, CancellationToken ct = default);
}

public sealed class EmbeddedFileLauncher(IShellOpener opener, ITemporaryFiles temporary, IExecutableFileTypes executables) : IEmbeddedFileLauncher
{
    public async Task<OperationResult<bool>> OpenAsync(ReadOnlyMemory<byte> data, string fileName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fileName)))
            return OperationResult<bool>.Fail(Diagnostic.Error("OPEN005", "An embedded file without a name is not opened."));

        if (executables.CanRunCode(fileName))
            return OperationResult<bool>.Fail(Diagnostic.Error("OPEN004", "Embedded files that can run code are not opened.").With(Path.GetFileName(fileName)));

        var folder = temporary.NewFolder("object-");
        var file = Path.Combine(folder, Path.GetFileName(fileName));
        await File.WriteAllBytesAsync(file, data.ToArray(), ct).ConfigureAwait(true);
        return opener.OpenFile(file);
    }
}
