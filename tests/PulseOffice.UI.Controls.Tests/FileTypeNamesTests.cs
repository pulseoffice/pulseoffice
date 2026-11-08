using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class FileTypeNamesTests
{
    [Theory]
    [InlineData(UiLanguage.Italian, ".odt", "Documento di testo OpenDocument (.odt)")]
    [InlineData(UiLanguage.Italian, ".docx", "Documento Word (.docx)")]
    [InlineData(UiLanguage.Italian, ".xltx", "Modello di Excel (.xltx)")]
    [InlineData(UiLanguage.Italian, ".potx", "Modello di PowerPoint (.potx)")]
    [InlineData(UiLanguage.German, ".xlsx", "Excel-Arbeitsmappe (.xlsx)")]
    [InlineData(UiLanguage.French, "pdf", "Document PDF (.pdf)")]
    [InlineData(UiLanguage.Spanish, "*.pptx", "Presentación de PowerPoint (.pptx)")]
    public void IlNomeDelTipoSegueLaLingua(UiLanguage language, string extension, string expected)
    {
        var names = new FileTypeNames(new TextCatalog(language));

        Assert.Equal(expected, names.Localize(new FileFilter("English name", [extension])).Name);
    }

    [Fact]
    public void UnaFamigliaDiEstensioniPrendeIlNomeDellaPrima()
    {
        var names = new FileTypeNames(new TextCatalog(UiLanguage.Italian));

        var filter = names.Localize(new FileFilter("OpenDocument Presentation", [".odp", ".fodp", ".otp", ".sxi", ".sti"]));

        Assert.StartsWith("Presentazione OpenDocument (.odp, .fodp", filter.Name);
    }

    [Fact]
    public void UnFiltroMistoConservaIlSuoNome()
    {
        var names = new FileTypeNames(new TextCatalog(UiLanguage.Italian));

        var filter = names.Localize(new FileFilter("Immagine", [".png", ".jpg", ".gif"]));

        Assert.Equal("Immagine", filter.Name);
    }

    [Fact]
    public void OgniLinguaHaTuttiITipi()
    {
        var extensions = new[] { "odt", "ott", "fodt", "ods", "ots", "fods", "odp", "otp", "fodp", "docx", "dotx", "docm", "xlsx", "xltx", "pptx", "potx", "rtf", "txt", "tsv", "csv", "html", "pdf", "png", "mp4" };

        foreach (var language in new[] { UiLanguage.Italian, UiLanguage.English, UiLanguage.German, UiLanguage.Spanish, UiLanguage.French })
        {
            var texts = new TextCatalog(language);
            foreach (var extension in extensions)
                Assert.True(texts.Contains("fileType." + extension), $"{language} fileType.{extension}");
        }
    }

    [Theory]
    [InlineData(28.0, 30.0, 21.0)]
    [InlineData(80.0, 30.0, 30.0)]
    [InlineData(4.0, 30.0, 8.0)]
    public void IlCorpoDelCampioneStaNelloSpazio(double points, double available, double expected)
    {
        Assert.Equal(expected, new SampleTypeScale().Fit(points, 12, available), 3);
    }

    [Fact]
    public void Il_nome_dello_stile_nella_casella_non_scende_sotto_il_corpo_dell_interfaccia()
    {
        var scale = new SampleTypeScale();
        Assert.Equal(12, scale.FitLabel(12, 12, 20), 3);
        Assert.Equal(20, scale.FitLabel(28, 12, 20), 3);
        Assert.Equal(13.5, scale.FitLabel(18, 12, 20), 3);
    }
}
