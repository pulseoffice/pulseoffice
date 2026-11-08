using System.Text.Json.Nodes;
using PulseOffice.Agent.Hosting;

namespace PulseOffice.Agent.Tools;

public sealed class ViewToolSource(IAgentWorkspace workspace, IAgentModuleNames modules) : IAgentToolSource
{
    public IEnumerable<AgentTool> Tools => [new RenderPageTool(workspace, modules)];
}

internal sealed class RenderPageTool(IAgentWorkspace workspace, IAgentModuleNames modules) : AgentTool
{
    private const int DefaultWidth = 1200;
    private const int MinimumWidth = 200;
    private const int MaximumWidth = 3000;

    public override string Name => "render_page";

    public override string Description =>
        "Returns a PNG picture, drawn by PulseOffice's own print engine, of a Folio page, a Deck slide or a Ledger printed page; " +
        "for Ledger 'range' (for example A1:F20) draws just that range of the active sheet.";

    public override AgentAccessLevel Access => AgentAccessLevel.Read;

    public override JsonObject InputSchema() => new SchemaBuilder()
        .String("document", "Id from list_documents; when omitted the active document (of 'module', if given).")
        .String("module", "folio, ledger or deck, to pick the active document of that module.", choices: modules.Names)
        .Integer("page", "1-based page or slide number (default 1).", minimum: 1)
        .String("range", "Ledger only: a cell range of the active sheet, such as A1:F20.")
        .Integer("width", "Picture width in pixels, 200 to 3000 (default 1200).", minimum: 200)
        .Build();

    public override async Task<AgentToolResult> InvokeAsync(AgentArguments arguments, CancellationToken ct)
    {
        var width = arguments.Integer("width", DefaultWidth);
        if (width is < MinimumWidth or > MaximumWidth)
            throw new AgentArgumentException($"The width must be between {MinimumWidth} and {MaximumWidth} pixels, not {width}.");
        var page = arguments.Integer("page", 1);
        if (page < 1) throw new AgentArgumentException("Pages and slides are numbered from 1.");
        var rendering = await workspace.RenderAsync(
            new AgentTarget(arguments.OptionalText("document"), modules.Parse(arguments.OptionalText("module"))),
            page, arguments.OptionalText("range"), width, ct).ConfigureAwait(true);

        return new AgentToolResult(
        [
            new AgentImage(rendering.Png, "image/png"),
            new AgentText(new JsonObject { ["page"] = rendering.Page, ["pageCount"] = rendering.PageCount }.ToJsonString()),
        ]);
    }
}
