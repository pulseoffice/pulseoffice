using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Tools;

namespace PulseOffice.Agent.Security;

public interface IAgentToolInvoker
{
    bool IsAllowed(AgentTool tool);

    Task<AgentToolResult> InvokeAsync(AgentTool tool, JsonObject arguments, string client, CancellationToken ct);
}

public sealed class AgentToolInvoker(
    IAgentPreferences preferences,
    IAgentConfirmation confirmation,
    IAgentOperationLog log,
    TimeProvider time,
    ILogger<AgentToolInvoker> logger) : IAgentToolInvoker
{
    public bool IsAllowed(AgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return tool.Access <= preferences.Level;
    }

    public async Task<AgentToolResult> InvokeAsync(AgentTool tool, JsonObject arguments, string client, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (!IsAllowed(tool))
        {
            Record(client, tool, AgentOperationOutcome.Refused, $"requires {tool.Access}, allowed {preferences.Level}");
            return AgentToolResult.Failure(
                $"The tool '{tool.Name}' needs the '{tool.Access.ToString().ToLowerInvariant()}' permission, " +
                $"but PulseOffice allows only '{preferences.Level.ToString().ToLowerInvariant()}'. The user can raise it in Settings > Agent.");
        }

        var parsed = new AgentArguments(arguments);
        try
        {
            if (await tool.ConfirmationAsync(parsed, ct).ConfigureAwait(true) is { } need
                && !await confirmation.ConfirmAsync(new AgentConfirmationRequest(tool.Name, need.Kind, need.Subject, tool.Title), ct).ConfigureAwait(true))
            {
                Record(client, tool, AgentOperationOutcome.Declined, $"{need.Kind.ToString().ToLowerInvariant()} {Summary(arguments)}");
                return AgentToolResult.Failure($"The user declined: {need.Summary}");
            }

            var result = await tool.InvokeAsync(parsed, ct).ConfigureAwait(true);
            Record(client, tool, result.IsError ? AgentOperationOutcome.Failed : AgentOperationOutcome.Done, Summary(arguments));
            return result;
        }
        catch (AgentArgumentException ex)
        {
            Record(client, tool, AgentOperationOutcome.Failed, ex.Message);
            return AgentToolResult.Failure(ex.Message);
        }
        catch (AgentHostException ex)
        {
            Record(client, tool, AgentOperationOutcome.Failed, ex.Message);
            return AgentToolResult.Failure(ex.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogError(ex, "Agent tool {Tool} failed", tool.Name);
            Record(client, tool, AgentOperationOutcome.Failed, ex.GetType().Name);
            return AgentToolResult.Failure($"The tool '{tool.Name}' failed: {ex.Message}");
        }
    }

    private void Record(string client, AgentTool tool, AgentOperationOutcome outcome, string? detail) =>
        log.Record(new AgentOperation(time.GetLocalNow(), client, tool.Name, outcome, detail, tool.Title));

    private string Summary(JsonObject arguments) =>
        string.Join(", ", arguments.Select(argument => $"{argument.Key}: {Shape(argument.Value)}"));

    private string Shape(JsonNode? value) => value switch
    {
        null => "null",
        JsonArray array => $"array({array.Count})",
        JsonObject members => $"object({members.Count})",
        JsonValue text when text.GetValueKind() == System.Text.Json.JsonValueKind.String => $"text({text.GetValue<string>().Length})",
        _ => value.GetValueKind().ToString().ToLowerInvariant(),
    };
}
