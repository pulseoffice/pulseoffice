using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.UI.Controls.StylePanels;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioStylesPanelTests
{
    private static FolioStylesSource Source(FolioScene scene) => Source(scene, new NoDialogs());

    private static FolioStylesSource Source(FolioScene scene, NoDialogs dialogs)
    {
        var context = scene.Services.GetRequiredService<IActiveFolioContext>();
        context.Attach(new FolioTarget(scene.Editor, () => default!, _ => { }, () => 1, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask));
        var services = scene.Services;
        var names = services.GetRequiredService<IFolioStyleNames>();
        var actions = new FolioStyleActions(services.GetRequiredService<IStyleEditing>(), services.GetRequiredService<IStyleFromSelection>(), dialogs, names,
            services.GetRequiredService<IFolioPageStyles>(), services.GetRequiredService<PulseOffice.Documents.Editing.Tables.ITableFormatting>());
        return new FolioStylesSource(context, actions, dialogs, names, services.GetRequiredService<IStyleResolver>(),
            services.GetRequiredService<IStyleUsage>(), services.GetRequiredService<PulseOffice.Localization.ITextCatalog>());
    }

    private sealed class NoDialogs : IFolioStyleDialogs
    {
        public Task<string?> NameAsync(StyleFamily family, string? suggestion, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task<bool> ConfirmDeleteAsync(string title, CancellationToken ct) => Task.FromResult(false);

        public Task EditAsync(FolioEditor editor, StyleFamily family, string name, CancellationToken ct) => Task.CompletedTask;

        public string? Renamed { get; set; }

        public Task<string?> RenameAsync(StyleFamily family, string name, CancellationToken ct) => Task.FromResult(Renamed);
    }

    [Fact]
    public Task LeFamiglieHannoIconeEAnteprimeEUsoNelDocumento() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Ciao");
        var source = Source(scene);

        Assert.Equal(["paragraph", "character", "frame", "page", "list", "table"], source.Families.Select(family => family.Key));
        Assert.All(source.Families, family => Assert.StartsWith("panel.family.", family.Icon, StringComparison.Ordinal));

        var paragraphs = source.Styles("paragraph");
        var standard = paragraphs.Single(entry => entry.Name == DefaultStyleNames.Standard);
        Assert.True(standard.InUse);
        Assert.False(standard.Custom);
        var heading = paragraphs.Single(entry => entry.Name == "Heading 1");
        Assert.True(heading.Depth > 0);
        Assert.True(heading.Preview!.Bold);
        Assert.False(heading.InUse);

        Assert.True(source.CanApply("paragraph"));
        Assert.False(source.CanApply("table"));
        Assert.False(source.CanApply("frame"));
        Assert.NotEmpty(source.Styles("table"));
        Assert.NotEmpty(source.Styles("frame"));

        source.Apply("paragraph", "Heading 1");
        Assert.Equal("Heading 1", source.Current("paragraph"));
        Assert.True(source.Styles("paragraph").Single(entry => entry.Name == "Heading 1").InUse);
    }, CancellationToken.None);

    [Fact]
    public Task IlPannelloEliminaLoStilePersonalizzatoSenzaChiedereDiNuovo() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        var source = Source(scene);
        var styles = scene.Services.GetRequiredService<IStyleEditing>();
        styles.Add(scene.Document, scene.History, scene.Document.Styles.Paragraph, new ParagraphStyle("Mio") { ParentName = DefaultStyleNames.Standard }, "test");

        var mine = source.Styles("paragraph").Single(entry => entry.Name == "Mio");
        Assert.True(mine.Custom);
        Assert.True(source.Can(StylePanelAction.Delete, "paragraph", "Mio"));
        Assert.False(source.Can(StylePanelAction.Delete, "paragraph", DefaultStyleNames.Standard));
        Assert.True(source.Can(StylePanelAction.Derive, "paragraph", "Mio"));
        Assert.False(source.Can(StylePanelAction.Edit, "table", DefaultStyleNames.DefaultTableStyle));

        await source.RunAsync(StylePanelAction.Delete, "paragraph", "Mio", CancellationToken.None);
        Assert.DoesNotContain(source.Styles("paragraph"), entry => entry.Name == "Mio");
    }, CancellationToken.None);

    [Fact]
    public Task The_panel_renames_a_custom_style_and_the_text_follows_in_one_undo_step() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        scene.Type("Ciao");
        var dialogs = new NoDialogs { Renamed = "Nostro" };
        var source = Source(scene, dialogs);
        var styles = scene.Services.GetRequiredService<IStyleEditing>();
        styles.Add(scene.Document, scene.History, scene.Document.Styles.Paragraph, new ParagraphStyle("Mio") { ParentName = DefaultStyleNames.Standard }, "test");
        source.Apply("paragraph", "Mio");

        Assert.True(source.Can(StylePanelAction.Rename, "paragraph", "Mio"));
        Assert.False(source.Can(StylePanelAction.Rename, "paragraph", DefaultStyleNames.Standard));
        await source.RunAsync(StylePanelAction.Rename, "paragraph", "Mio", CancellationToken.None);

        Assert.Equal("Nostro", source.Current("paragraph"));
        Assert.Contains(source.Styles("paragraph"), entry => entry.Name == "Nostro");
        Assert.DoesNotContain(source.Styles("paragraph"), entry => entry.Name == "Mio");
        scene.History.Undo();
        Assert.Equal("Mio", source.Current("paragraph"));

        dialogs.Renamed = DefaultStyleNames.TextBody;
        await source.RunAsync(StylePanelAction.Rename, "paragraph", "Mio", CancellationToken.None);
        Assert.Equal(DefaultStyleNames.TextBody + " 1", source.Current("paragraph"));
    }, CancellationToken.None);
}
