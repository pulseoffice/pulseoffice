namespace PulseOffice.Presentations;

public interface ISlideLayoutRegistry
{
    string OoxmlType(SlideLayout layout);

    string OdfName(SlideLayout layout);

    SlideLayout FromOoxml(string type);

    SlideLayout FromOoxml(string type, string? name);

    SlideLayout FromOdf(string name);

    bool TryFromOdf(string name, out SlideLayout layout);
}

public sealed class SlideLayoutRegistry : ISlideLayoutRegistry
{
    private sealed record Entry(SlideLayout Layout, string Ooxml, string Odf);

    private readonly IReadOnlyList<Entry> _entries =
    [
        new(SlideLayout.TitleSlide, "title", "AL-title"),
        new(SlideLayout.TitleAndContent, "obj", "AL-titleContent"),
        new(SlideLayout.SectionHeader, "secHead", "AL-section"),
        new(SlideLayout.TwoContent, "twoObj", "AL-two"),
        new(SlideLayout.Comparison, "twoTxTwoObj", "AL-comparison"),
        new(SlideLayout.TitleOnly, "titleOnly", "AL-titleOnly"),
        new(SlideLayout.Blank, "blank", "AL-blank"),
        new(SlideLayout.ContentWithCaption, "objTx", "AL-contentCaption"),
        new(SlideLayout.PictureWithCaption, "picTx", "AL-pictureCaption"),
        new(SlideLayout.VerticalText, "vertTx", "AL-verticalText"),
        new(SlideLayout.VerticalTitleAndText, "vertTitleAndTx", "AL-verticalTitleText"),
        new(SlideLayout.CenteredText, "objOnly", "AL-centeredText"),
        new(SlideLayout.ContentAndTwoContent, "objAndTwoObj", "AL-contentTwoContent"),
        new(SlideLayout.TwoContentAndContent, "twoObjAndObj", "AL-twoContentContent"),
        new(SlideLayout.ContentOverContent, "objOverTx", "AL-contentOverContent"),
        new(SlideLayout.TwoContentOverContent, "twoObjOverTx", "AL-twoContentOverContent"),
        new(SlideLayout.FourContent, "fourObj", "AL-fourContent"),
        new(SlideLayout.SixContent, "cust", "AL-sixContent"),
        new(SlideLayout.VerticalTitleTextChart, "vertTitleAndTxOverChart", "AL-verticalTitleTextChart"),
        new(SlideLayout.TitleTwoVerticalText, "twoColTx", "AL-titleTwoVerticalText"),
    ];

    public string OoxmlType(SlideLayout layout) => Find(layout).Ooxml;

    public string OdfName(SlideLayout layout) => Find(layout).Odf;

    private readonly IReadOnlyDictionary<string, SlideLayout> _ooxmlAliases = new Dictionary<string, SlideLayout>(StringComparer.Ordinal)
    {
        ["tx"] = SlideLayout.TitleAndContent,
        ["twoTxTwoObj"] = SlideLayout.Comparison,
        ["txAndObj"] = SlideLayout.TwoContent,
        ["objAndTx"] = SlideLayout.TwoContent,
        ["txAndChart"] = SlideLayout.TwoContent,
        ["chartAndTx"] = SlideLayout.TwoContent,
        ["txAndClipArt"] = SlideLayout.TwoContent,
        ["clipArtAndTx"] = SlideLayout.TwoContent,
        ["txAndMedia"] = SlideLayout.TwoContent,
        ["mediaAndTx"] = SlideLayout.TwoContent,
        ["twoColTxOverObj"] = SlideLayout.TwoContent,
        ["chart"] = SlideLayout.TitleAndContent,
        ["tbl"] = SlideLayout.TitleAndContent,
        ["clipArtAndVertTx"] = SlideLayout.VerticalText,
        ["dgm"] = SlideLayout.TitleAndContent,
        ["txOverObj"] = SlideLayout.ContentOverContent,
        ["objOverTx"] = SlideLayout.ContentOverContent,
        ["obj"] = SlideLayout.TitleAndContent,
        ["txAndTwoObj"] = SlideLayout.ContentAndTwoContent,
        ["twoObjAndTx"] = SlideLayout.TwoContentAndContent,
    };

    private readonly IReadOnlyDictionary<int, SlideLayout> _impressAutoLayouts = new Dictionary<int, SlideLayout>
    {
        [0] = SlideLayout.TitleSlide,
        [1] = SlideLayout.TitleAndContent,
        [2] = SlideLayout.TitleAndContent,
        [3] = SlideLayout.TwoContent,
        [4] = SlideLayout.TwoContent,
        [5] = SlideLayout.TitleAndContent,
        [6] = SlideLayout.TwoContent,
        [7] = SlideLayout.TwoContent,
        [8] = SlideLayout.TitleAndContent,
        [9] = SlideLayout.TwoContent,
        [10] = SlideLayout.TwoContent,
        [11] = SlideLayout.TitleAndContent,
        [12] = SlideLayout.ContentAndTwoContent,
        [13] = SlideLayout.TwoContent,
        [14] = SlideLayout.ContentOverContent,
        [15] = SlideLayout.TwoContentAndContent,
        [16] = SlideLayout.TwoContentOverContent,
        [17] = SlideLayout.ContentOverContent,
        [18] = SlideLayout.FourContent,
        [19] = SlideLayout.TitleOnly,
        [20] = SlideLayout.Blank,
        [27] = SlideLayout.VerticalTitleTextChart,
        [28] = SlideLayout.VerticalTitleAndText,
        [29] = SlideLayout.VerticalText,
        [30] = SlideLayout.TitleTwoVerticalText,
        [32] = SlideLayout.CenteredText,
        [34] = SlideLayout.SixContent,
    };

    public SlideLayout FromOoxml(string type) =>
        _entries.FirstOrDefault(e => e.Ooxml == type)?.Layout
        ?? (_ooxmlAliases.TryGetValue(type, out var alias) ? alias : SlideLayout.Blank);

    public SlideLayout FromOoxml(string type, string? name) =>
        Enum.TryParse<SlideLayout>(name, out var named) && Find(named).Ooxml == type ? named : FromOoxml(type);

    public SlideLayout FromOdf(string name) =>
        TryFromOdf(name, out var layout) ? layout : SlideLayout.Blank;

    public bool TryFromOdf(string name, out SlideLayout layout)
    {
        var entry = _entries.FirstOrDefault(e => e.Odf == name);
        if (entry is not null)
        {
            layout = entry.Layout;
            return true;
        }

        var marker = name.LastIndexOf('T');
        if (name.StartsWith("AL", StringComparison.Ordinal) && marker > 2
            && int.TryParse(name.AsSpan(marker + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var code)
            && _impressAutoLayouts.TryGetValue(code, out layout))
            return true;

        layout = SlideLayout.Blank;
        return false;
    }

    private Entry Find(SlideLayout layout) =>
        _entries.FirstOrDefault(e => e.Layout == layout) ?? _entries[^1];
}
