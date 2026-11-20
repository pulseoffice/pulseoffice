using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round316FolioFieldHelpTests
{
    [Fact]
    public Task The_caret_inside_a_titled_form_field_shows_the_shared_input_help_note() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome: Ada");
        var paragraph = scene.Editor.Caret.ParagraphId;
        scene.Services.GetRequiredService<IContentControlEditing>().Insert(scene.Document, scene.History,
            new DocumentRange(new DocumentPosition(paragraph, 6), new DocumentPosition(paragraph, 9)), new ContentControl { Kind = ContentControlKind.PlainText, Title = "Nome" });

        scene.Editor.PlaceCaret(new DocumentPosition(paragraph, 7), false);
        Assert.True(scene.View.FieldHelp.Box.IsVisible);
        Assert.Equal("Nome", scene.View.FieldHelp.Title);

        scene.Editor.PlaceCaret(new DocumentPosition(paragraph, 2), false);
        Assert.False(scene.View.FieldHelp.Box.IsVisible);
    }, CancellationToken.None);
}
