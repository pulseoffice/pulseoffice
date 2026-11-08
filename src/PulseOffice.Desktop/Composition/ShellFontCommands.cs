using PulseOffice.Document.Commands;
using PulseOffice.Document.Fonts;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellFontCommands(
    IDocumentWorkspace workspace,
    IDialogService dialogs,
    FontPickerServices fonts,
    IStatusBarService status,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.embedFonts"),
            IconKey = "shell.file.embedFonts",
            MenuPath = "file/document",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Fonts() is not null,
            Execute = (_, _) =>
            {
                if (workspace.Active is not { } document || Fonts() is not { } target) return Task.CompletedTask;

                var before = target.Fonts;
                var after = before with { EmbedSystemFonts = !before.EmbedSystemFonts };
                target.Fonts = after;
                document.History.Push(new DelegateAction(
                    texts["command.shell.file.embedFonts.title"],
                    () => target.Fonts = target.Fonts with { EmbedSystemFonts = after.EmbedSystemFonts },
                    () => target.Fonts = target.Fonts with { EmbedSystemFonts = before.EmbedSystemFonts }));
                status.Report(texts[after.EmbedSystemFonts ? "status.fonts.embedOn" : "status.fonts.embedOff"]);
                return Task.CompletedTask;
            },
        },
        new CommandDefinition
        {
            Id = new CommandId("shell.tools.fonts"),
            IconKey = "shell.tools.fonts",
            MenuPath = "tools/settings",
            Surface = CommandSurface.Advanced,
            Execute = async (_, ct) =>
            {
                var chosen = await dialogs.ShowAsync<string>(new FontBrowserView(string.Empty, fonts), new DialogOptions
                {
                    TitleKey = "font.browser.title",
                    PreferredWidth = 1040,
                    PreferredHeight = 680,
                }, ct).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(chosen)) status.Report(texts.Format("status.fonts.ready", chosen));
            },
        },
    ];

    private IFontDocument? Fonts() => workspace.Active?.Model as IFontDocument;
}

public sealed class ActiveFontDocument(IDocumentWorkspace workspace, PulseOffice.Typography.Embedding.IUsedFonts used)
    : PulseOffice.Typography.Sources.IActiveFontDocument
{
    public IFontDocument? Current => workspace.Active?.Model as IFontDocument;

    public IReadOnlyCollection<string> UsedFamilies => used.Of(workspace.Active?.Model);
}

public sealed class DocumentFontRegistrar
{
    public DocumentFontRegistrar(IDocumentWorkspace workspace, IFontActivation activation)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(activation);
        workspace.DocumentOpened += (_, document) => activation.RegisterDocument(document.Model as IFontDocument);
        workspace.ActiveChanged += (_, document) => activation.RegisterDocument(document.Model as IFontDocument);
    }
}
