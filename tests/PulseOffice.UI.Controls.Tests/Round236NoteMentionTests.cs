using PulseOffice.Localization;
using PulseOffice.UI.Controls.Review;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round236NoteMentionTests
{
    [Fact]
    public Task The_note_prompt_editor_takes_a_mention_picker() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var prompt = new TextPromptView(string.Empty, "Ask @A", new TextCatalog(UiLanguage.English), multiline: true);
        var pickers = new MentionPickers(new MentionQuery());

        pickers.Create().Attach(prompt.Editor, () => ["Ada Lovelace"]);
        prompt.Editor.CaretIndex = prompt.Editor.Text!.Length;
        prompt.Editor.Text += "d";

        Assert.Equal("Ask @Ad", prompt.Editor.Text);
        Assert.NotSame(pickers.Create(), pickers.Create());
    }, TestContext.Current.CancellationToken);
}
