using System.Globalization;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class FieldLanguageTests
{
    private readonly DateTime _when = new(2026, 3, 4, 14, 5, 0);

    [Theory]
    [InlineData("it-IT", "en-US", "March 4, 2026")]
    [InlineData("en-US", "it-IT", "4 marzo 2026")]
    public void TheDateFooterFollowsTheDeckLanguage(string system, string language, string expected)
    {
        var settings = new HeaderFooterSettings { ShowDate = true, DateFormat = HeaderFooterDateFormat.MonthDayYear };
        if (language == "it-IT") settings = settings with { DateFormat = HeaderFooterDateFormat.DayMonthYear };

        var chrome = WithCulture(system, () => new HeaderFooterResolver().Resolve(
            settings, new Slide("s1", SlideLayout.TitleAndContent), 1, _when, language: language));

        Assert.Equal(expected, chrome.Date);
    }

    [Theory]
    [InlineData("it-IT", "en-US", "3/4/2026")]
    [InlineData("en-US", "it-IT", "04/03/2026")]
    public void ASlideDateFieldFollowsTheDeckLanguage(string system, string language, string expected)
    {
        var deck = new Presentation();
        deck.Properties.Language = language;
        deck.AddExisting(new Slide("s1", SlideLayout.Blank));
        var fields = new SlideFieldsFactory().For(deck, deck.Slides[0]) with { Now = _when };

        var shown = WithCulture(system, () => new TextFieldResolver().Resolve(new TextRun("x") { Field = TextFieldKind.Date }, fields));

        Assert.Equal(expected, shown);
    }

    [Theory]
    [InlineData("it-IT", "en-US", "3/4/2026")]
    [InlineData("en-US", "it-IT", "04/03/2026")]
    public void WithoutADeclaredLanguageTheDeckDefaultTextLanguageApplies(string system, string language, string expected)
    {
        var deck = new Presentation { DefaultLanguages = new TextLanguages(language) };
        deck.AddExisting(new Slide("s1", SlideLayout.Blank));
        var fields = new SlideFieldsFactory().For(deck, deck.Slides[0]) with { Now = _when };

        var shown = WithCulture(system, () => new TextFieldResolver().Resolve(new TextRun("x") { Field = TextFieldKind.Date }, fields));

        Assert.Equal(expected, shown);
    }

    private T WithCulture<T>(string name, Func<T> action)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
