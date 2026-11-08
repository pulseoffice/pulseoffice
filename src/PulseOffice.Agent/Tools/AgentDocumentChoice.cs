using PulseOffice.Agent.Hosting;

namespace PulseOffice.Agent.Tools;

public sealed class AgentDocumentChoice(IAgentWorkspace workspace)
{
    public async Task<string> IdAsync(AgentArguments arguments, CancellationToken ct)
    {
        if (arguments.OptionalText("document") is { Length: > 0 } id) return id;
        var open = await workspace.DocumentsAsync(ct).ConfigureAwait(true);
        return open.Count switch
        {
            1 => open[0].Id,
            0 => throw new AgentHostException("No document is open."),
            _ => throw new AgentHostException("Several documents are open: pass 'document' with an id from list_documents."),
        };
    }
}
