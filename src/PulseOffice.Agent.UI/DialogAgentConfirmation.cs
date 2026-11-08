using PulseOffice.Agent.Hosting;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Agent.UI;

public sealed class DialogAgentConfirmation(IDialogService dialogs, ITextCatalog texts, IAgentUiThread ui, IAgentToolTitles titles) : IAgentConfirmation
{
    public Task<bool> ConfirmAsync(AgentConfirmationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ui.InvokeAsync(async () =>
        {
            var view = new ChoicePromptView(
                texts.Format($"agent.confirm.{request.Kind.ToString().ToLowerInvariant()}", request.Subject),
                texts.Format("agent.confirm.explanation", titles.Of(request.Tool, request.Title)),
                [
                    new PromptChoice(texts["agent.confirm.allow"], true, IsDefault: true),
                    new PromptChoice(texts["agent.confirm.deny"], false, IsCancel: true),
                ],
                texts);

            var answer = await dialogs.ShowAsync<object>(
                view,
                new DialogOptions { TitleKey = "agent.confirm.title", PreferredWidth = 500 },
                ct).ConfigureAwait(true);
            return answer is true;
        });
    }
}
