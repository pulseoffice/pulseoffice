using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditRound62LocaleTests
{
    private sealed class PlainTheme : ITextTheme
    {
        public uint Resolve(ColorRole role) => 0xFF000000;

        public string MajorFont => "Inter";

        public string MinorFont => "Inter";
    }

    private sealed class CapturingPlot : ChartPlotBase
    {
        public CultureInfo? Culture { get; private set; }

        public override bool Handles(ChartType type) => true;

        public override void Draw(ChartFrame frame) => Culture = frame.Formatting;
    }

    private static void WithSystem(string system, Action body)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static CultureInfo? CultureOf(object? value, int depth = 3)
    {
        if (value is null || depth < 0) return null;
        if (value is CultureInfo culture) return culture;
        if (value is string || value.GetType().IsPrimitive) return null;
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (!field.FieldType.IsPrimitive && CultureOf(field.GetValue(value), depth - 1) is { } found) return found;
        return null;
    }

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task ClipboardChartPicturesAreDrawnInTheSourceLanguage(string system, string language) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var plot = new CapturingPlot();
        var picture = new EmbeddedObjectPicture([new ChartPicturePainter(new ChartRenderer([plot]))]);
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty, InlineData = new ChartData(["A"], [new ChartSeries("S", [1.5])]) };
        WithSystem(system, () => Assert.NotNull(picture.Png(chart, new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)), CultureInfo.GetCultureInfo(language))));
        Assert.Equal(language, plot.Culture?.Name);
    }, CancellationToken.None);

    [Theory]
    [InlineData("it-IT", "en-US", null, "en-US")]
    [InlineData("en-US", "it-IT", null, "it-IT")]
    [InlineData("en-US", null, "it-IT", "it-IT")]
    public Task RichTextIsShapedInTheRunLanguage(string system, string? runLanguage, string? fieldsLanguage, string expected) => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var run = new TextRun("testo") { Language = runLanguage };
        var paragraph = new TextParagraph("testo") { Runs = [run] };
        CultureInfo? culture = null;
        WithSystem(system, () =>
        {
            var frame = new TextFrameComposer(new ListLabels()).Compose(
                new RichText(PlaceholderRole.Body, [paragraph]), new Rect(0, 0, 20000, 8000), new PlainTheme(),
                new TextFields(1, DateTime.Now, Language: fieldsLanguage), 1.0);
            culture = CultureOf(frame.Lines[0].Segments[0].Text);
        });
        Assert.Equal(expected, culture?.Name);
    }, CancellationToken.None);
}
