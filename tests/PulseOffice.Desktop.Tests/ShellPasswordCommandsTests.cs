using PulseOffice.Desktop.Composition;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Formats.Packaging.Encryption.OpenPgp;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Prompts;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ShellPasswordCommandsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-password-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class AskForPassword : IDocumentPasswordPrompt
    {
        public Task<string?> AskAsync(string documentName, bool retry, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task<SecretKeyAnswer?> AskSecretKeyAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct) =>
            Task.FromResult<SecretKeyAnswer?>(null);

        public Task<ProtectionRequest?> AskProtectionAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct) =>
            Task.FromResult<ProtectionRequest?>(new PasswordRequest("segreto"));
    }

    private sealed class PlainSaver : IDocumentSaver
    {
        public bool CanSave(OpenDocument document) => true;

        public async Task<bool> SaveAsync(OpenDocument document, CancellationToken ct = default)
        {
            await File.WriteAllBytesAsync(document.Path!, [0x50, 0x4B, 0x03, 0x04], ct);
            document.MarkSaved(document.Path!, Path.GetFileName(document.Path!));
            return true;
        }
    }

    [Fact]
    public async Task La_password_su_un_formato_che_non_si_cifra_non_si_annuncia_e_non_resta_sul_documento()
    {
        var path = Path.Combine(_folder, "lettera.docx");
        var workspace = new DocumentWorkspace();
        var document = new OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Documents, "lettera", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        document.RestoreLocation(path);
        workspace.Open(document);
        var history = new NotificationHistory(TimeProvider.System);
        var status = new StatusBarService(TimeProvider.System, new NotificationCenter(TimeProvider.System), history);
        var openPgp = new BouncyCastleOpenPgp();
        var gate = new DocumentPasswordGate(new OdfPackageEncryption(), new AskForPassword(), openPgp, new OpenPgpProtectionFactory(openPgp));
        var commands = new ShellPasswordCommands(workspace, [new PlainSaver()], gate, status, new TextCatalog(UiLanguage.Italian));
        var command = commands.GetCommands().Single();

        await command.Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.Null(document.Protection);
        Assert.DoesNotContain(history.Entries, entry => entry.Notification.Severity == NotificationSeverity.Success);
    }
}
