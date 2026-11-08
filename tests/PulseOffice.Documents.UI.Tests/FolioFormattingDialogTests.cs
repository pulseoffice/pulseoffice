using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Text;
using PulseOffice.UI.Controls.TextFormat;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFormattingDialogTests
{
    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.Italian);

    private Dictionary<string, object> Answers(IReadOnlyList<FormTab> tabs) =>
        tabs.SelectMany(tab => tab.Fields).ToDictionary(field => field.Key, field => field switch
        {
            CheckField check => (object)check.Initial,
            NumberField number => number.Initial,
            ChoiceField choice => choice.Initial,
            ColorField color => new ColorFieldValue().Normalize(color.Initial),
            FontField font => font.Initial,
            TabStopsField tabs => tabs.Initial,
            PulseOffice.UI.Controls.Borders.BorderEditorField borders => borders.Initial,
            PulseOffice.UI.Controls.Choices.IconChoiceField icon => icon.Initial,
            _ => string.Empty,
        });

    private FolioCharacterTabs Characters() =>
        new(new CharacterFormatTabs(null!, new LanguageChoices(_texts), new TextFormatIcons(), null!, new CharacterSampleLayout(), _texts));

    private FolioParagraphTabs Paragraphs() =>
        new(new ParagraphFormatTabs(new TextFormatIcons(), null!, new ParagraphSampleLayout(), _texts), _texts);

    [Fact]
    public void LaFinestraDelCarattereSenzaModificheNonCambiaNulla()
    {
        var tabs = Characters();
        var current = new TextRun("x") { Font = "Liberation Serif", FontSize = 12, Color = 0xFF112233, Language = "it-IT", LetterSpacing = 1, Case = TextCase.Upper };

        var change = tabs.Read(current, Answers(tabs.Tabs(current)));

        Assert.True(change.IsEmpty, change.ToString());
    }

    [Fact]
    public void LaFinestraDelCarattereScriveSoloCiòCheCambia()
    {
        var tabs = Characters();
        var current = new TextRun("x") { FontSize = 12, Highlight = 0xFFFFFF00 };
        var answers = Answers(tabs.Tabs(current));
        answers["spacing"] = 3.0;
        answers["scale"] = 90.0;
        answers["relief"] = 2;
        answers["case"] = 3;
        answers["hidden"] = true;
        answers["highlight"] = string.Empty;

        var change = tabs.Read(current, answers);

        Assert.Equal(new CharacterProperties { LetterSpacing = 3, ScaleWidth = 90, Relief = FontRelief.Engraved, Case = TextCase.Capitalize, Hidden = true }, change.Set);
        Assert.True(change.ClearHighlight);
        Assert.Null(change.Apply(new DirectFormatting(), current).Highlight);
    }

    [Fact]
    public void LaLinguaPredefinitaToglieLaLinguaDelTesto()
    {
        var tabs = Characters();
        var current = new TextRun("x") { FontSize = 12, Language = "de-DE" };
        var answers = Answers(tabs.Tabs(current));
        answers["language"] = 0;

        var change = tabs.Read(current, answers);

        Assert.False(change.IsEmpty);
        Assert.Null(change.Apply(new DirectFormatting(), current).Language);
        Assert.Null(change.Apply(new CharacterProperties { Language = "de-DE" }).Language);
    }

    [Fact]
    public void LaFinestraDelParagrafoSenzaModificheNonCambiaNulla()
    {
        var tabs = Paragraphs();
        var scope = new ParagraphDialogScope(new TextParagraph("x")
        {
            MarginLeft = 10,
            LineHeight = ParagraphLineHeight.AtLeast(14),
            Borders = BoxBorders.Uniform(new BorderLine(1, 0xFF112233), 2),
            DropCap = new DropCap(3, 2),
            TabStops = [new TabStop(50) { Alignment = TabAlignment.Center }],
            Background = 0xFFEEEEEE,
        }, ["Standard", "Landscape"]);

        Assert.Equal(ParagraphProperties.None, tabs.Read(scope, Answers(tabs.Tabs(scope))));
    }

    [Fact]
    public void LaFinestraDelParagrafoScriveBordiCapoletteraEFlusso()
    {
        var tabs = Paragraphs();
        var scope = new ParagraphDialogScope(new TextParagraph("x") { Background = 0xFFEEEEEE }, ["Standard", "Landscape"]);
        var answers = Answers(tabs.Tabs(scope));
        answers["borders"] = PulseOffice.Document.Borders.BorderFrame.Empty.With(
            PulseOffice.Document.Borders.BorderPosition.Bottom,
            new PulseOffice.Document.Borders.BorderStroke(PulseOffice.Document.Borders.BorderDash.Double, 1.5, "#000000"));
        answers["border.padding"] = 3.0;
        answers["background"] = string.Empty;
        answers["drop"] = true;
        answers["drop.lines"] = 2.0;
        answers["pageStyle"] = 2;
        answers["together"] = true;
        answers["linePreset"] = 4;
        answers["lineMode"] = 2;
        answers["lineValue"] = 18.0;
        answers["tabs"] = new List<TabStop> { new(72) { Leader = TabLeader.Dots } };

        var change = tabs.Read(scope, answers);

        Assert.Equal(new BoxBorders { Bottom = new BorderLine(1.5, 0xFF000000) { Style = BorderLineStyle.Double }, PaddingBottom = 3 }, change.Borders);
        Assert.Equal(0x00FFFFFFu, change.Background);
        Assert.Equal(new DropCap(2, 1), change.DropCap);
        Assert.Equal("Landscape", change.PageStyleName);
        Assert.True(change.KeepTogether);
        Assert.Equal(ParagraphLineHeight.Exact(18), change.LineHeight);
        Assert.Equal(TabLeader.Dots, Assert.Single(change.TabStops!).Leader);
    }

    [Fact]
    public void LeTabulazioniRestanoOrdinateESenzaDoppioni()
    {
        var list = new TabStopList([new TabStop(100), new TabStop(20)]);

        Assert.True(list.Add(60));
        Assert.False(list.Add(60.001));
        list.Replace(0, new TabStop(150) { Alignment = TabAlignment.Right });

        Assert.Equal([60, 100, 150], list.Stops.Select(stop => stop.Position));
        Assert.Equal(TabAlignment.Right, list.Stops[^1].Alignment);
    }

    [Fact]
    public void LaFinestraDelParagrafoModificaLaDistanzaPredefinitaDelleTabulazioni()
    {
        var tabs = Paragraphs();
        var scope = new ParagraphDialogScope(new TextParagraph("x"), ["Standard"]) { DefaultTabInterval = StyleSheet.StandardTabInterval };
        var field = Assert.IsType<NumberField>(tabs.Tabs(scope)[FolioParagraphTabs.TabsTab].Fields.Single(item => item.Key == "defaultTabs"));
        Assert.Equal(1.25, field.Initial, 3);
        Assert.Equal("cm", field.Unit);

        var answers = Answers(tabs.Tabs(scope));
        Assert.Null(tabs.DefaultTabInterval(scope, answers));
        answers["defaultTabs"] = 2.0;
        Assert.Equal(2 * 72 / 2.54, tabs.DefaultTabInterval(scope, answers)!.Value, 3);
        Assert.Equal(ParagraphProperties.None, tabs.Read(scope, answers));

        var styleScope = new ParagraphDialogScope(new TextParagraph("x"), ["Standard"]);
        Assert.DoesNotContain(tabs.Tabs(styleScope).SelectMany(tab => tab.Fields), item => item.Key == "defaultTabs");
    }

    [Fact]
    public void LoStileSenzaLaSchedaFlussoNonPerdeTieniConIlSuccessivoNeSillabazione()
    {
        var tabs = Paragraphs();
        var scope = new ParagraphDialogScope(new TextParagraph("x")
        {
            KeepWithNext = true,
            KeepTogether = true,
            Hyphenation = ParagraphHyphenation.On,
            PageStyleName = "Landscape",
        }, ["Standard", "Landscape"]);
        var shown = tabs.Tabs(scope).Where(tab => tab.Fields.All(field => field.Key is not ("break" or "pageStyle" or "pageNumber"))).ToList();

        Assert.Equal(ParagraphProperties.None, tabs.Read(scope, Answers(shown)));
    }
}
