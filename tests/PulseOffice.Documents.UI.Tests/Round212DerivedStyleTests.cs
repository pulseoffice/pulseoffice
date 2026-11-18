using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.StyleCatalog;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round212DerivedStyleTests
{
    private sealed class NamingDialogs(string name) : IFolioStyleDialogs
    {
        public Task<string?> NameAsync(StyleFamily family, string? suggestion, CancellationToken ct) => Task.FromResult<string?>(name);

        public Task<bool> ConfirmDeleteAsync(string title, CancellationToken ct) => Task.FromResult(false);

        public Task EditAsync(FolioEditor editor, StyleFamily family, string name, CancellationToken ct) => Task.CompletedTask;

        public Task<string?> RenameAsync(StyleFamily family, string name, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    [Fact]
    public Task A_derived_paragraph_style_follows_itself_when_its_parent_does_and_keeps_the_category() => FolioScene.Session.Dispatch(async () =>
    {
        var scene = new FolioScene();
        var services = scene.Services;
        var dialogs = new NamingDialogs("Child");
        var actions = new FolioStyleActions(services.GetRequiredService<IStyleEditing>(), services.GetRequiredService<IStyleFromSelection>(), dialogs,
            services.GetRequiredService<IFolioStyleNames>(), services.GetRequiredService<IFolioPageStyles>(),
            services.GetRequiredService<PulseOffice.Documents.Editing.Tables.ITableFormatting>());
        scene.Document.Styles.Paragraph.Add(new ParagraphStyle("Body") { ParentName = DefaultStyleNames.Standard, NextStyleName = "Body", Category = ParagraphStyleCategory.List });

        await actions.DeriveAsync(scene.Editor, StyleFamily.Paragraph, "Body", CancellationToken.None);

        var child = scene.Document.Styles.Paragraph.Find("Child")!;
        Assert.Equal("Body", child.ParentName);
        Assert.Equal("Child", child.NextStyleName);
        Assert.Equal(ParagraphStyleCategory.List, child.Category);
    }, CancellationToken.None);
}
