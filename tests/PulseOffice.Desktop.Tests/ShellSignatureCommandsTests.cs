using System.Security.Cryptography.X509Certificates;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Security;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ShellSignatureCommandsTests
{
    private sealed class Signatures : IDocumentSignatureService
    {
        public int Reads { get; private set; }

        public bool CanSign(string path) => true;

        public bool IsSigned(string path) => false;

        public IReadOnlyList<DocumentSignature> Read(string path)
        {
            Reads++;
            return [];
        }

        public void Sign(string path, X509Certificate2 certificate) { }

        public bool Remove(string path, string thumbprint) => false;
    }

    [Fact]
    public async Task Non_si_firma_un_documento_con_modifiche_non_salvate()
    {
        var workspace = new DocumentWorkspace();
        var document = new OpenDocument(ModuleKind.Documents, "lettera", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        document.RestoreLocation(Path.Combine(Path.GetTempPath(), "lettera.odt"));
        document.MarkModified();
        workspace.Open(document);
        var history = new NotificationHistory(TimeProvider.System);
        var status = new StatusBarService(TimeProvider.System, new NotificationCenter(TimeProvider.System), history);
        var signatures = new Signatures();
        var commands = new ShellSignatureCommands(workspace, null!, null!, signatures, null!, null!, null!, null!, null!, status, new TextCatalog(UiLanguage.Italian));

        await commands.GetCommands().Single(command => command.Id.Value == "shell.file.signatures").Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(0, signatures.Reads);
        Assert.Contains(history.Entries, entry => entry.Notification.Severity == NotificationSeverity.Warning);
    }
}
