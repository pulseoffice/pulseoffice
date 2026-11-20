using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round303TabStripPointerTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    private static OpenDocument Tab(string title) =>
        new(ModuleKind.Sheets, title, new Panel(), new UndoStack());

    [Fact]
    public Task Dragging_a_tab_shows_the_drop_indicator_and_moves_it_and_a_middle_click_closes_one() => _session.Dispatch(() =>
    {
        var workspace = new DocumentWorkspace();
        var (a, b, c) = (Tab("Alfa"), Tab("Beta"), Tab("Gamma"));
        foreach (var tab in new[] { a, b, c }) workspace.Open(tab);
        var view = new DocumentTabsView(workspace, new TextCatalog(UiLanguage.Italian));
        var closed = new List<OpenDocument>();
        view.CloseRequested += (_, document) => closed.Add(document);
        var window = new Window { Width = 600, Height = 80, Content = view };
        window.Show();
        try
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Point Centre(int index)
            {
                var strip = view.GetVisualDescendants().OfType<StackPanel>()
                    .First(panel => panel.Orientation == Orientation.Horizontal && panel.Children.Count == workspace.Documents.Count);
                var tab = strip.Children[index];
                return tab.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), window)!.Value;
            }

            var target = Centre(2);
            window.MouseDown(Centre(0), MouseButton.Left);
            window.MouseMove(target, RawInputModifiers.LeftMouseButton);
            Assert.Equal(2, view.DropIndicatorIndex);
            window.MouseUp(target, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal([b, c, a], workspace.Documents);
            Assert.Same(a, workspace.Active);
            Assert.Equal(-1, view.DropIndicatorIndex);

            window.UpdateLayout();
            var middle = Centre(0);
            window.MouseDown(middle, MouseButton.Middle);
            window.MouseUp(middle, MouseButton.Middle);
            Assert.Equal([b], closed);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);
}
