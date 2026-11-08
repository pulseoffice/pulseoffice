using System.Diagnostics;
using System.Runtime.InteropServices;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform.Windows;

public sealed class MapiMailComposer : IMailComposer
{
    private const int MapiLogonUi = 0x00000001;
    private const int MapiDialog = 0x00000008;
    private const int MapiRecipTo = 1;
    private const int MapiRecipCc = 2;
    private const int MapiSuccess = 0;
    private const int MapiUserAbort = 1;

    public string DisplayName => "MAPI";

    public bool IsAvailable => OperatingSystem.IsWindows()
        && File.Exists(Path.Combine(Environment.SystemDirectory, "mapi32.dll"));

    public bool SupportsAttachments => true;

    public Task<OperationResult<bool>> ComposeAsync(MailDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var recipients = new List<MapiRecipDesc>();

        recipients.AddRange(draft.To.Select(address => Recipient(address, MapiRecipTo)));
        recipients.AddRange(draft.Cc.Select(address => Recipient(address, MapiRecipCc)));

        var files = draft.Attachments
            .Select(path => new MapiFileDesc
            {
                Position = -1,
                Path = path,
                Name = Path.GetFileName(path),
            })
            .ToList();

        var message = new MapiMessage
        {
            Subject = draft.Subject,
            NoteText = draft.Body,
            RecipientCount = recipients.Count,
            Recipients = Pack(recipients),
            FileCount = files.Count,
            Files = Pack(files),
        };

        try
        {
            var code = MapiSendMail(IntPtr.Zero, IntPtr.Zero, message, MapiLogonUi | MapiDialog, 0);

            return Task.FromResult(code is MapiSuccess or MapiUserAbort
                ? OperationResult<bool>.Ok(true)
                : OperationResult<bool>.Fail(Diagnostic.Error("MAIL001", $"MAPI error {code}").With($"MAPI error {code}")));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Task.FromResult(OperationResult<bool>.Fail(Diagnostic.Error("MAIL002", ex.Message).With(ex.Message)));
        }
        finally
        {
            Release<MapiRecipDesc>(message.Recipients, recipients.Count);
            Release<MapiFileDesc>(message.Files, files.Count);
        }
    }

    private static MapiRecipDesc Recipient(string address, int kind) => new()
    {
        RecipientClass = kind,
        Name = address,
        Address = "SMTP:" + address,
    };

    private static IntPtr Pack<T>(IReadOnlyList<T> items) where T : struct
    {
        if (items.Count == 0) return IntPtr.Zero;

        var size = Marshal.SizeOf<T>();
        var block = Marshal.AllocHGlobal(size * items.Count);

        for (var index = 0; index < items.Count; index++)
            Marshal.StructureToPtr(items[index], block + (index * size), false);

        return block;
    }

    private static void Release<T>(IntPtr block, int count) where T : struct
    {
        if (block == IntPtr.Zero) return;

        var size = Marshal.SizeOf<T>();
        for (var index = 0; index < count; index++)
            Marshal.DestroyStructure<T>(block + (index * size));

        Marshal.FreeHGlobal(block);
    }

    [DllImport("mapi32.dll", CharSet = CharSet.Ansi)]
    private static extern int MapiSendMail(
        IntPtr session, IntPtr window, MapiMessage message, int flags, int reserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private sealed class MapiMessage
    {
        public int Reserved;
        public string? Subject;
        public string? NoteText;
        public string? MessageType;
        public string? DateReceived;
        public string? ConversationId;
        public int Flags;
        public IntPtr Originator;
        public int RecipientCount;
        public IntPtr Recipients;
        public int FileCount;
        public IntPtr Files;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MapiRecipDesc
    {
        public int Reserved;
        public int RecipientClass;
        public string? Name;
        public string? Address;
        public int EntryIdSize;
        public IntPtr EntryId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MapiFileDesc
    {
        public int Reserved;
        public int Flags;
        public int Position;
        public string? Path;
        public string? Name;
        public IntPtr Type;
    }
}

public sealed class ShellMailComposer : IMailComposer
{
    private readonly MailtoLink _link = new();

    public string DisplayName => "mailto";

    public bool IsAvailable => true;

    public bool SupportsAttachments => false;

    public Task<OperationResult<bool>> ComposeAsync(MailDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        try
        {
            using var process = Process.Start(new ProcessStartInfo(_link.Build(draft))
            {
                UseShellExecute = true,
            });

            return Task.FromResult(OperationResult<bool>.Ok(true));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            return Task.FromResult(OperationResult<bool>.Fail(Diagnostic.Error("MAIL002", ex.Message).With(ex.Message)));
        }
    }
}

public sealed class FsquirtSharing(ISharingProcessRunner processes) : IFileSharing
{
    public string DisplayName => "fsquirt";

    public bool IsAvailable => OperatingSystem.IsWindows()
        && File.Exists(Path.Combine(Environment.SystemDirectory, "fsquirt.exe"));

    public async Task<OperationResult<bool>> SendAsync(
        IReadOnlyList<string> files, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
            return OperationResult<bool>.Fail(Diagnostic.Error("SHARE004", "No file to send."));

        return await processes.RunAsync(
            Path.Combine(Environment.SystemDirectory, "fsquirt.exe"), ["-send", Path.GetFullPath(files[0])], ct)
            .ConfigureAwait(false);
    }
}
