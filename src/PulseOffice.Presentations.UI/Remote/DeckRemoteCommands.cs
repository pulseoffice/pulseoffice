using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations.Remote;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.Remote;

public sealed class DeckRemoteCommands(
    IActiveDeckContext context,
    IRemoteServer server,
    IRemoteSession session,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.slideshow.remote") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => ShowAsync(ct),
        },
    ];

    private async Task ShowAsync(CancellationToken ct)
    {
        RemoteEndpoint endpoint;
        try
        {
            endpoint = server.Endpoint ?? server.Start();
        }
        catch (System.Net.Sockets.SocketException)
        {
            status.Report(texts["remote.failed"], NotificationSeverity.Error);
            return;
        }

        var keep = await dialogs.ShowAsync<bool?>(
            new RemoteView(endpoint, new QrEncoder().Encode(endpoint.Url), session.Target is not null, texts),
            new DialogOptions { TitleKey = "command.deck.slideshow.remote.title", PreferredWidth = 620 }, ct).ConfigureAwait(true);

        if (keep == false)
        {
            server.Stop();
            status.Report(texts["remote.off"]);
        }
        else
        {
            status.Report(texts.Format("remote.on", endpoint.Url));
        }
    }
}
