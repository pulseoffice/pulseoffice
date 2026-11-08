using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Packaging;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpHeaderFooterDateFormatTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Sample(HeaderFooterDateFormat format)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        deck.HeaderFooter = new HeaderFooterSettings { ShowDate = true, DateFormat = format, ShowOnTitleSlide = true };
        return deck;
    }

    private byte[] Write(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private OperationResult<Presentation> Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new OdpReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded);
        return result;
    }

    private byte[] EditContent(byte[] bytes, Action<XDocument> edit)
    {
        using var buffer = new MemoryStream();
        buffer.Write(bytes);
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var reading = entry.Open()) content = XDocument.Load(reading);
            edit(content);
            entry.Delete();
            using var writing = archive.CreateEntry("content.xml").Open();
            content.Save(writing);
        }
        return buffer.ToArray();
    }

    private void DropExtension(XDocument content) =>
        content.Descendants(XNamespace.Get(HeaderFooterXml.Namespace) + "hf").ToList().ForEach(element => element.Remove());

    [Theory]
    [InlineData(HeaderFooterDateFormat.LongDate)]
    [InlineData(HeaderFooterDateFormat.DayShortMonthShortYear)]
    [InlineData(HeaderFooterDateFormat.Time12WithSeconds)]
    public void IlFormatoDellaDataAttraversaODP(HeaderFooterDateFormat format)
    {
        Assert.Equal(format, Read(Write(Sample(format))).Value!.HeaderFooter.DateFormat);
    }

    [Theory]
    [InlineData(HeaderFooterDateFormat.ShortDate)]
    [InlineData(HeaderFooterDateFormat.LongDate)]
    [InlineData(HeaderFooterDateFormat.DayMonthYear)]
    [InlineData(HeaderFooterDateFormat.MonthDayYear)]
    [InlineData(HeaderFooterDateFormat.DayShortMonthShortYear)]
    [InlineData(HeaderFooterDateFormat.MonthShortYear)]
    [InlineData(HeaderFooterDateFormat.ShortMonthShortYear)]
    [InlineData(HeaderFooterDateFormat.DateAndTime)]
    [InlineData(HeaderFooterDateFormat.DateAndTimeWithSeconds)]
    [InlineData(HeaderFooterDateFormat.Time24)]
    [InlineData(HeaderFooterDateFormat.Time24WithSeconds)]
    [InlineData(HeaderFooterDateFormat.Time12)]
    [InlineData(HeaderFooterDateFormat.Time12WithSeconds)]
    public void IlFormatoSiRileggeDalloStileDellaDataSenzaLaNostraEstensione(HeaderFooterDateFormat format)
    {
        var read = Read(EditContent(Write(Sample(format)), DropExtension));

        Assert.Equal(format, read.Value!.HeaderFooter.DateFormat);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODP215");
    }

    [Fact]
    public void LoStileDellaDataSiScriveNelDocumento()
    {
        var bytes = Write(Sample(HeaderFooterDateFormat.MonthShortYear));
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var stream = archive.GetEntry("content.xml")!.Open();
        var content = XDocument.Load(stream);
        var number = OdfPackage.Namespaces.Number;

        var decl = content.Descendants(OdfPackage.Namespaces.Presentation + "date-time-decl").Single();
        var name = (string?)decl.Attribute(OdfPackage.Namespaces.Style + "data-style-name");
        var style = content.Descendants(number + "date-style").Single(element => (string?)element.Attribute(OdfPackage.Namespaces.Style + "name") == name);
        Assert.Equal("true", (string?)style.Element(number + "month")!.Attribute(number + "textual"));
    }

    [Fact]
    public void UnoStileDellaDataSconosciutoPrendeIlPiuVicinoEDichiaraLaPerdita()
    {
        var number = OdfPackage.Namespaces.Number;
        var read = Read(EditContent(Write(Sample(HeaderFooterDateFormat.ShortDate)), content =>
        {
            DropExtension(content);
            var style = content.Descendants(number + "date-style").Single();
            style.RemoveNodes();
            style.Add(new XElement(number + "year", new XAttribute(number + "style", "long")),
                new XElement(number + "text", "."),
                new XElement(number + "quarter"),
                new XElement(number + "text", " "),
                new XElement(number + "hours"));
        }));

        Assert.Equal(HeaderFooterDateFormat.DateAndTime, read.Value!.HeaderFooter.DateFormat);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODP215");
    }
}
