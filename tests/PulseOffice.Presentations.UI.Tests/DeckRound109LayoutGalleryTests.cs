using Avalonia.Controls;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Foundation.Zoom;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Design;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound109LayoutGalleryTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private readonly ISlideFactory _factory = new SlideFactory();
    private readonly ISlideLayoutCatalog _catalog = new SlideLayoutCatalog();

    private sealed class Scene
    {
        public required Presentation Deck { get; init; }
        public required UndoStack History { get; init; }
        public required DeckActions Actions { get; init; }
        public required DeckLayoutCommands Commands { get; init; }
        public required ILayoutChoices Choices { get; init; }
        public DeckViewMode Mode { get; set; } = DeckViewMode.Normal;
        public string? MasterLayoutId { get; set; }
        public Slide Current => Deck.Slides[CurrentIndex];
        public int CurrentIndex { get; set; }

        public CommandDefinition Command(string id) => Commands.GetCommands().Single(command => command.Id.Value == id);

        public Task Run(string id, object? argument = null) =>
            Command(id).Execute(new CommandContext(ModuleKind.Presentations, SelectionKind.None, DocumentState.DocumentOpen) { Argument = argument }, CancellationToken.None);

        public bool Enabled(string id) =>
            Command(id).IsEnabled(new CommandContext(ModuleKind.Presentations, SelectionKind.None, DocumentState.DocumentOpen));
    }

    private Scene Build(bool secondMaster = false)
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var standard = deck.Master.Template(SlideLayout.TitleAndContent);
        var quote = standard with
        {
            Id = "layout.custom.1",
            Name = "Quote",
            Placeholders = [.. standard.Placeholders.Select(placeholder => placeholder with { Bounds = placeholder.Bounds with { Left = new Twip(4000) } })],
        };
        deck.Master = deck.Master with { Layouts = [.. deck.Master.Layouts, quote] };
        if (secondMaster)
            deck.AddMaster(new MasterFactory().CreateStandard(deck.Theme) with { Id = "master2", Name = "Second" });
        deck.AddExisting(_factory.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        deck.AddExisting(_factory.Create(deck.NextId(), quote));

        var history = new UndoStack();
        var texts = new FakeTextCatalog();
        var actions = new DeckActions(deck, history, _factory, texts, layoutCatalog: _catalog);
        var choices = new LayoutChoices(new DeckChoiceNames(texts), _catalog);
        var context = new ActiveDeckContext();
        var commands = new DeckLayoutCommands(context, choices, new NoDialogs(), texts);
        var scene = new Scene { Deck = deck, History = history, Actions = actions, Commands = commands, Choices = choices };
        context.Attach(new DeckTarget(deck, actions, () => scene.Current, () => scene.CurrentIndex, () => { },
            _ => { }, () => false, () => null, () => [], _ => { }, _ => { }, () => null,
            () => Task.CompletedTask, mode => scene.Mode = mode, () => scene.Mode, () => null,
            () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => false, _ => false,
            new DeckViewOptions(), _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null)
        {
            MasterLayoutId = () => scene.MasterLayoutId,
            SelectMasterLayout = id => scene.MasterLayoutId = id,
        });
        return scene;
    }

    private RibbonChoice[] Gallery(Scene scene, string id) =>
        [.. (scene.Command(id).RibbonControl is SplitGallerySpec split ? split.Choices() : ((ChoiceSpec)scene.Command(id).RibbonControl!).Choices())];

    [Fact]
    public void The_gallery_lists_every_layout_by_identity_with_the_current_one_checked()
    {
        var scene = Build();
        scene.CurrentIndex = 1;
        var spec = (ChoiceSpec)scene.Command("deck.layouts.gallery").RibbonControl!;
        var choices = spec.Choices();

        Assert.Equal(scene.Deck.Master.Layouts.Count, choices.Count);
        Assert.Equal(2, choices.Count(choice => choice.Title == "titleAndContent" || choice.Title == "Quote"));
        Assert.Contains(choices, choice => choice.Title == "Quote");
        Assert.All(choices, choice => Assert.IsType<LayoutThumbnail>(choice.Thumbnail));
        Assert.All(choices, choice => Assert.Null(choice.Section));
        Assert.Equal("Quote", choices.Single(choice => choice.Key == spec.Current()).Title);
    }

    [Fact]
    public async Task Choosing_a_custom_layout_applies_it_by_identity_in_one_undo_step()
    {
        var scene = Build();
        var quote = Gallery(scene, "deck.layouts.gallery").Single(choice => choice.Title == "Quote");
        var before = scene.History.StatePoint;

        await scene.Run("deck.layouts.gallery", quote.Key);

        Assert.Equal("layout.custom.1", scene.Deck.Slides[0].LayoutId);
        Assert.All(scene.Deck.Slides[0].Elements.OfType<TextElement>(), text => Assert.Equal(4000, text.Bounds.Left.Value));
        scene.History.Undo();
        Assert.Same(before, scene.History.StatePoint);
        Assert.Equal("layout.titleContent", scene.Deck.Slides[0].LayoutId);
    }

    [Fact]
    public async Task With_several_masters_the_gallery_is_grouped_and_another_master_layout_moves_the_slide_in_one_step()
    {
        var scene = Build(secondMaster: true);
        var choices = Gallery(scene, "deck.layouts.gallery");

        Assert.Equal(scene.Deck.Masters.Sum(master => master.Layouts.Count), choices.Length);
        Assert.Equal(scene.Deck.Master.Name, choices[0].Section);
        Assert.Equal("Second", choices[^1].Section);

        var other = choices.First(choice => choice.Section == "Second");
        var before = scene.History.StatePoint;
        await scene.Run("deck.layouts.gallery", other.Key);

        Assert.Equal("master2", scene.Deck.Slides[0].MasterId);
        Assert.Equal(scene.Deck.Masters[1].Layouts[0].Id, scene.Deck.Slides[0].LayoutId);
        Assert.Equal(scene.Deck.Masters[1].Name, Gallery(scene, "deck.layouts.gallery")[0].Section);
        scene.History.Undo();
        Assert.Same(before, scene.History.StatePoint);
        Assert.Null(scene.Deck.Slides[0].MasterId);
        Assert.Equal("layout.titleContent", scene.Deck.Slides[0].LayoutId);
    }

    [Fact]
    public async Task The_new_slide_split_button_adds_a_slide_on_the_chosen_layout_or_on_title_and_content()
    {
        var scene = Build();
        var quote = Gallery(scene, "deck.slide.add").Single(choice => choice.Title == "Quote");

        await scene.Run("deck.slide.add", quote.Key);
        Assert.Equal(3, scene.Deck.Slides.Count);
        Assert.Equal("layout.custom.1", scene.Deck.Slides[1].LayoutId);

        await scene.Run("deck.slide.add");
        Assert.Equal(4, scene.Deck.Slides.Count);
        Assert.Equal(SlideLayout.TitleAndContent, scene.Deck.Slides[1].Layout);
        Assert.Equal("layout.titleContent", scene.Deck.Slides[1].LayoutId);

        scene.History.Undo();
        scene.History.Undo();
        Assert.Equal(2, scene.Deck.Slides.Count);
    }

    [Fact]
    public async Task Deleting_a_custom_layout_moves_its_slides_to_the_standard_one_in_one_undo_step()
    {
        var scene = Build();
        scene.Mode = DeckViewMode.Master;
        scene.MasterLayoutId = "layout.titleContent";
        Assert.False(scene.Enabled("deck.layouts.delete"));
        Assert.False(scene.Enabled("deck.layouts.rename"));
        Assert.True(scene.Enabled("deck.layouts.duplicate"));

        scene.MasterLayoutId = "layout.custom.1";
        Assert.True(scene.Enabled("deck.layouts.delete"));
        var masterBefore = scene.Deck.Master;
        var slideBefore = scene.Deck.Slides[1];

        await scene.Run("deck.layouts.delete");

        Assert.DoesNotContain(scene.Deck.Master.Layouts, layout => layout.Id == "layout.custom.1");
        Assert.Equal("layout.titleContent", scene.Deck.Slides[1].LayoutId);
        Assert.Null(scene.MasterLayoutId);
        Assert.True(scene.History.CanUndo);
        scene.History.Undo();
        Assert.Same(masterBefore, scene.Deck.Master);
        Assert.Same(slideBefore, scene.Deck.Slides[1]);
        Assert.False(scene.History.CanUndo);
    }

    [Fact]
    public async Task Duplicating_and_renaming_a_layout_are_single_steps_and_the_copy_is_selected()
    {
        var scene = Build();
        scene.Mode = DeckViewMode.Master;
        scene.MasterLayoutId = "layout.titleContent";

        await scene.Run("deck.layouts.duplicate");

        var copy = Assert.Single(scene.Deck.Master.Layouts, layout => layout.Id == scene.MasterLayoutId);
        Assert.Equal("layout.custom.2", copy.Id);
        Assert.Equal(SlideLayout.TitleAndContent, copy.Kind);
        Assert.False(_catalog.IsStandard(scene.Deck.Master, copy));

        scene.Actions.Layouts.RenameLayout(scene.Deck.Master, copy.Id, "  Agenda  ");
        Assert.Equal("Agenda", scene.Choices.Title(scene.Deck.Master, _catalog.Find(scene.Deck.Master, copy.Id)!));
        scene.Actions.Layouts.RenameLayout(scene.Deck.Master, "layout.titleContent", "Hijack");
        Assert.NotEqual("Hijack", _catalog.Find(scene.Deck.Master, "layout.titleContent")!.Name);

        scene.History.Undo();
        Assert.Equal(copy.Name, _catalog.Find(scene.Deck.Master, copy.Id)!.Name);
        scene.History.Undo();
        Assert.Null(_catalog.Find(scene.Deck.Master, copy.Id));
    }

    [Fact]
    public Task The_split_button_shows_a_main_part_and_a_gallery_with_one_tile_per_layout() => Session.Dispatch(() =>
    {
        var scene = Build(secondMaster: true);
        var command = scene.Command("deck.slide.add");
        var invoked = new List<RibbonInvocation>();
        var services = new RibbonServices(
            new IconCatalog(), new FakeTextCatalog(), null, new RibbonStripLayout(), new RibbonReductionPlanner(),
            new RibbonSlotCatalog([new SplitGallerySlotFactory()]));
        var context = new RibbonBuildContext(services, DesignTokens.For(InterfaceDensity.Compact), invoked.Add);
        var slot = new SplitGallerySlot(new RibbonItem(command, true, "New slide", null), RibbonShape.Large, RibbonForm.Full, context);

        slot.MainButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new RibbonInvocation("deck.slide.add"), Assert.Single(invoked));

        var gallery = slot.Gallery();
        var tiles = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(gallery).OfType<Button>().Where(button => button.Classes.Contains("tile")).ToList();
        Assert.Equal(scene.Deck.Masters.Sum(master => master.Layouts.Count), tiles.Count);
        Assert.Single(tiles, tile => tile.Classes.Contains("checked"));

        tiles[^1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, invoked.Count);
        Assert.NotNull(invoked[1].Argument);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    private sealed class NoDialogs : PulseOffice.UI.Controls.Services.IDialogService
    {
        public bool IsDialogOpen => false;

        public Task<T?> ShowAsync<T>(Control content, PulseOffice.UI.Controls.Services.DialogOptions options, CancellationToken ct = default) =>
            Task.FromResult<T?>(default);

        public Task ShowAsync(Control content, PulseOffice.UI.Controls.Services.DialogOptions options, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }
}
