using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Protocol;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Tools;
using PulseOffice.Agent.Transport;
using PulseOffice.Platform;
using PulseOffice.Settings;

namespace PulseOffice.Agent.UI;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAgent(this IServiceCollection services, IUserDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton<ISettingsProvider, AgentSettings>();
        services.AddSingleton<IAgentPreferences, AgentPreferences>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPrivateFilesFactory, PrivateFilesFactory>();
        services.TryAddSingleton<IPrivateFiles>(provider => provider.GetRequiredService<IPrivateFilesFactory>().Create());
        services.AddSingleton<IAgentAccessCode>(provider => new FileAgentAccessCode(
            paths.Configuration(Path.Combine("agent", "access-code")), provider.GetRequiredService<IPrivateFiles>()));
        services.AddSingleton<IAgentOperationLog>(provider => new AgentOperationLog(
            paths.Configuration(Path.Combine("agent", "operations.jsonl")), provider.GetRequiredService<IPrivateFiles>(),
            provider.GetRequiredService<ILogger<AgentOperationLog>>()));
        services.AddSingleton(new AgentLimits());
        services.AddSingleton<IAgentSessions, AgentSessions>();
        services.AddSingleton<IAgentRequestGate, AgentRequestGate>();
        services.AddSingleton<IAgentModuleNames, AgentModuleNames>();
        services.AddSingleton<IAgentFileSystem, AgentFileSystem>();
        services.AddSingleton<IAgentCommandPolicy, AgentCommandPolicy>();
        services.AddSingleton<IScriptMemberAccess, ScriptMemberAccess>();
        services.AddSingleton<IAgentClientInstructions, AgentClientInstructions>();

        services.AddSingleton<IAgentUiThread, AvaloniaAgentUiThread>();
        services.AddSingleton<IAgentDocumentIds, AgentDocumentIds>();
        services.AddSingleton<IAgentPictureFrame, AgentPictureFrame>();
        services.AddSingleton<IAgentPageRenderer, AgentPageRenderer>();
        services.AddSingleton<IAgentToolTitles, AgentToolTitles>();
        services.AddSingleton<IAgentOperationGate, AgentOperationGate>();
        services.AddSingleton<ShellAgentServices>();
        services.AddSingleton<IAgentWorkspace, ShellAgentWorkspace>();
        services.AddSingleton<IAgentCommands, ShellAgentCommands>();
        services.TryAddSingleton<PulseOffice.Scripting.IScriptExecutionPermissions, PulseOffice.Scripting.ScriptExecutionPermissions>();
        services.AddSingleton<IAgentScripts, ModuleAgentScripts>();
        services.AddSingleton<IAgentScriptCatalog, ScriptApiAgentCatalog>();
        services.AddSingleton<IAgentConfirmation, DialogAgentConfirmation>();

        services.AddSingleton<IAgentToolSource, DocumentToolSource>();
        services.AddSingleton<IAgentToolSource, CommandToolSource>();
        services.AddSingleton<IAgentToolSource, ScriptCatalogToolSource>();
        services.AddSingleton<IAgentToolSource, ViewToolSource>();
        services.AddSingleton<IAgentToolCatalog, AgentToolCatalog>();
        services.AddSingleton<IAgentToolInvoker, AgentToolInvoker>();

        services.AddSingleton<IJsonRpcCodec, JsonRpcCodec>();
        services.AddSingleton<IMcpProtocolVersions, McpProtocolVersions>();
        services.AddSingleton<IMcpMethod, PingMethod>();
        services.AddSingleton<IMcpMethod, ToolsListMethod>();
        services.AddSingleton<IMcpMethod, ToolsCallMethod>();
        services.AddSingleton<IMcpEndpoint, McpEndpoint>();
        services.AddSingleton<IAgentServer, AgentHttpServer>();
        services.AddSingleton<IAgentServerController, AgentServerController>();

        services.AddSingleton<AgentConnectionServices>();
        services.AddSingleton<PulseOffice.UI.Controls.Settings.ISettingEditor, AgentConnectionSettingEditor>();
        services.AddSingleton<IAgentStatusIndicator, AgentStatusIndicator>();
        return services;
    }
}
