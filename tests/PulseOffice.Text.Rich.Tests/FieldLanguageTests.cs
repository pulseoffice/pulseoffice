using System.Globalization;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class FieldLanguageTests
{
    private readonly DateTime _moment = new(2026, 3, 4, 14, 5, 0);

    [Theory]
    [InlineData("it-IT", "en-US", "3/4/2026")]
    [InlineData("en-US", "it-IT", "04/03/2026")]
    public void ADateFieldFollowsTheRunLanguageNotTheSystemCulture(string system, string language, string expected)
    {
        var shown = WithCulture(system, () => new TextFieldResolver().Resolve(
            new TextRun("x") { Field = TextFieldKind.Date, Language = language }, new TextFields(1, _moment)));

        Assert.Equal(expected, shown);
    }

    [Theory]
    [InlineData("it-IT", "en-US", "March 2026")]
    [InlineData("en-US", "it-IT", "marzo 2026")]
    public void WithoutARunLanguageTheDocumentLanguageApplies(string system, string language, string expected)
    {
        var shown = WithCulture(system, () => new TextFieldResolver().Resolve(
            new TextRun("x") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Format = "MMMM yyyy" } },
            new TextFields(1, _moment, Language: language)));

        Assert.Equal(expected, shown);
    }

    [Fact]
    public void TheRunLanguageWinsOverTheDocumentLanguage()
    {
        var shown = WithCulture("en-US", () => new TextFieldResolver().Resolve(
            new TextRun("x") { Field = TextFieldKind.Date, Language = "de-DE", FieldDetail = new FieldDetail { Format = "MMMM" } },
            new TextFields(1, _moment, Language: "it-IT")));

        Assert.Equal("März", shown);
    }

    [Theory]
    [InlineData("zxx")]
    [InlineData("not a language")]
    [InlineData("")]
    public void AnUnusableRunLanguageFallsBackToTheDocumentLanguage(string language)
    {
        var culture = new TextCultures(CultureInfo.InvariantCulture).For(language, "it-IT");

        Assert.Equal("it-IT", culture.Name);
    }

    [Fact]
    public void WithNoLanguageAtAllTheInjectedFallbackApplies()
    {
        var fallback = CultureInfo.GetCultureInfo("fr-FR");

        Assert.Same(fallback, new TextCultures(fallback).For(null, "", "zxx"));
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
