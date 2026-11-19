using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Indexes;
using PulseOffice.Documents.UI.StyleCatalog;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round269IndexStyleCandidatesTests
{
    private sealed class ShoutingNames : IFolioStyleNames
    {
        public string Title(DocumentStyle style) => Title(style.Name);

        public string Title(string name) => "T:" + name;

        public bool IsBuiltIn(StyleFamily family, string name) => false;
    }

    [Fact]
    public void The_toc_style_list_holds_only_used_or_assigned_styles_under_their_shown_names()
    {
        var document = new TextDocument();
        document.Styles.Paragraph.Add(new ParagraphStyle("Used"));
        document.Styles.Paragraph.Add(new ParagraphStyle("Unused"));
        document.Styles.Paragraph.Add(new ParagraphStyle("Assigned"));
        document.Body.Add(new ParagraphBlock("p", "x", "Used"));
        var candidates = new FolioIndexStyleCandidates(new ShoutingNames(), new StyleUsage(new BlockTree()));

        var names = candidates.Candidates(document, IndexSourceStyles.None.With("Assigned", 2)).Select(style => style.Name).ToList();

        Assert.Contains("Used", names);
        Assert.Contains("Assigned", names);
        Assert.DoesNotContain("Unused", names);
        Assert.Equal("T:Used", candidates.Title(document.Styles.Paragraph.Find("Used")!));
    }

    [Fact]
    public void A_style_not_yet_used_is_offered_for_assignment()
    {
        var document = new TextDocument();
        document.Styles.Paragraph.Add(new ParagraphStyle("Used"));
        document.Styles.Paragraph.Add(new ParagraphStyle("Unused"));
        document.Body.Add(new ParagraphBlock("p", "x", "Used"));
        var candidates = new FolioIndexStyleCandidates(new ShoutingNames(), new StyleUsage(new BlockTree()));

        var others = candidates.Others(document, IndexSourceStyles.None).Select(style => style.Name).ToList();

        Assert.Contains("Unused", others);
        Assert.DoesNotContain("Used", others);
    }
}
