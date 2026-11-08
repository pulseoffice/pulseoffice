using PulseOffice.Localization;

namespace PulseOffice.Agent.UI;

public interface IAgentToolTitles
{
    string Of(string tool, string? title);
}

public sealed class AgentToolTitles(ITextCatalog texts) : IAgentToolTitles
{
    public string Of(string tool, string? title)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var key = $"agent.tool.{tool}";
        if (texts.Contains(key)) return texts[key];
        return title is { Length: > 0 } && title != tool ? title : tool;
    }
}
