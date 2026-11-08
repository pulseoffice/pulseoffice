using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform.Linux;

public sealed class XdgMailComposer(IExecutableLocator executables, IExternalProcess processes) : ProcessMailComposer(executables, processes)
{
    private readonly MailAddressFilter _addresses = new();

    public override string DisplayName => "xdg-email";

    protected override string Executable => "xdg-email";

    public override async Task<OperationResult<bool>> ComposeAsync(
        MailDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var arguments = new List<string>();

        if (draft.Subject.Length > 0) arguments.AddRange(["--subject", draft.Subject]);
        if (draft.Body.Length > 0) arguments.AddRange(["--body", draft.Body]);

        foreach (var address in draft.Cc.Where(_addresses.IsPlain)) arguments.AddRange(["--cc", address]);
        foreach (var attachment in draft.Attachments) arguments.AddRange(["--attach", Path.GetFullPath(attachment)]);

        arguments.AddRange(draft.To.Where(_addresses.IsPlain));

        return await RunAsync(Executable, arguments, null, ct).ConfigureAwait(false);
    }
}

public sealed class BluetoothSendToSharing(ISharingProcessRunner processes, IExecutableLocator executables) : IFileSharing
{
    public string DisplayName => "bluetooth-sendto";

    public bool IsAvailable => executables.Exists("bluetooth-sendto");

    public async Task<OperationResult<bool>> SendAsync(
        IReadOnlyList<string> files, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
            return OperationResult<bool>.Fail(Diagnostic.Error("SHARE004", "No file to send."));

        return await processes.RunAsync("bluetooth-sendto", [.. files.Select(Path.GetFullPath)], ct).ConfigureAwait(false);
    }
}
