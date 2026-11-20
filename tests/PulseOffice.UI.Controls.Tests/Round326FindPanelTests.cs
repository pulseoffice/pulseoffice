using PulseOffice.Localization;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Search;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round326FindPanelTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public Task The_find_panel_highlights_as_it_types_and_walks_both_ways() => Session.Dispatch(() =>
    {
        var target = new Target();
        var source = new Source(target);
        var panel = new FindPanelView(source, new TextCatalog(UiLanguage.English));
        panel.Rebuild();

        panel.Query = "ago";
        panel.Refresh();
        panel.Go(backwards: false);
        panel.Go(backwards: true);
        panel.Query = string.Empty;
        panel.Refresh();

        Assert.Equal("ago", source.Highlighted);
        Assert.Equal(["next", "previous"], target.Walks);
        Assert.True(source.Cleared);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task The_dialog_offers_format_choices_and_sends_the_chosen_values() => Session.Dispatch(() =>
    {
        var target = new Target();
        var view = new FindReplaceView(target, new TextCatalog(UiLanguage.English));

        Assert.Contains("1", view.Summary, StringComparison.Ordinal);
        Assert.Equal(string.Empty, target.LastChoice);
        return Task.CompletedTask;
    }, CancellationToken.None);

    private sealed class Source(Target target) : IFindPanelSource
    {
        public string? Highlighted { get; private set; }

        public bool Cleared { get; private set; }

        public IFindReplaceTarget? Target() => target;

        public void Highlight(FindRequest request) => Highlighted = request.Query;

        public void ClearHighlights() => Cleared = true;

        public Task OpenReplaceAsync(string query) => Task.CompletedTask;
    }

    private sealed class Target : IFindReplaceTarget
    {
        public List<string> Walks { get; } = [];

        public string? LastChoice { get; private set; }

        public IReadOnlyList<FindOption> Options { get; } = [new FindOption("case", "find.matchCase")];

        public IReadOnlyList<FindChoice> Choices { get; } =
            [new FindChoice("emphasis", "find.format.emphasis", [new(string.Empty, "any"), new("bold", "Bold")])];

        public string InitialQuery => "ago";

        public bool IsReadOnly => false;

        public bool SupportsFindNext => true;

        public bool SupportsFindPrevious => true;

        public string? Summarize(FindRequest request)
        {
            LastChoice = request.Choices.GetValueOrDefault("emphasis");
            return "1";
        }

        public string FindNext(FindRequest request)
        {
            Walks.Add("next");
            return string.Empty;
        }

        public string FindPrevious(FindRequest request)
        {
            Walks.Add("previous");
            return string.Empty;
        }

        public string ReplaceAll(FindRequest request) => string.Empty;
    }
}
