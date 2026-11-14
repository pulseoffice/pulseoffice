using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Localization;
using PulseOffice.Documents.Editing.Borders;
using PulseOffice.UI.Controls.Pages;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioPageDialogTests
{
    private FolioPageTabs Tabs() =>
        new(new FolioPageServices(null!, null!, null!, null!, null!, new BoxBorderFrames(), null!, null!, null!, new TextCatalog(UiLanguage.Italian)));

    [Fact]
    public void MarginiPiuLarghiDellaPaginaLascianoSempreUnCorpoDiTesto()
    {
        var style = new PageStyle("Default");
        var values = new Dictionary<string, object>
        {
            ["size"] = 0,
            ["orientation"] = 0,
            ["margins"] = new PageMargins(20, 20, 15, 15),
        };

        Tabs().Write(style, [], values);

        var body = style.Width.ToCentimeters() - style.Margins.Left.ToCentimeters() - style.Margins.Right.ToCentimeters();
        var tall = style.Height.ToCentimeters() - style.Margins.Top.ToCentimeters() - style.Margins.Bottom.ToCentimeters();
        Assert.True(body >= 0.99, $"body width {body} cm");
        Assert.True(tall >= 0.99, $"body height {tall} cm");
        Assert.Equal(style.Margins.Left, style.Margins.Right);
    }

    [Fact]
    public void MarginiNormaliRestanoComeScelti()
    {
        var style = new PageStyle("Default");
        var values = new Dictionary<string, object>
        {
            ["size"] = 0,
            ["orientation"] = 0,
            ["margins"] = new PageMargins(2, 2.5, 3, 1.5),
        };

        Tabs().Write(style, [], values);

        Assert.Equal((2, 2.5, 3, 1.5), (Math.Round(style.Margins.Top.ToCentimeters(), 2), Math.Round(style.Margins.Bottom.ToCentimeters(), 2), Math.Round(style.Margins.Left.ToCentimeters(), 2), Math.Round(style.Margins.Right.ToCentimeters(), 2)));
    }
}
