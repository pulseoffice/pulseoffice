using PulseOffice.Agent.Hosting;
using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor;

namespace PulseOffice.Agent.UI;

public sealed class ScriptApiAgentCatalog(IScriptApiProvider provider) : IAgentScriptCatalog
{
    public ScriptApiCatalog Catalog => provider.Catalog;
}
