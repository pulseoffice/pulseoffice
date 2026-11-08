using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using PulseOffice.Platform;

namespace PulseOffice.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class CredentialManagerVault : ISecretVault
{
    private const int CredentialTypeGeneric = 1;
    private const int PersistLocalMachine = 2;

    public string DisplayName => "Windows Credential Manager";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task<string?> ReadAsync(string service, string account, CancellationToken ct = default)
    {
        if (!CredRead(Target(service, account), CredentialTypeGeneric, 0, out var handle))
            return Task.FromResult<string?>(null);

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(handle);
            if (credential.CredentialBlob == nint.Zero || credential.CredentialBlobSize == 0)
                return Task.FromResult<string?>(null);

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);

            var secret = Encoding.Unicode.GetString(bytes);
            Array.Clear(bytes);
            return Task.FromResult<string?>(secret);
        }
        finally
        {
            CredFree(handle);
        }
    }

    public Task<bool> WriteAsync(
        string service, string account, string secret, CancellationToken ct = default)
    {
        var bytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocHGlobal(bytes.Length);

        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);

            var credential = new Credential
            {
                Type = CredentialTypeGeneric,
                TargetName = Target(service, account),
                CredentialBlob = blob,
                CredentialBlobSize = bytes.Length,
                Persist = PersistLocalMachine,
                UserName = account,
            };

            return Task.FromResult(CredWrite(ref credential, 0));
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Array.Clear(bytes);
            Marshal.FreeHGlobal(blob);
        }
    }

    public Task<bool> RemoveAsync(string service, string account, CancellationToken ct = default) =>
        Task.FromResult(CredDelete(Target(service, account), CredentialTypeGeneric, 0));

    private static string Target(string service, string account) => service + ":" + account;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public int LastWrittenLow;
        public int LastWrittenHigh;
        public int CredentialBlobSize;
        public nint CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public nint Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out nint credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(nint buffer);
}
