using System.Diagnostics;
using System.Text;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform;

public sealed record MailDraft
{
    public IReadOnlyList<string> To { get; init; } = [];

    public IReadOnlyList<string> Cc { get; init; } = [];

    public string Subject { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public IReadOnlyList<string> Attachments { get; init; } = [];
}

public interface IMailComposer
{
    string DisplayName { get; }

    bool IsAvailable { get; }

    bool SupportsAttachments { get; }

    Task<OperationResult<bool>> ComposeAsync(MailDraft draft, CancellationToken ct = default);
}

public interface IMailComposerProvider
{
    IMailComposer Current { get; }
}

public interface IFileSharing
{
    string DisplayName { get; }

    bool IsAvailable { get; }

    Task<OperationResult<bool>> SendAsync(IReadOnlyList<string> files, CancellationToken ct = default);
}

public interface IFileSharingProvider
{
    IFileSharing Current { get; }
}

public interface IExecutableLocator
{
    bool Exists(string executable);
}

public interface IExecutableResolver
{
    string? Resolve(string executable);
}

public sealed class PathExecutableLocator(Func<string?> searchPath) : IExecutableLocator, IExecutableResolver
{
    public PathExecutableLocator()
        : this(() => Environment.GetEnvironmentVariable("PATH"))
    {
    }

    public bool Exists(string executable) => Resolve(executable) is not null;

    public string? Resolve(string executable)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        if (Path.IsPathRooted(executable)) return File.Exists(executable) ? executable : null;

        var paths = searchPath();
        if (string.IsNullOrEmpty(paths)) return null;

        return paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Path.IsPathFullyQualified)
            .Select(directory => Path.Combine(directory, executable))
            .FirstOrDefault(File.Exists);
    }
}

public abstract class ProcessMailComposer(IExecutableLocator executables, IExternalProcess processes) : IMailComposer
{
    public abstract string DisplayName { get; }

    public virtual bool SupportsAttachments => true;

    protected abstract string Executable { get; }

    public virtual bool IsAvailable => executables.Exists(Executable);

    public abstract Task<OperationResult<bool>> ComposeAsync(
        MailDraft draft, CancellationToken ct = default);

    protected async Task<OperationResult<bool>> RunAsync(
        string executable, IReadOnlyList<string> arguments, string? input, CancellationToken ct)
    {
        try
        {
            var outcome = await processes.RunAsync(executable, arguments, input: input, ct: ct).ConfigureAwait(false);
            if (outcome is null) return OperationResult<bool>.Fail(Diagnostic.Error("MAIL002", executable).With(executable));

            return outcome.Succeeded
                ? OperationResult<bool>.Ok(true)
                : OperationResult<bool>.Fail(Diagnostic.Error("MAIL001", outcome.Error.Trim()).With(outcome.Error.Trim()));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("MAIL002", ex.Message).With(ex.Message));
        }
    }
}

public sealed class MailAddressFilter
{
    public bool IsPlain(string address) =>
        !string.IsNullOrWhiteSpace(address)
        && !address.StartsWith('-')
        && address.Contains('@', StringComparison.Ordinal)
        && !address.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character is ':' or '?' or '&' or '#' or '%' or ',' or ';' or '<' or '>' or '"' or '\\');
}

public sealed class MailtoLink
{
    private static string Address(string address) =>
        address.Replace("%", "%25", StringComparison.Ordinal).Replace("?", "%3F", StringComparison.Ordinal)
            .Replace("&", "%26", StringComparison.Ordinal).Replace("#", "%23", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);

    public string Build(MailDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var link = new StringBuilder("mailto:").Append(string.Join(',', draft.To.Select(Address)));
        var parameters = new List<string>();

        if (draft.Cc.Count > 0) parameters.Add("cc=" + Uri.EscapeDataString(string.Join(',', draft.Cc)));
        if (draft.Subject.Length > 0) parameters.Add("subject=" + Uri.EscapeDataString(draft.Subject));
        if (draft.Body.Length > 0) parameters.Add("body=" + Uri.EscapeDataString(draft.Body));

        if (parameters.Count > 0) link.Append('?').Append(string.Join('&', parameters));

        return link.ToString();
    }
}

public sealed class UnavailableMailComposer : IMailComposer
{
    public string DisplayName => "—";

    public bool IsAvailable => false;

    public bool SupportsAttachments => false;

    public Task<OperationResult<bool>> ComposeAsync(MailDraft draft, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<bool>.Fail(
            Diagnostic.Error("MAIL003", "No mail client is reachable on this system.")));
}

public sealed class UnavailableFileSharing : IFileSharing
{
    public string DisplayName => "—";

    public bool IsAvailable => false;

    public Task<OperationResult<bool>> SendAsync(
        IReadOnlyList<string> files, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<bool>.Fail(
            Diagnostic.Error("SHARE003", "No Bluetooth transfer tool is reachable on this system.")));
}
