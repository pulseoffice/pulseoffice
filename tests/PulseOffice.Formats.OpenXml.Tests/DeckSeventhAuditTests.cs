using System.IO.Compression;
using System.Text;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckSeventhAuditTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        return deck;
    }

    private byte[] Odp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private string Entry(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Il_grafico_di_Deck_si_scrive_alla_dimensione_del_riquadro()
    {
        var deck = Deck();
        deck.Slides[0].Add(new ObjectElement
        {
            Id = "g1",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(20), Twip.FromCentimeters(10)),
            Content = new ChartDefinition
            {
                Id = "c1",
                Type = ChartType.Column,
                SourceSheet = string.Empty,
                SourceRange = string.Empty,
                InlineData = new ChartData(["Gen", "Feb"], [new ChartSeries("Vendite", [12, 30])]),
            },
        });

        var chart = Entry(Odp(deck), "Object 1/content.xml");

        Assert.Contains("svg:width=\"20.001cm\"", chart);
        Assert.Contains("svg:height=\"9.999cm\"", chart);
    }

    private byte[] Pptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private Presentation ReadPptx(byte[] bytes)
    {
        var result = new PptxReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation ReadOdp(byte[] bytes)
    {
        var result = new OdpReader(_themes, _masters).Read(new MemoryStream(bytes));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation WithFields(params (TextFieldKind Kind, string? Format)[] fields)
    {
        var deck = Deck();
        deck.Slides[0].Add(new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(4)),
            Text = new RichText(PlaceholderRole.Free,
                [.. fields.Select(field => new TextParagraph("x").WithRuns(
                    [new TextRun("x") { Field = field.Kind, FieldDetail = field.Format is null ? null : new FieldDetail { Format = field.Format } }]))]),
        });
        return deck;
    }

    private IReadOnlyList<string?> Formats(Presentation deck) =>
        [.. deck.Slides[0].Elements.OfType<TextElement>().SelectMany(element => element.Text.Paragraphs).SelectMany(p => p.Runs)
            .Where(run => run.Field is TextFieldKind.Date or TextFieldKind.Time).Select(run => run.FieldDetail?.Format)];

    [Fact]
    public void Il_formato_delle_date_dei_campi_fa_il_giro_in_ODP()
    {
        string?[] formats = ["D", "dd'.'MM'.'yy", "d'. 'MMMM' 'yyyy", "dddd', 'd'. 'MMMM' 'yyyy", null];
        string?[] times = ["T", "H':'mm", "h':'mm tt", "H':'mm':'ss.ff"];
        var deck = WithFields([.. formats.Select(f => (TextFieldKind.Date, f)), .. times.Select(f => (TextFieldKind.Time, f))]);
        var odp = Odp(deck);

        Assert.Equal([.. formats, .. times], Formats(ReadOdp(odp)));
        var content = Entry(odp, "content.xml");
        Assert.Contains("<number:date-style style:name=", content);
        Assert.Contains("number:decimal-places=\"2\"", content);
        Assert.Contains("<number:am-pm />", content);
    }

    [Fact]
    public void I_formati_di_PowerPoint_fanno_il_giro_e_gli_altri_si_dichiarano()
    {
        string?[] exact = ["D", "d' 'MMMM' 'yyyy", "MMM'-'yy", "MM'/'dd'/'yyyy' 'h':'mm':'ss tt", null];
        var deck = WithFields([.. exact.Select(f => (TextFieldKind.Date, f)), (TextFieldKind.Time, "T"), (TextFieldKind.Time, "h':'mm tt")]);

        Assert.Equal([.. exact, "T", "h':'mm tt"], Formats(ReadPptx(Pptx(deck))));
        Assert.Equal([.. exact, "T", "h':'mm tt"], Formats(ReadOdp(Odp(ReadPptx(Pptx(deck))))));
        Assert.DoesNotContain("PPTX037", new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code));

        var lossy = WithFields((TextFieldKind.Date, "dd'.'MM'.'yyyy"));
        Assert.Contains("PPTX037", new PptxWriter().CheckFidelity(lossy).Losses.Select(loss => loss.Code));
        Assert.Equal([null], Formats(ReadPptx(Pptx(lossy))));
    }

    [Fact]
    public void Il_campo_si_mostra_nel_formato_conservato()
    {
        var resolver = new TextFieldResolver();
        var fields = new TextFields(1, new DateTime(2021, 6, 29, 22, 4, 52));
        var run = new TextRun("x") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Format = "dd'.'MM'.'yyyy" } };

        Assert.Equal("29.06.2021", resolver.Resolve(run, fields));
        Assert.Equal("22:04", resolver.Resolve(run with { Field = TextFieldKind.Time, FieldDetail = new FieldDetail { Format = "H':'mm" } }, fields));
    }

    [Fact]
    public void L_etichetta_degli_elenchi_conserva_le_misure_reali_in_ODP()
    {
        var points = 72 / 2.54;
        var deck = Deck();
        var symbol = new BulletSymbol("✗") { Font = "DejaVu Sans", RelativeSize = 0.45 };
        deck.Slides[0].Add(new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(20), Twip.FromCentimeters(8)),
            Text = new RichText(PlaceholderRole.Free,
            [
                new TextParagraph("uno") { Bullet = true, BulletSymbol = symbol, MarginLeft = 1.2 * points, Indent = -0.9 * points },
                new TextParagraph("due") { Bullet = true, BulletSymbol = symbol, BulletLevel = 1, MarginLeft = 2.4 * points, Indent = -0.9 * points },
            ]),
        });

        var odp = Odp(deck);
        var content = Entry(odp, "content.xml");
        Assert.Contains("text:space-before=\"0.3cm\" text:min-label-width=\"0.9cm\"", content);
        Assert.Contains("text:space-before=\"1.5cm\" text:min-label-width=\"0.9cm\"", content);
        Assert.DoesNotContain("fo:text-indent", content);

        var paragraphs = ReadOdp(odp).Slides[0].Elements.OfType<TextElement>().Single().Text.Paragraphs;
        Assert.Equal(2.4, paragraphs[1].MarginLeft!.Value / points, 2);
        Assert.Equal(-0.9, paragraphs[1].Indent!.Value / points, 2);
        Assert.Equal(1.2, paragraphs[0].MarginLeft!.Value / points, 2);
    }
}
