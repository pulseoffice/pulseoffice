using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls.StylePanels;

namespace PulseOffice.Shell.StylePanels;

public interface IModuleStyleSources
{
    void Register(ModuleKind module, IStylesPanelSource source);
}

public sealed class ModuleStylesSource : IStylesPanelSource, IModuleStyleSources
{
    private readonly IDocumentWorkspace _workspace;
    private readonly Dictionary<ModuleKind, IStylesPanelSource> _sources = [];

    public ModuleStylesSource(IDocumentWorkspace workspace)
    {
        _workspace = workspace;
        workspace.ActiveChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        workspace.DocumentClosed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public void Register(ModuleKind module, IStylesPanelSource source)
    {
        _sources[module] = source;
        source.Changed += (_, _) =>
        {
            if (ReferenceEquals(Active, source)) Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    private IStylesPanelSource? Active =>
        _workspace.Active is { } document && _sources.TryGetValue(document.Module, out var source) ? source : null;

    public IReadOnlyList<StyleFamilyChoice> Families => Active?.Families ?? [];

    private bool Locked => _workspace.Active?.IsReadOnly == true;

    public bool CanApply(string family) => !Locked && (Active?.CanApply(family) ?? false);

    public IReadOnlyList<StyleListEntry> Styles(string family) => Active?.Styles(family) ?? [];

    public string? Current(string family) => Active?.Current(family);

    public bool Can(StylePanelAction action, string family, string? style) => !Locked && (Active?.Can(action, family, style) ?? false);

    public void Apply(string family, string style)
    {
        if (!Locked) Active?.Apply(family, style);
    }

    public Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct) =>
        Locked ? Task.CompletedTask : Active?.RunAsync(action, family, style, ct) ?? Task.CompletedTask;
}
