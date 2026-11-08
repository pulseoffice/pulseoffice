using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.Shell.StylePanels;
using PulseOffice.UI.Controls.StylePanels;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class ReadOnlyStylesPanelTests
{
    private sealed class RecordingSource : IStylesPanelSource
    {
        public List<string> Calls { get; } = [];

        public IReadOnlyList<StyleFamilyChoice> Families => [];

        public IReadOnlyList<StyleListEntry> Styles(string family) => [];

        public string? Current(string family) => null;

        public bool CanApply(string family) => true;

        public bool Can(StylePanelAction action, string family, string? style) => true;

        public void Apply(string family, string style) => Calls.Add("apply");

        public Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct)
        {
            Calls.Add(action.ToString());
            return Task.CompletedTask;
        }

        public event EventHandler? Changed { add { } remove { } }
    }

    [Fact]
    public async Task UnDocumentoInSolaLetturaNonSiCambiaDalPannelloStili()
    {
        var workspace = new DocumentWorkspace();
        var styles = new ModuleStylesSource(workspace);
        var inner = new RecordingSource();
        styles.Register(ModuleKind.Documents, inner);
        workspace.Open(new OpenDocument(ModuleKind.Documents, "Lettera", new Avalonia.Controls.Panel(), new UndoStack()) { IsReadOnly = true });

        Assert.False(styles.CanApply("paragraph"));
        Assert.False(styles.Can(StylePanelAction.Delete, "paragraph", "Titolo"));
        styles.Apply("paragraph", "Titolo");
        await styles.RunAsync(StylePanelAction.Delete, "paragraph", "Titolo", TestContext.Current.CancellationToken);

        Assert.Empty(inner.Calls);
    }
}
