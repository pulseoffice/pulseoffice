using System.Globalization;
using PulseOffice.Documents.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class FieldLanguageTests
{
    private readonly Fixture _f = new();
    private readonly DateTime _now = new(2026, 3, 4, 14, 5, 0);

    [Theory]
    [InlineData("it-IT", "en-US", null, "3/4/2026")]
    [InlineData("en-US", "it-IT", null, "04/03/2026")]
    [InlineData("en-US", "en-US", "it-IT", "04/03/2026")]
    [InlineData("it-IT", "it-IT", "en-US", "3/4/2026")]
    public void TheDateFieldFollowsTheRunThenTheDocumentLanguage(string system, string document, string? run, string expected)
    {
        _f.Document.Language = document;
        var field = new TextRun("x") { Field = TextFieldKind.Date, Language = run };

        var value = WithCulture(system, () => new ClockFieldSource(new TextCultures()).Evaluate(field, Context()));

        Assert.Equal(expected, value!.Text);
    }

    [Theory]
    [InlineData("it-IT", "en-US", "Wednesday")]
    [InlineData("en-US", "it-IT", "mercoledì")]
    public void TheCreationDateFollowsTheDocumentLanguage(string system, string document, string expected)
    {
        _f.Document.Language = document;
        _f.Document.Properties.Created = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);
        var field = new TextRun("x") { Field = TextFieldKind.CreatedDate, FieldDetail = new FieldDetail { Format = "dddd" } };

        var value = WithCulture(system, () => new PropertyFieldSource(new TextCultures()).Evaluate(field, Context()));

        Assert.Equal(expected, value!.Text);
    }

    private FieldContext Context() => new(_f.Document, new FieldUpdate(new NoFieldLayoutFacts(), _now), new Dictionary<string, int>());

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
