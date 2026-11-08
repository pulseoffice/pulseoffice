using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Text.Rich;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ShapeListNumberingTests
{
    [Fact]
    public void Schemi_e_inizio_della_numerazione_di_una_forma_attraversano_XLSX_e_ODS()
    {
        var workbook = Workbook(new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno\nDue\nTre",
            Lists =
            [
                new ShapeListItem { Numbered = true, NumberFormat = new ListNumberFormat { Style = ListNumberStyle.LowerLetter, Suffix = ")", Start = 3 } },
                new ShapeListItem { Numbered = true, Level = 1, NumberFormat = new ListNumberFormat { Style = ListNumberStyle.UpperRoman, Prefix = "(", Suffix = ")" } },
                new ShapeListItem { Numbered = true, NumberFormat = new ListNumberFormat { Style = ListNumberStyle.LowerLetter, Suffix = ")", Start = 3 } },
            ],
        });

        var crossed = RoundTripXlsx(Workbook(RoundTripOds(Workbook(RoundTripXlsx(workbook)))));

        foreach (var shape in new[] { RoundTripXlsx(workbook), RoundTripOds(workbook), crossed })
        {
            Assert.Equal(new ListNumberFormat { Style = ListNumberStyle.LowerLetter, Suffix = ")", Start = 3 }, shape.Lists![0]!.NumberFormat);
            Assert.Equal(new ListNumberFormat { Style = ListNumberStyle.UpperRoman, Prefix = "(", Suffix = ")" }, shape.Lists[1]!.NumberFormat);
        }
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX184");
    }

    [Fact]
    public void Lo_schema_XLSX_e_l_inizio_si_leggono_e_si_riscrivono()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook(new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno\nDue",
            Lists = [new ShapeListItem { Numbered = true }, new ShapeListItem { Numbered = true }],
        }), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var drawing = document.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!;
            var bullets = drawing.Descendants<A.AutoNumberedBullet>().ToList();
            bullets[0].Type = A.TextAutoNumberSchemeValues.RomanUpperCharacterPeriod;
            bullets[0].StartAt = 4;
            bullets[1].Type = new A.TextAutoNumberSchemeValues("circleNumDbPlain");
            bullets[1].SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute("startAt", string.Empty, "tanti"));
            drawing.Save();
        }

        var shape = Read(stream.ToArray());

        Assert.Equal(new ListNumberFormat { Style = ListNumberStyle.UpperRoman, Start = 4 }, shape.Lists![0]!.NumberFormat);
        Assert.Equal("circleNumDbPlain", shape.Lists[1]!.NumberFormat!.DrawingScheme);
        var back = RoundTripXlsx(Workbook(RoundTripOds(Workbook(shape))));
        Assert.Equal(shape.Lists[0]!.NumberFormat, back.Lists![0]!.NumberFormat);
        Assert.Equal("circleNumDbPlain", back.Lists[1]!.NumberFormat!.DrawingScheme);
    }

    [Fact]
    public void Affissi_che_DrawingML_non_ha_si_dichiarano_in_XLSX_e_PPTX()
    {
        var odd = new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno",
            Lists = [new ShapeListItem { Numbered = true, NumberFormat = new ListNumberFormat { Prefix = "§", Suffix = "°" } }],
        };

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(Workbook(odd)).Losses, loss => loss.Code == "XLSX184");
        var deck = Deck(new TextParagraph("Uno") { Numbered = true, NumberFormat = new ListNumberFormat { Suffix = "-" } });
        Assert.Contains(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code == "PPTX222");
        Assert.Equal(new ListNumberFormat { Prefix = "§", Suffix = "°" }, RoundTripOds(Workbook(odd)).Lists![0]!.NumberFormat);
    }

    [Fact]
    public void Un_a_capo_nel_testo_di_una_forma_resta_nel_paragrafo_in_XLSX_e_ODS()
    {
        var workbook = Workbook(new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno\u2028dopo\nDue",
            Lists = [new ShapeListItem { Numbered = true }, new ShapeListItem { Numbered = true }],
        });
        var plain = Workbook(new ShapeObject { Id = "Forma", Shape = ShapeKind.Rectangle, Text = "Riga\u2028seguito" });

        foreach (var shape in new[] { RoundTripXlsx(workbook), RoundTripOds(workbook), RoundTripXlsx(Workbook(RoundTripOds(workbook))) })
        {
            Assert.Equal("Uno\u2028dopo\nDue", shape.Text);
            Assert.Equal(2, shape.Lists!.Count(item => item is { Numbered: true }));
        }
        Assert.Equal("Riga\u2028seguito", RoundTripXlsx(plain).Text);
        Assert.Equal("Riga\u2028seguito", RoundTripOds(plain).Text);
    }

    [Fact]
    public void La_numerazione_di_un_testo_Deck_attraversa_PPTX_e_ODP_e_un_livello_non_numerico_non_rompe()
    {
        var wanted = new ListNumberFormat { Style = ListNumberStyle.UpperLetter, Suffix = ")", Start = 2 };
        var deck = Deck(new TextParagraph("Uno") { Numbered = true, BulletLevel = 1, NumberFormat = wanted });
        using var stream = new MemoryStream();
        new PptxWriter().Write(deck, stream);
        var clean = ReadPptx(stream.ToArray());
        Assert.Equal(wanted, Paragraph(clean).NumberFormat);
        Assert.Equal(wanted, Paragraph(ReadOdp(Odp(clean))).NumberFormat);
        Assert.Equal(wanted, Paragraph(ReadPptx(Pptx(ReadOdp(Odp(deck))))).NumberFormat);

        using (var document = PresentationDocument.Open(stream, true))
        {
            var slide = document.PresentationPart!.SlideParts.Single();
            foreach (var properties in slide.Slide!.Descendants<A.ParagraphProperties>())
                properties.SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute("lvl", string.Empty, "molto"));
            slide.Slide.Save();
        }

        var read = ReadPptx(stream.ToArray());

        Assert.Equal(0, Paragraph(read).BulletLevel);
        Assert.Equal(wanted, Paragraph(read).NumberFormat);
    }

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(TextParagraph paragraph)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[0].Add(new TextElement
        {
            Id = "box",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(PulseOffice.Foundation.Units.Twip.FromCentimeters(1), PulseOffice.Foundation.Units.Twip.FromCentimeters(1), PulseOffice.Foundation.Units.Twip.FromCentimeters(10), PulseOffice.Foundation.Units.Twip.FromCentimeters(4)),
            Text = new RichText(PlaceholderRole.Free, [paragraph]),
        });
        return deck;
    }

    private TextParagraph Paragraph(Presentation deck) => deck.Slides[0].Elements.OfType<TextElement>().Single().Text.Paragraphs[0];

    private byte[] Odp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
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

    private ShapeObject Read(byte[] data)
    {
        var read = new XlsxReader().Read(new MemoryStream(data));
        return Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
    }

    private ShapeObject RoundTripXlsx(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        return Read(stream.ToArray());
    }

    private ShapeObject RoundTripOds(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        var read = new OdsReader().Read(new MemoryStream(stream.ToArray()));
        return Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
    }

    private Workbook Workbook(ShapeObject content)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "Forma",
            Anchor = new PulseOffice.Sheets.Addressing.CellAddress(1, 1),
            Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Content = content,
        });
        return workbook;
    }
}
