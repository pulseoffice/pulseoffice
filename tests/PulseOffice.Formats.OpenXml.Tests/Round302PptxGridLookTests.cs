using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round302PptxGridLookTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void A_grid_table_carries_its_header_tint_and_bands_in_a_table_style_and_reads_back_as_a_grid()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var style = new DataTableStyle { AccentHex = "#2563EB" };
        slide.Add(new ObjectElement
        {
            Id = "t1",
            Bounds = new DocumentRect(new Twip(500), new Twip(500), new Twip(6000), new Twip(3000)),
            Content = new DataTableObject
            {
                Id = "t1",
                Query = new DataQuerySource { ConnectionId = "", Sql = "" },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("A", DataValueKind.Text)], Rows = [["x"], ["y"]] },
                Style = style,
                CellTexts = [new TableCellText(0, 0, [new TableCellParagraph([new TableTextRun("x") { Bold = true }])])],
            },
        });

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);

        buffer.Position = 0;
        using (var package = PresentationDocument.Open(buffer, false))
        {
            var entry = Assert.Single(package.PresentationPart!.TableStylesPart!.TableStyleList!.Elements<D.TableStyleEntry>());
            var look = new TableGridLook();
            Assert.Equal(look.HeaderFill(style).TrimStart('#'), entry.GetFirstChild<D.FirstRow>()!.Descendants<D.RgbColorModelHex>().Last().Val!.Value);
            Assert.Equal(look.BandFill.TrimStart('#'), entry.GetFirstChild<D.Band2Horizontal>()!.Descendants<D.RgbColorModelHex>().Single().Val!.Value);
        }

        buffer.Position = 0;
        var table = new PptxReader(_themes, _masters).Read(buffer).Value!.Slides[0].Elements.OfType<ObjectElement>()
            .Select(o => o.Content).OfType<DataTableObject>().Single();
        Assert.True(table.Style.Grid);
        Assert.False(table.Style.OwnLook);
        Assert.Equal("#2563EB", table.Style.AccentHex);
        Assert.True(Assert.Single(table.CellTexts).Paragraphs[0].Runs[0].Bold);
    }
}
