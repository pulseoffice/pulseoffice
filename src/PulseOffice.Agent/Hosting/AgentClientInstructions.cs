using System.Text.Json.Nodes;

namespace PulseOffice.Agent.Hosting;

public interface IAgentClientInstructions
{
    string ClaudeCode(Uri address, string code);

    string HttpConfiguration(Uri address, string code);
}

public sealed class AgentClientInstructions : IAgentClientInstructions
{
    public string ClaudeCode(Uri address, string code)
    {
        ArgumentNullException.ThrowIfNull(address);
        return $"claude mcp add --transport http pulseoffice {address} --header \"Authorization: Bearer {code}\"";
    }

    public string HttpConfiguration(Uri address, string code)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["pulseoffice"] = new JsonObject
                {
                    ["type"] = "http",
                    ["url"] = address.ToString(),
                    ["headers"] = new JsonObject { ["Authorization"] = $"Bearer {code}" },
                },
            },
        }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
