using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Review;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ReviewPanelRequestTests
{
    private sealed class Comments : IReviewSource
    {
        public List<(string Id, string Text)> Edited { get; } = [];

        public List<(string Id, string Text)> Replied { get; } = [];

        public ReviewAbilities Abilities { get; } = new(true, true, true, true) { Edit = true };

        public IReadOnlyList<ReviewItem> Changes => [];

        public IReadOnlyList<ReviewItem> Comments_ { get; } = [new("c1", ReviewItemKind.Comment, "Anna", DateTimeOffset.Now, "Da rivedere", 0xFF2563EB)];

        IReadOnlyList<ReviewItem> IReviewSource.Comments => Comments_;

        public string? Current => null;

        public event EventHandler? Changed;

        public void Go(string id) => Changed?.Invoke(this, EventArgs.Empty);

        public void Accept(IReadOnlyList<string> ids)
        {
        }

        public void Reject(IReadOnlyList<string> ids)
        {
        }

        public void Reply(string id, string text) => Replied.Add((id, text));

        public void Edit(string id, string text) => Edited.Add((id, text));

        public void ToggleResolved(string id)
        {
        }

        public void Delete(IReadOnlyList<string> ids)
        {
        }
    }

    [Fact]
    public Task LaModificaDelCommentoAvvieneNelPannelloSenzaModale() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new Comments();
        var requests = new ReviewPanelRequests();
        var panel = new ReviewPanel(source, new IconCatalog(), new TextCatalog(UiLanguage.Italian), requests: requests);
        requests.Request(new ReviewPanelRequest(ReviewPanelRequestKind.Edit, "c1", "Da rivedere"));
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.ShowingComments);
        Assert.True(panel.IsEditing);
        Assert.Equal("Da rivedere", panel.ReplyBox.Text);
        panel.Reply("Rivisto");
        Assert.Equal([("c1", "Rivisto")], source.Edited);
        Assert.False(panel.IsEditing);

        requests.Request(new ReviewPanelRequest(ReviewPanelRequestKind.Reply, "c1", string.Empty));
        panel.Reply("D'accordo");
        Assert.Equal([("c1", "D'accordo")], source.Replied);
    }, CancellationToken.None);

    [Fact]
    public Task IlPulsanteModificaApreIlTestoDelCommentoScelto() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new Comments();
        var panel = new ReviewPanel(source, new IconCatalog(), new TextCatalog(UiLanguage.Italian));
        panel.Show(true);
        panel.Select("c1");
        panel.Press("edit");
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.IsEditing);
        Assert.Equal("Da rivedere", panel.ReplyBox.Text);
        panel.ReplyBox.Text = "Rivisto";
        panel.Reply("Rivisto");
        Assert.Equal([("c1", "Rivisto")], source.Edited);
        Assert.False(panel.IsEditing);
    }, CancellationToken.None);
}
