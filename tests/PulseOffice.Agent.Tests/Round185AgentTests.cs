using System.Text.Json.Nodes;
using PulseOffice.Agent.Tools;
using PulseOffice.Scripting;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class Round185AgentTests
{
    private readonly IReadOnlyList<ApiParameter> _parameters =
    [
        new("reference", "string"),
        new("backgroundHex", "string"),
        new("colorHex", "string"),
    ];

    [Fact]
    public void An_explicit_null_argument_is_passed_as_null_not_treated_as_missing()
    {
        var call = new ScriptCallSource().Call("rules", "AddCondition", _parameters,
            new AgentArguments(new JsonObject { ["reference"] = "A1", ["backgroundHex"] = null, ["colorHex"] = "#FF0000" }));

        Assert.Equal("rules.AddCondition(\"A1\", null, \"#FF0000\")", call);
    }

    [Fact]
    public void An_absent_argument_before_a_given_one_is_still_refused()
    {
        Assert.Throws<AgentArgumentException>(() => new ScriptCallSource().Call("rules", "AddCondition", _parameters,
            new AgentArguments(new JsonObject { ["reference"] = "A1", ["colorHex"] = "#FF0000" })));
    }
}
