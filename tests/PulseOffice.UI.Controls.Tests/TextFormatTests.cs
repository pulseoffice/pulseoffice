using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using PulseOffice.Document.Pages;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Pages;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Controls.Text;
using PulseOffice.UI.Controls.TextFormat;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class TextFormatTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly DesignTokens.Metrics Metrics = DesignTokens.For(InterfaceDensity.Compact);

    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.Italian);

    private readonly ListLabeler _labeler = new();

    private sealed class ManualTimers : IDebounceTimerFactory
    {
        public List<Action> Actions { get; } = [];

        public int Kicks { get; private set; }

        public IDebounceTimer Create(TimeSpan delay, Action action)
        {
            Actions.Add(action);
            return new Timer(this);
        }

        private sealed class Timer(ManualTimers owner) : IDebounceTimer
        {
            public bool IsPending { get; private set; }

            public void Kick()
            {
                owner.Kicks++;
                IsPending = true;
            }

            public void Flush() => IsPending = false;

            public void Dispose() => IsPending = false;
        }
    }

    private Dictionary<string, object> Answers(IEnumerable<FormTab> tabs) =>
        tabs.SelectMany(tab => tab.Fields).ToDictionary(field => field.Key, field => field switch
        {
            CheckField check => (object)check.Initial,
            NumberField number => number.Initial,
            ChoiceField choice => choice.Initial,
            IconChoiceField icon => icon.Initial,
            ColorField color => new ColorFieldValue().Normalize(color.Initial),
            FontField font => font.Initial,
            _ => string.Empty,
        });

    private CharacterFormatTabs Characters(ITextFormatPreviews? previews = null) =>
        new(null!, new LanguageChoices(_texts), new TextFormatIcons(), previews ?? null!, new CharacterSampleLayout(), _texts);

    private ParagraphFormatTabs Paragraphs(ITextFormatPreviews? previews = null) =>
        new(new TextFormatIcons(), previews ?? null!, new ParagraphSampleLayout(), _texts);

    [Fact]
    public void ListLabelsFollowNumberingAndDisplayedLevels()
    {
        Assert.Equal("xiv", _labeler.Number(ListNumbering.LowerRoman, 14));
        Assert.Equal("AB", _labeler.Number(ListNumbering.UpperLetter, 28));
        ListLevelFormat[] levels =
        [
            new(ListNumbering.Arabic) { Suffix = string.Empty },
            new(ListNumbering.Arabic) { Suffix = string.Empty, DisplayLevels = 2 },
            new(ListNumbering.Bullet) { Bullet = "◦" },
        ];

        Assert.Equal("2.3", _labeler.Label(levels, 1, [2, 3, 0]));
        Assert.Equal("◦", _labeler.Label(levels, 2, [2, 3, 1]));
    }

    [Fact]
    public void ListSampleRowsCountPerLevelAndRestartBelow()
    {
        var rows = new ListSampleRows(_labeler);
        ListLevelFormat[] levels =
        [
            new(ListNumbering.UpperRoman) { Indent = 18 },
            new(ListNumbering.LowerLetter) { Suffix = ")", Indent = 36 },
        ];

        var sample = rows.Rows(levels, [0, 1, 1, 0, 1]);

        Assert.Equal(["I.", "a)", "b)", "II.", "a)"], sample.Select(row => row.Label));
        Assert.Equal(36, sample[1].TextX);
        Assert.Equal(18, sample[1].LabelX);
        Assert.Equal([0, 1, 2, 2, 1, 0, 1], rows.Pattern(ListSampleMode.Headings, 10));
        Assert.All(rows.Pattern(ListSampleMode.Items, 1), level => Assert.Equal(0, level));
    }

    [Fact]
    public void ListPresetsRespectWhatTheSurfaceCanWrite()
    {
        var catalog = new ListPresetCatalog(_texts);
        var deck = catalog.Presets(new ListFormatRequest([new ListLevelFormat(ListNumbering.None)], 0, [ListNumbering.None, ListNumbering.Bullet, ListNumbering.Arabic], ListFeatures.Symbol));
        var headings = catalog.Presets(new ListFormatRequest([new ListLevelFormat(ListNumbering.None)], 0, [ListNumbering.None, ListNumbering.Arabic, ListNumbering.UpperRoman], ListFeatures.All, ListSampleMode.Headings));

        Assert.Contains(deck, preset => preset.Level.Numbering == ListNumbering.Bullet);
        Assert.Equal(["arabic.dot"], deck.Where(preset => preset.Level.Numbering == ListNumbering.Arabic).Select(preset => preset.Key));
        Assert.DoesNotContain(headings, preset => preset.Level.Numbering == ListNumbering.Bullet);

        var levels = catalog.Apply([new ListLevelFormat(ListNumbering.None), new ListLevelFormat(ListNumbering.None)], 1, headings.Single(preset => preset.Key == "outline.full"));
        Assert.Equal(2, levels[1].DisplayLevels);
        Assert.Equal(ListNumbering.None, levels[0].Numbering);
        Assert.Equal("outline.full", catalog.Matching(headings, levels[1], 1)!.Key);
    }

    [Fact]
    public void ParagraphSampleMovesWithIndentsAlignmentAndSpacing()
    {
        var layout = new ParagraphSampleLayout();
        var plain = layout.Lines(new ParagraphFormatState(), 240, 400).Where(line => line.Current).ToList();
        var indented = layout.Lines(new ParagraphFormatState { Left = 72, FirstLine = 36 }, 240, 400).Where(line => line.Current).ToList();
        var right = layout.Lines(new ParagraphFormatState { Align = TextAlign.Right }, 240, 400).Where(line => line.Current).ToList();
        var doubled = layout.Lines(new ParagraphFormatState { LineValue = 200 }, 240, 400).Where(line => line.Current).ToList();

        Assert.Equal(5, plain.Count);
        Assert.True(indented[0].X > indented[1].X);
        Assert.True(indented[1].X > plain[1].X);
        Assert.Equal(right[0].X + right[0].Width, right[^1].X + right[^1].Width, 3);
        Assert.True(doubled[1].Y - doubled[0].Y > (plain[1].Y - plain[0].Y) * 1.9);
    }

    [Fact]
    public void CharacterTabsReadBackWhatTheyShowAndOfferOnlyTheSurfaceFeatures()
    {
        var tabs = Characters();
        var current = new CharacterFormatState { Font = "Liberation Serif", Size = 14, Bold = true, Underline = UnderlineKind.Double, Color = 0xFF112233, Baseline = TextBaseline.Subscript };
        var request = new CharacterFormatRequest(current, CharacterFeatures.All, "Testo");

        Assert.Equal(current, tabs.Read(request, Answers(tabs.Tabs(request))));

        var deck = new CharacterFormatRequest(current, CharacterFeatures.Basic | CharacterFeatures.Position, string.Empty);
        var keys = tabs.Tabs(deck).SelectMany(tab => tab.Fields).Select(field => field.Key).ToList();
        Assert.DoesNotContain("language", keys);
        Assert.DoesNotContain("relief", keys);
        Assert.Contains("position", keys);

        var answers = Answers(tabs.Tabs(request));
        answers["fontStyle"] = 3;
        answers["underline"] = 0;
        answers["color"] = string.Empty;
        var read = tabs.Read(request, answers);
        Assert.True(read.Italic && read.Bold);
        Assert.Equal(UnderlineKind.None, read.Underline);
        Assert.Null(read.Color);
    }

    [Fact]
    public void ParagraphLinePresetsAndCustomSpacing()
    {
        var tabs = Paragraphs();
        var request = new ParagraphFormatRequest(new ParagraphFormatState { LineValue = 115 }, ParagraphFeatures.All);
        var tab = tabs.Tab(request);
        var answers = Answers([tab]);

        Assert.Equal(1, ((IconChoiceField)tab.Fields.Single(field => field.Key == "linePreset")).Initial);
        Assert.Equal(request.Current, tabs.Read(request, answers));

        answers["linePreset"] = 4;
        answers["lineMode"] = 2;
        answers["lineValue"] = 16.0;
        Assert.Equal((LineHeightMode.Exact, 16.0), (tabs.Read(request, answers).LineMode, tabs.Read(request, answers).LineValue));
        Assert.False(tab.Fields.Single(field => field.Key == "lineValue").ShownWhen!(new Dictionary<string, object> { ["linePreset"] = 2 }));
    }

    [Fact]
    public Task LivePreviewFollowsTheFormAcrossTabs() => Session.Dispatch(() =>
    {
        var timers = new ManualTimers();
        var tabs = Characters(new TextFormatPreviews(timers, _texts));
        var request = new CharacterFormatRequest(new CharacterFormatState { Size = 12 }, CharacterFeatures.Size | CharacterFeatures.Position, "Ab");
        var (_, panels) = new TabbedForms(null!, _texts).Build(tabs.Tabs(request));

        var pane = Assert.IsType<LivePreviewPane>(panels[1].Preview);
        Assert.Equal(TextBaseline.Normal, Sample(pane).State.Baseline);

        var position = panels[1].Layout.Sections.SelectMany(section => section.Rows).Select(row => row.Editor).OfType<IconChoiceGroup>().Single();
        position.SelectedIndex = 1;
        Assert.True(timers.Kicks > 0);
        timers.Actions[1]();
        Assert.Equal(TextBaseline.Superscript, Sample(pane).State.Baseline);
        Assert.Equal("Ab", Sample(pane).Sample);
    }, CancellationToken.None);

    [Fact]
    public Task ListEditorEditsEveryLevelAndReportsTheChosenOne() => Session.Dispatch(() =>
    {
        var editors = new ListLevelsEditors(new ListPresetCatalog(_texts), new ListSampleRows(_labeler), new GalleryNavigator(), new MeasureUnits(), null!, new TextFormatIcons(), _texts, null!);
        var request = new ListFormatRequest([.. Enumerable.Repeat(new ListLevelFormat(ListNumbering.Arabic), 3)], 0,
            [ListNumbering.None, ListNumbering.Bullet, ListNumbering.Arabic], ListFeatures.All);
        var editor = editors.Create(request, Metrics);
        var changes = 0;
        editor.Changed += (_, _) => changes++;

        editor.SelectLevel(2);
        editor.Choose(editor.Gallery.Items.First(item => item.Value.Key == "bullet.➢").Value);

        Assert.Equal(2, editor.Answer.Level);
        Assert.Equal(ListNumbering.Bullet, editor.Levels[2].Numbering);
        Assert.Equal("➢", editor.Levels[2].Bullet);
        Assert.Equal(ListNumbering.Arabic, editor.Levels[0].Numbering);
        Assert.Equal(editor.Gallery.IndexOf(editor.Gallery.Items.First(item => item.Value.Key == "bullet.➢").Value), editor.Gallery.SelectedIndex);
        Assert.True(changes >= 2);
    }, CancellationToken.None);

    [Fact]
    public Task MarginsEditorHighlightsTheMatchingPreset() => Session.Dispatch(() =>
    {
        var editors = new PageMarginsEditors(new MarginPresetCatalog(new MeasureUnits()), new PageSketchLayout(), new GalleryNavigator(), new MeasureUnits(), new TextFormatIcons(), _texts);
        var editor = editors.Create(new PageMargins(2, 2, 2, 2), "cm", true, Metrics);

        Assert.Equal("normal", editor.Gallery.SelectedItem!.Key);
        editor.Apply(editor.Gallery.Items.Single(item => item.Value.Key == "mirrored").Value.Margins);

        Assert.True(editor.Margins.Mirrored);
        Assert.Equal(3.18, editor.Margins.Left, 2);
        Assert.Equal("mirrored", editor.Gallery.SelectedItem!.Key);

        editor.Apply(new PageMargins(1, 1, 1, 1));
        Assert.Equal(-1, editor.Gallery.SelectedIndex);
    }, CancellationToken.None);

    [Fact]
    public Task ColumnsAndPageNumberGalleriesFeedTheirValues() => Session.Dispatch(() =>
    {
        var columns = new ColumnLayoutEditors(new PageSketchLayout(), new GalleryNavigator(), new MeasureUnits(), new TextFormatIcons(), _texts).Create(new ColumnLayout(2, 1.25, true), "cm", Metrics);
        Assert.Equal(2, columns.Gallery.SelectedItem);
        Assert.Equal(new ColumnLayout(2, 1.25, true), columns.Layout);

        var galleries = new PageNumberPositionGalleries(new PageSketchLayout(), new GalleryNavigator(), _texts);
        var gallery = galleries.Create(new PageNumberPosition(false, PageNumberAlignment.Right), Metrics);
        Assert.Equal(6, galleries.Positions.Count);
        Assert.Equal(new PageNumberPosition(false, PageNumberAlignment.Right), gallery.SelectedItem);
        var sketch = Assert.IsType<PagePreview>(gallery.Thumbnails[gallery.SelectedIndex]).Sketch!;
        Assert.Null(sketch.Header);
        Assert.Equal("1", sketch.Footer!.Right);
    }, CancellationToken.None);

    [Fact]
    public void EveryTextFormatKeyIsTranslatedInFiveLanguages()
    {
        string[] keys =
        [
            "textFormat.character.tab.font", "textFormat.paragraph.tab", "textFormat.list.tab", "textFormat.list.numbering.upperroman",
            "page.margins.mirrored", "page.columns.4", "page.number.bottom.center", "textFormat.character.smallCaps",
        ];
        foreach (var language in new[] { UiLanguage.Italian, UiLanguage.English, UiLanguage.German, UiLanguage.Spanish, UiLanguage.French })
        {
            var catalog = new TextCatalog(language);
            Assert.All(keys, key => Assert.True(catalog.Contains(key), $"{language}: {key}"));
        }
    }

    private CharacterSampleView Sample(LivePreviewPane pane) =>
        Assert.IsType<CharacterSampleView>(pane.Rendered);
}
