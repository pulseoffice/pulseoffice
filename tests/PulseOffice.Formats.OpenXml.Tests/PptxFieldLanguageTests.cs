using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxFieldLanguageTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public void TheCachedDateFooterIsWrittenInTheDeckLanguage(string system, string language)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        deck.Properties.Language = language;
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        deck.HeaderFooter = new HeaderFooterSettings { ShowDate = true, DateFormat = HeaderFooterDateFormat.MonthShortYear, ShowOnTitleSlide = true };

        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        string cached;
        var before = DateTime.Now;
        try
        {
            using var buffer = new MemoryStream();
            Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
            buffer.Position = 0;
            using var package = PresentationDocument.Open(buffer, false);
            cached = package.PresentationPart!.SlideParts.Single().Slide!.Descendants<D.Field>().Single(field => field.Type?.Value == "datetime6").InnerText;
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }

        var culture = CultureInfo.GetCultureInfo(language);
        Assert.Contains(cached, new[] { before.ToString("MMMM yy", culture), DateTime.Now.ToString("MMMM yy", culture) });
    }
}
