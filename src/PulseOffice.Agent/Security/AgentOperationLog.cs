using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PulseOffice.Platform;

namespace PulseOffice.Agent.Security;

public enum AgentOperationOutcome { Done, Failed, Refused, Declined }

public sealed record AgentOperation(DateTimeOffset When, string Client, string Tool, AgentOperationOutcome Outcome, string? Detail, string? Title = null);

public interface IAgentOperationLog
{
    void Record(AgentOperation operation);

    IReadOnlyList<AgentOperation> Recent { get; }

    event EventHandler? Changed;
}

public sealed class AgentOperationLog(string path, IPrivateFiles files, ILogger<AgentOperationLog> logger) : IAgentOperationLog
{
    public const long MaximumFileBytes = 4 * 1024 * 1024;
    private const int Kept = 50;
    private const int MaximumDetail = 4096;
    private readonly Lock _gate = new();
    private readonly LinkedList<AgentOperation> _recent = new();

    public event EventHandler? Changed;

    public IReadOnlyList<AgentOperation> Recent
    {
        get
        {
            lock (_gate) return [.. _recent];
        }
    }

    public void Record(AgentOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        logger.LogInformation("Agent {Client} called {Tool}: {Outcome}", operation.Client, operation.Tool, operation.Outcome);

        lock (_gate)
        {
            _recent.AddFirst(operation);
            while (_recent.Count > Kept) _recent.RemoveLast();
            Append(operation);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Append(AgentOperation operation)
    {
        try
        {
            files.CreateFolder(Path.GetDirectoryName(path)!);
            var line = new JsonObject
            {
                ["when"] = operation.When.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ["client"] = operation.Client,
                ["tool"] = operation.Tool,
                ["title"] = operation.Title,
                ["outcome"] = operation.Outcome.ToString(),
                ["detail"] = operation.Detail is { Length: > MaximumDetail } lengthy ? lengthy[..MaximumDetail] + "…" : operation.Detail,
            }.ToJsonString();
            Rotate();
            using (var stream = files.Open(path, FileMode.Append, FileShare.Read))
            using (var writer = new StreamWriter(stream))
                writer.WriteLine(line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The agent operation log could not be written");
        }
    }

    private void Rotate()
    {
        var current = new FileInfo(path);
        if (!current.Exists || current.Length < MaximumFileBytes) return;
        File.Move(path, path + ".1", overwrite: true);
    }
}
