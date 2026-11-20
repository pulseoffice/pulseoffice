using Avalonia;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round307ShellWindowHostTests
{
    private static OpenDocument Tab(string title) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack());

    private sealed class FakeWindow(PixelRect area) : IShellWindowHandle
    {
        public IDocumentWorkspace Workspace { get; } = new DocumentWorkspace();
        public PixelRect Area { get; set; } = area;
        public bool Shown { get; private set; }

        public void Show(PixelPoint? position)
        {
            Shown = true;
            if (position is { } at) Area = new PixelRect(at, Area.Size);
        }

        public void Activate() => Activated?.Invoke(this, EventArgs.Empty);

        public void Close() => Closed?.Invoke(this, EventArgs.Empty);

        public bool Contains(PixelPoint screenPoint) => Area.Contains(screenPoint);

        public int TabIndexAt(PixelPoint screenPoint) => 0;

        public event EventHandler? Activated;
        public event EventHandler? Closed;
    }

    private sealed class Factory : IShellWindowFactory
    {
        public IShellWindowHandle Create() => new FakeWindow(new PixelRect(0, 0, 100, 100));
    }

    private sealed class CountingQuit : IQuitTarget
    {
        public int Count { get; private set; }

        public void Quit() => Count++;
    }

    private readonly FakeWindow _primary = new(new PixelRect(0, 0, 500, 500));
    private readonly CountingQuit _quit = new();
    private readonly WorkspaceSet _set;
    private readonly ShellWindowHost _host;

    public Round307ShellWindowHostTests()
    {
        _set = new WorkspaceSet(_primary.Workspace);
        _host = new ShellWindowHost(_primary, _set, new Factory(), _quit);
    }

    [Fact]
    public void Tearing_off_a_tab_moves_it_to_a_new_window_without_closing_it()
    {
        var first = Tab("a");
        var second = Tab("b");
        _primary.Workspace.Open(first);
        _primary.Workspace.Open(second);
        var closed = 0;
        _primary.Workspace.DocumentClosed += (_, _) => closed++;

        var window = _host.TearOff(second, new PixelPoint(900, 900));

        Assert.NotNull(window);
        Assert.Equal(new[] { first }, _primary.Workspace.Documents);
        Assert.Same(second, window.Workspace.Active);
        Assert.Equal(0, closed);
        Assert.Same(window, _host.Focused);
        Assert.Equal(2, _set.Workspaces.Count);
        Assert.Equal(2, _set.Documents.Count());
    }

    [Fact]
    public void The_only_tab_of_a_window_is_not_torn_off()
    {
        var only = Tab("a");
        _primary.Workspace.Open(only);

        Assert.Null(_host.TearOff(only, new PixelPoint(900, 900)));
        Assert.Single(_host.Windows);
    }

    [Fact]
    public void Dropping_on_another_window_moves_the_tab_and_closes_the_emptied_window()
    {
        var first = Tab("a");
        var second = Tab("b");
        _primary.Workspace.Open(first);
        _primary.Workspace.Open(second);
        var window = _host.TearOff(second, new PixelPoint(900, 900))!;

        Assert.True(_host.DropAt(second, new PixelPoint(10, 10)));

        Assert.Equal(new[] { second, first }, _primary.Workspace.Documents);
        Assert.Single(_host.Windows);
        Assert.DoesNotContain(window.Workspace, _set.Workspaces);
        Assert.Equal(0, _quit.Count);
    }

    [Fact]
    public void Closing_the_last_window_quits_and_the_primary_role_passes_on()
    {
        _primary.Workspace.Open(Tab("a"));
        _primary.Workspace.Open(Tab("b"));
        var window = _host.TearOff(_primary.Workspace.Documents[1], new PixelPoint(900, 900))!;

        _primary.Close();
        Assert.Same(window, _host.Primary);
        Assert.Equal(0, _quit.Count);

        window.Close();
        Assert.Equal(1, _quit.Count);
    }

    [Fact]
    public void Detach_keeps_the_shared_model_and_raises_no_close()
    {
        var workspace = new DocumentWorkspace();
        var first = Tab("a");
        workspace.Open(first);
        var detached = 0;
        var closed = 0;
        workspace.DocumentDetached += (_, _) => detached++;
        workspace.DocumentClosed += (_, _) => closed++;

        Assert.True(workspace.Detach(first));
        Assert.False(workspace.Detach(first));
        Assert.Empty(workspace.Documents);
        Assert.Null(workspace.Active);
        Assert.Equal((1, 0), (detached, closed));
    }

    [Fact]
    public void A_held_document_is_not_detached()
    {
        var workspace = new DocumentWorkspace();
        var first = Tab("a");
        workspace.Open(first);
        using (workspace.Hold(first)) Assert.False(workspace.Detach(first));
        Assert.Single(workspace.Documents);
    }

    [Fact]
    public void Drag_geometry_scrolls_near_the_edges_and_detects_a_tear_off()
    {
        var geometry = new TabStripDragGeometry();

        Assert.True(geometry.ScrollStep(2, 400) < 0);
        Assert.True(geometry.ScrollStep(398, 400) > 0);
        Assert.Equal(0, geometry.ScrollStep(200, 400));
        Assert.Equal(0, geometry.ScrolledOffset(5, -16, 800, 400));
        Assert.Equal(400, geometry.ScrolledOffset(395, 16, 800, 400));
        Assert.True(geometry.IsTornOff(new Point(10, 80), new Size(400, 30)));
        Assert.False(geometry.IsTornOff(new Point(10, 40), new Size(400, 30)));
    }
}
