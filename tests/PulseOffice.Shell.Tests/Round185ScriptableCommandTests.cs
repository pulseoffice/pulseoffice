using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round185ScriptableCommandTests
{
    [Fact]
    public void A_command_that_runs_the_document_queries_cannot_be_run_from_a_macro()
    {
        var pivotRefresh = new CommandDefinition
        {
            Id = new CommandId("ledger.data.pivotRefresh"),
            RunsDocumentQueries = true,
            Execute = (_, _) => Task.CompletedTask,
        };

        Assert.False(new ScriptableCommandPolicy().Allows(pivotRefresh));
    }
}
