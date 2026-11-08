using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class MacroEditorGatewayTests
{
    private sealed class RecordingLauncher : IMacroEditorLauncher
    {
        public List<MacroEditorRequest> Requests { get; } = [];

        public Task ShowAsync(MacroEditorRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private readonly MacroEditorRequest _request = new(
        ModuleKind.Sheets, new Dictionary<string, object>(), new ScriptApiCatalog(), null, "title.key");

    [Fact]
    public async Task LAperturaDaUnaRigaPortaLaMacroAllEditor()
    {
        var launcher = new RecordingLauncher();

        await new MacroEditorGateway(launcher, _request).OpenAsync("Foglio1_Activated", CancellationToken.None);

        var sent = Assert.Single(launcher.Requests);
        Assert.Equal("Foglio1_Activated", sent.OpenMacro);
        Assert.Equal("title.key", sent.TitleKey);
        Assert.Null(_request.OpenMacro);
    }

    [Fact]
    public async Task LEditorSiApreAncheSenzaMacro()
    {
        var launcher = new RecordingLauncher();

        await new MacroEditorGateway(launcher, _request).OpenAsync(null, CancellationToken.None);

        Assert.Null(Assert.Single(launcher.Requests).OpenMacro);
    }
}
