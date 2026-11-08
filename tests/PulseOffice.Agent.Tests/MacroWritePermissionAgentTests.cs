using System.Reflection;
using PulseOffice.Agent.Hosting;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;
using Xunit;

namespace PulseOffice.Agent.Tests;

public sealed class MacroWritePermissionAgentTests
{
    private readonly Type[] _grantingServices = [typeof(IMacroWriteGrants), typeof(FileMacroWriteGrants), typeof(IMacroPermissionPolicy), typeof(MacroPermissionPolicy)];

    [Fact]
    public void L_agente_non_raggiunge_i_permessi_di_scrittura_delle_macro()
    {
        var reached = typeof(ModuleAgentScripts).Assembly.GetTypes()
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters().Select(parameter => (type, parameter.ParameterType)))
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(field => (type, field.FieldType))))
            .Where(pair => _grantingServices.Contains(pair.Item2))
            .Select(pair => pair.type.FullName)
            .ToList();

        Assert.Empty(reached);
    }

    [Fact]
    public async Task Uno_script_dell_agente_al_livello_modifica_non_scrive_nella_cartella_delle_macro()
    {
        var root = Path.Combine(Path.GetTempPath(), "pulse-agent-files-" + Guid.NewGuid().ToString("N"));
        var permissions = new ScriptExecutionPermissions();
        var globals = new FilesGlobals(new ScriptFilesFacade(permissions, new MacroFileFolder(root), new PulseOffice.Platform.ConfinedFileWriter(new PulseOffice.Platform.FileSystemLinks())));
        var scripts = new ModuleAgentScripts([new PulseOffice.Scripting.JavaScript.JavaScriptEngine()], [globals], permissions);

        try
        {
            var outcome = await scripts.RunAsync(ModuleKind.Sheets, "files.WriteText('a.txt', 'x')", AgentAccessLevel.Edit, TestContext.Current.CancellationToken);

            Assert.False(outcome.Succeeded);
            Assert.False(File.Exists(Path.Combine(root, "a.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FilesGlobals(ScriptFilesFacade files) : IModuleScriptGlobals
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public IReadOnlyDictionary<string, object> Globals { get; } = new Dictionary<string, object> { ["files"] = files };
    }
}
