using System.Text;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform.MacOS;

public sealed class MailAppComposer(IExecutableLocator executables, IExternalProcess processes) : ProcessMailComposer(executables, processes)
{
    public override string DisplayName => "Mail";

    protected override string Executable => "/usr/bin/osascript";

    public override async Task<OperationResult<bool>> ComposeAsync(
        MailDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return await RunAsync(Executable, ["-e", Script(draft)], null, ct).ConfigureAwait(false);
    }

    private static string Script(MailDraft draft)
    {
        var script = new StringBuilder();

        script.Append("tell application \"Mail\"\n")
              .Append("set newMessage to make new outgoing message with properties {subject:")
              .Append(Quote(draft.Subject))
              .Append(", content:")
              .Append(Quote(draft.Body))
              .Append(", visible:true}\n")
              .Append("tell newMessage\n");

        foreach (var address in draft.To)
            script.Append("make new to recipient at end of to recipients with properties {address:")
                  .Append(Quote(address)).Append("}\n");

        foreach (var address in draft.Cc)
            script.Append("make new cc recipient at end of cc recipients with properties {address:")
                  .Append(Quote(address)).Append("}\n");

        foreach (var attachment in draft.Attachments)
            script.Append("make new attachment with properties {file name:(POSIX file ")
                  .Append(Quote(Path.GetFullPath(attachment))).Append(")} at after the last paragraph\n");

        return script.Append("end tell\nactivate\nend tell").ToString();
    }

    private static string Quote(string text) =>
        "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal)
                   .Replace("\"", "\\\"", StringComparison.Ordinal)
                   .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}

public sealed class BluetoothExchangeSharing(ISharingProcessRunner processes) : IFileSharing
{
    private static readonly string[] Candidates =
    [
        "/System/Applications/Utilities/Bluetooth File Exchange.app",
        "/System/Library/CoreServices/Applications/Bluetooth File Exchange.app",
        "/Applications/Utilities/Bluetooth File Exchange.app",
    ];

    public string DisplayName => "Bluetooth File Exchange";

    public bool IsAvailable => Installed is not null;

    private static string? Installed => Candidates.FirstOrDefault(Directory.Exists);

    public async Task<OperationResult<bool>> SendAsync(
        IReadOnlyList<string> files, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
            return OperationResult<bool>.Fail(Diagnostic.Error("SHARE004", "No file to send."));

        if (Installed is not { } application)
            return OperationResult<bool>.Fail(Diagnostic.Error("SHARE003", "No Bluetooth transfer tool is reachable on this system."));

        return await processes.RunAsync(
            "/usr/bin/open", ["-a", application, .. files.Select(Path.GetFullPath)], ct).ConfigureAwait(false);
    }
}
