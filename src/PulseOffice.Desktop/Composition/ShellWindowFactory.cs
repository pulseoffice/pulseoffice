using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public interface IDialogHostFactory
{
    DialogHost Create();
}

public sealed class DialogHostFactory(IIconCatalog icons, ITextCatalog texts, IServiceProvider provider) : IDialogHostFactory
{
    public DialogHost Create()
    {
        var host = new DialogHost();
        host.Use(icons, texts);
        host.AdmitResultsThrough(provider.GetService<IDialogResultAdmission>);
        return host;
    }
}

public sealed class ShellWindowFactory(
    IServiceProvider provider,
    IDialogHostFactory dialogs,
    ITextCatalog texts,
    IShellWindowWiring wiring) : IShellWindowFactory
{
    public IShellWindowHandle Create()
    {
        var workspace = new DocumentWorkspace();
        var window = ActivatorUtilities.CreateInstance<ShellWindow>(
            provider, workspace, dialogs.Create(), new SidePanelView(texts));
        wiring.Attach(window);
        window.UpdateForContext(workspace.Context);
        return window;
    }
}
