using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxHeaderFooterDateFormatTests
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
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private Presentation Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new PptxReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private byte[] WithoutExtension(byte[] bytes)
    {
        using var buffer = new MemoryStream();
        buffer.Write(bytes);
        using (var document = PresentationDocument.Open(buffer, true))
        {
            var presentation = document.PresentationPart!.Presentation!;
            foreach (var extension in presentation.Descendants<P.PresentationExtension>().Where(ext => ext.Uri == "{QO-HEADERFOOTER}").ToList())
                extension.Remove();
            presentation.Save();
        }
        return buffer.ToArray();
    }

    [Theory]
    [InlineData(HeaderFooterDateFormat.ShortDate, "datetime1")]
    [InlineData(HeaderFooterDateFormat.MonthDayYear, "datetime4")]
    [InlineData(HeaderFooterDateFormat.Time12WithSeconds, "datetime13")]
    public void IlCampoDataPortaIlTipoDiPowerPoint(HeaderFooterDateFormat format, string type)
    {
        using var document = PresentationDocument.Open(new MemoryStream(Write(Sample(format))), false);
        var field = document.PresentationPart!.SlideParts.SelectMany(part => part.Slide!.Descendants<D.Field>())
            .Single(candidate => candidate.Type?.Value?.StartsWith("datetime", StringComparison.Ordinal) == true);

        Assert.Equal(type, field.Type!.Value);
    }

    [Theory]
    [InlineData(HeaderFooterDateFormat.LongDate)]
    [InlineData(HeaderFooterDateFormat.ShortMonthShortYear)]
    [InlineData(HeaderFooterDateFormat.Time24WithSeconds)]
    public void IlFormatoDellaDataAttraversaPPTX(HeaderFooterDateFormat format)
    {
        var bytes = Write(Sample(format));

        Assert.Equal(format, Read(bytes).HeaderFooter.DateFormat);
        Assert.Equal(format, Read(WithoutExtension(bytes)).HeaderFooter.DateFormat);
    }
}
