using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class Round300TabCloseOrderTests
{
    private static OpenDocument Tab(string title) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack());

    [Fact]
    public void Closing_the_active_tab_activates_its_right_neighbour_or_the_left_one_at_the_end()
    {
        var workspace = new DocumentWorkspace();
        var (a, b, c, d) = (Tab("A"), Tab("B"), Tab("C"), Tab("D"));
        foreach (var tab in new[] { a, b, c, d }) workspace.Open(tab);

        workspace.Activate(b);
        workspace.Close(b);
        Assert.Same(c, workspace.Active);

        workspace.Activate(d);
        workspace.Close(d);
        Assert.Same(c, workspace.Active);
    }

    [Fact]
    public void Moving_a_tab_reorders_the_documents_and_keeps_the_active_one()
    {
        var workspace = new DocumentWorkspace();
        var (a, b, c) = (Tab("A"), Tab("B"), Tab("C"));
        foreach (var tab in new[] { a, b, c }) workspace.Open(tab);
        OpenDocument? moved = null;
        workspace.DocumentMoved += (_, document) => moved = document;

        workspace.Move(c, 0);
        workspace.Move(a, 99);

        Assert.Equal(["C", "B", "A"], workspace.Documents.Select(document => document.Title));
        Assert.Same(a, moved);
        Assert.Same(c, workspace.Active);
    }
}
