using System.IO.Compression;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Formats.Packaging.Encryption.OpenPgp;
using PulseOffice.UI.Controls.Prompts;

namespace PulseOffice.Shell;

public sealed record UnlockedDocument(MemoryStream Package, OdfProtection? Protection);

public enum UnlockOutcome { Unlocked, Cancelled, Unsupported }

public sealed record ProtectionChoice(OdfProtection? Protection);

public interface IDocumentPasswordGate
{
    Task<(UnlockOutcome Outcome, UnlockedDocument? Document)> UnlockAsync(MemoryStream buffer, string documentName, CancellationToken ct);

    Task<ProtectionChoice?> ChooseProtectionAsync(string documentName, CancellationToken ct);

    bool CanProtect(MemoryStream package);

    bool IsProtected(string path);

    MemoryStream Protect(MemoryStream package, OdfProtection protection);

    Task<bool> CommitAsync(MemoryStream package, OdfProtection? protection, Func<MemoryStream, Task> commit);
}

public sealed class DocumentPasswordGate(
    IOdfPackageEncryption encryption,
    IDocumentPasswordPrompt prompt,
    IOpenPgp openPgp,
    IOpenPgpProtectionFactory openPgpProtections) : IDocumentPasswordGate
{
    private const long MaxKeyFileBytes = 16L * 1024 * 1024;

    public async Task<(UnlockOutcome Outcome, UnlockedDocument? Document)> UnlockAsync(MemoryStream buffer, string documentName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (!encryption.IsEncrypted(buffer)) return (UnlockOutcome.Unlocked, new UnlockedDocument(buffer, null));
        return encryption.RequiresOpenPgp(buffer)
            ? await UnlockWithKeyAsync(buffer, documentName, ct).ConfigureAwait(true)
            : await UnlockWithPasswordAsync(buffer, documentName, ct).ConfigureAwait(true);
    }

    private async Task<(UnlockOutcome, UnlockedDocument?)> UnlockWithPasswordAsync(MemoryStream buffer, string documentName, CancellationToken ct)
    {
        var retry = false;
        while (true)
        {
            var password = await prompt.AskAsync(documentName, retry, ct).ConfigureAwait(true);
            if (password is null) return (UnlockOutcome.Cancelled, null);

            var result = await DecryptAsync(buffer, new OdfPassword(password), ct).ConfigureAwait(true);
            if (result.Status == OdfDecryptionStatus.Decrypted) return (UnlockOutcome.Unlocked, new UnlockedDocument(result.Package!, result.Protection));
            if (result.Status != OdfDecryptionStatus.WrongPassword) return (UnlockOutcome.Unsupported, null);
            retry = true;
        }
    }

    private async Task<(UnlockOutcome, UnlockedDocument?)> UnlockWithKeyAsync(MemoryStream buffer, string documentName, CancellationToken ct)
    {
        var problem = OpenPgpKeyProblem.None;
        while (true)
        {
            if (await prompt.AskSecretKeyAsync(documentName, problem, ct).ConfigureAwait(true) is not { } answer)
                return (UnlockOutcome.Cancelled, null);

            if (ReadKeyFile(answer.KeyFile) is not { } keyFile)
            {
                problem = OpenPgpKeyProblem.InvalidKeyFile;
                continue;
            }

            OdfDecryptionResult result;
            try
            {
                result = await DecryptAsync(buffer, new OpenPgpSecretKey(keyFile, answer.Passphrase), ct).ConfigureAwait(true);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(keyFile);
            }

            switch (result.Status)
            {
                case OdfDecryptionStatus.Decrypted:
                    return (UnlockOutcome.Unlocked, new UnlockedDocument(result.Package!, result.Protection));
                case OdfDecryptionStatus.WrongPassword:
                    problem = OpenPgpKeyProblem.WrongPassphrase;
                    break;
                case OdfDecryptionStatus.NoMatchingKey:
                    problem = OpenPgpKeyProblem.NoMatchingKey;
                    break;
                case OdfDecryptionStatus.InvalidKeyFile:
                    problem = OpenPgpKeyProblem.InvalidKeyFile;
                    break;
                default:
                    return (UnlockOutcome.Unsupported, null);
            }
        }
    }

    public async Task<ProtectionChoice?> ChooseProtectionAsync(string documentName, CancellationToken ct)
    {
        var problem = OpenPgpKeyProblem.None;
        while (true)
        {
            switch (await prompt.AskProtectionAsync(documentName, problem, ct).ConfigureAwait(true))
            {
                case null:
                    return null;
                case PasswordRequest password:
                    return new ProtectionChoice(password.Password is null ? null : new PasswordProtection(password.Password));
                case RecipientsRequest recipients:
                    var found = new List<OpenPgpRecipient>();
                    foreach (var path in recipients.KeyFiles)
                    {
                        if (ReadKeyFile(path) is not { } bytes) continue;
                        try
                        {
                            found.AddRange(openPgp.ReadRecipients(bytes));
                        }
                        catch (InvalidDataException)
                        {
                        }
                    }

                    if (found.Count > 0) return new ProtectionChoice(openPgpProtections.ForRecipients(found));
                    problem = OpenPgpKeyProblem.InvalidKeyFile;
                    break;
            }
        }
    }

    private Task<OdfDecryptionResult> DecryptAsync(MemoryStream buffer, OdfUnlockKey key, CancellationToken ct)
    {
        buffer.Position = 0;
        return Task.Run(() =>
        {
            try
            {
                return encryption.Decrypt(buffer, key);
            }
            catch (InvalidDataException)
            {
                return new OdfDecryptionResult(OdfDecryptionStatus.Unsupported, null);
            }
        }, ct);
    }

    private byte[]? ReadKeyFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxKeyFileBytes ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public bool CanProtect(MemoryStream package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var origin = package.Position;
        try
        {
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.GetEntry("mimetype") is not { } mimetype || archive.GetEntry("META-INF/manifest.xml") is null) return false;
            using var reader = new StreamReader(mimetype.Open());
            return reader.ReadToEnd().StartsWith("application/vnd.oasis.opendocument.", StringComparison.Ordinal);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            package.Position = origin;
        }
    }

    public bool IsProtected(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            using var file = File.OpenRead(path);
            return encryption.IsEncrypted(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    public async Task<bool> CommitAsync(MemoryStream package, OdfProtection? protection, Func<MemoryStream, Task> commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        package.Position = 0;
        if (protection is null || !CanProtect(package))
        {
            await commit(package).ConfigureAwait(true);
            return protection is null;
        }

        using var protectedPackage = Protect(package, protection);
        await commit(protectedPackage).ConfigureAwait(true);
        return true;
    }

    public MemoryStream Protect(MemoryStream package, OdfProtection protection)
    {
        package.Position = 0;
        return encryption.Encrypt(package, protection);
    }
}
