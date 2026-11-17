using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ShapePickerTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.English);

    private static ShapeTool[] Tools(List<string> ran) =>
    [
        new("test.dimension", "command.folio.insert.dimensionLine.title", "folio.insert.dimensionLine", () => ran.Add("dimension")),
        new("test.freeform", "command.folio.draw.freeform.title", "folio.draw.freeform", () => ran.Add("freeform")),
    ];

    [Fact]
    public void Sections_follow_the_families_and_lines_hold_the_tools()
    {
        var model = new ShapePickerModel(_texts, Tools([]), []);

        Assert.Equal(
            [ShapeSection.Basic, ShapeSection.Arrows, ShapeSection.Stars, ShapeSection.Callouts, ShapeSection.Symbols, ShapeSection.FlowChart, ShapeSection.Lines],
            model.Sections);
        Assert.Equal(ShapeFamilies.Basic, model.Items(ShapeSection.Basic, string.Empty).Select(choice => choice.Shape!.Value));
        Assert.Equal(["shape.line", "test.dimension", "test.freeform"], model.Items(ShapeSection.Lines, string.Empty).Select(choice => choice.Key));
        Assert.Equal("Block arrows", model.TitleOf(ShapeSection.Arrows));
        Assert.Equal("Dimension line", model.TitleOf(model.Find("test.dimension")!));
    }

    [Fact]
    public void All_lists_each_shape_once_and_search_matches_names_and_sections()
    {
        var model = new ShapePickerModel(_texts, Tools([]), []);

        var all = model.Items(null, string.Empty);
        Assert.Equal(all.Count, all.Select(choice => choice.Key).Distinct().Count());
        Assert.Contains(all, choice => choice.Shape == ShapeKind.FlowDecision);

        Assert.Equal([ShapeKind.Hexagon], model.Items(null, "hexag").Select(choice => choice.Shape!.Value));
        Assert.Equal(ShapeFamilies.Stars, model.Items(null, "STARS").Select(choice => choice.Shape!.Value));
        Assert.Empty(model.Items(ShapeSection.Basic, "chevron"));
    }

    [Fact]
    public void Recent_choices_come_first_and_unknown_keys_are_dropped()
    {
        var recent = new RecentShapes(capacity: 2);
        recent.Remember("shape.star5");
        recent.Remember("gone.tool");
        recent.Remember("shape.hexagon");
        recent.Remember("shape.star5");

        Assert.Equal(["shape.star5", "shape.hexagon"], recent.Keys);

        var model = new ShapePickerModel(_texts, [], ["shape.hexagon", "gone.tool"]);
        Assert.Equal(ShapeSection.Recent, model.Sections[0]);
        Assert.Equal([ShapeKind.Hexagon], model.Items(ShapeSection.Recent, string.Empty).Select(choice => choice.Shape!.Value));
        Assert.Empty(new ShapePickerModel(_texts, [], []).Items(ShapeSection.Recent, string.Empty));
    }

    [Fact]
    public Task The_view_filters_moves_with_the_keyboard_and_inserts() => Session.Dispatch(() =>
    {
        var model = new ShapePickerModel(_texts, Tools([]), []);
        var view = new ShapePickerView(model, _texts, new IconCatalog());
        object? result = "none";
        view.Completed += (_, value) => result = value;
        var window = new Window { Content = view, Width = 800, Height = 560 };
        window.Show();

        Assert.Equal(ShapeSection.Basic, view.Section);
        Assert.Equal(ShapeKind.Rectangle, view.Selected!.Shape);

        view.SearchText = "decision";
        Assert.Null(view.Section);
        Assert.Equal(ShapeKind.FlowDecision, view.Selected!.Shape);

        view.SearchText = string.Empty;
        view.Section = ShapeSection.Arrows;
        window.UpdateLayout();
        Assert.True(view.Gallery!.HandleKey(Key.Right));
        Assert.Equal(ShapeKind.ArrowLeft, view.Selected!.Shape);

        view.Confirm();
        Assert.Equal(ShapeKind.ArrowLeft, Assert.IsType<ShapeChoice>(result).Shape);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task The_chooser_returns_shapes_runs_tools_and_remembers_them() => Session.Dispatch(async () =>
    {
        var ran = new List<string>();
        var recent = new RecentShapes();
        var dialogs = new ScriptedDialogs(["test.freeform", "shape.heart", null]);
        var chooser = new ShapeChooser(dialogs, _texts, new IconCatalog(), recent);

        Assert.Null(await chooser.ChooseAsync(Tools(ran)));
        Assert.Equal(["freeform"], ran);
        Assert.Equal(ShapeKind.Heart, await chooser.ChooseAsync(Tools(ran)));
        Assert.Null(await chooser.ChooseAsync(Tools(ran)));
        Assert.Equal(["shape.heart", "test.freeform"], recent.Keys);
        Assert.Equal("dialog.shapeGallery.title", dialogs.Titles[0]);
    }, CancellationToken.None);

    private sealed class ScriptedDialogs(IReadOnlyList<string?> answers) : IDialogService
    {
        private int _next;

        public List<string> Titles { get; } = [];

        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            Titles.Add(options.TitleKey);
            var view = Assert.IsType<ShapePickerView>(content);
            var key = answers[_next++];
            object? answer = key is null ? null : view.Model.Find(key);
            return Task.FromResult(answer is TResult typed ? typed : default);
        }

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }
}
