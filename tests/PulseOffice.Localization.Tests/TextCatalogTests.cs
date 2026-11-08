using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Localization.Tests;

public class TextCatalogTests
{
    [Theory]
    [InlineData(UiLanguage.Italian)]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.Spanish)]
    [InlineData(UiLanguage.French)]
    public void OgniLinguaTraduceEsattamenteLeStesseChiavi(UiLanguage language)
    {
        var catalog = new TextCatalog(UiLanguage.Italian);

        var italian = catalog.KeysIn(UiLanguage.Italian).ToHashSet(StringComparer.Ordinal);
        var other = catalog.KeysIn(language).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(italian.Except(other));
        Assert.Empty(other.Except(italian));
    }

    [Theory]
    [InlineData(UiLanguage.Italian)]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.Spanish)]
    [InlineData(UiLanguage.French)]
    public void OgniLinguaHaGliStessiSegnaposto(UiLanguage language)
    {
        var english = new TextCatalog(UiLanguage.English);
        var other = new TextCatalog(language);
        var pattern = new System.Text.RegularExpressions.Regex(@"\{\d+[^}]*\}");
        string Signature(string text) =>
            string.Join(",", pattern.Matches(text).Select(match => match.Value).Distinct().Order(StringComparer.Ordinal));

        var mismatches = english.KeysIn(UiLanguage.English)
            .Where(key => Signature(english[key]) != Signature(other[key]))
            .ToList();

        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    public void NessunaChiaveECompareDueVolte(string code)
    {
        var assembly = typeof(TextCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream($"PulseOffice.Localization.Resources.{code}.json");
        Assert.NotNull(stream);

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var reader = new System.Text.Json.Utf8JsonReader(buffer.ToArray());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        while (reader.Read())
            if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName && reader.CurrentDepth == 1
                && !seen.Add(reader.GetString()!))
                duplicates.Add(reader.GetString()!);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void IlTestoCambiaConLaLingua()
    {
        var catalog = new TextCatalog(UiLanguage.Italian);
        Assert.Equal("Copia", catalog["command.ledger.clipboard.copy.title"]);

        catalog.Use(UiLanguage.German);
        Assert.Equal("Kopieren", catalog["command.ledger.clipboard.copy.title"]);

        catalog.Use(UiLanguage.French);
        Assert.Equal("Copier", catalog["command.ledger.clipboard.copy.title"]);
    }

    [Fact]
    public void UnaChiaveMancanteSiVede()
    {
        var catalog = new TextCatalog(UiLanguage.Italian);

        Assert.Equal("⟦non.esiste⟧", catalog["non.esiste"]);
        Assert.False(catalog.Contains("non.esiste"));
    }

    [Fact]
    public void IlCambioDiLinguaVieneSegnalato()
    {
        var catalog = new TextCatalog(UiLanguage.Italian);
        var notified = 0;
        catalog.LanguageChanged += (_, _) => notified++;

        catalog.Use(UiLanguage.Spanish);

        Assert.Equal(1, notified);
        Assert.Equal(UiLanguage.Spanish, catalog.Language);
    }

    [Fact]
    public void LaLinguaDiSistemaVieneSciolta()
    {
        var catalog = new TextCatalog(UiLanguage.System);

        Assert.NotEqual(UiLanguage.System, catalog.Language);
    }

    [Fact]
    public void ISegnapostoVengonoSostituiti()
    {
        var catalog = new TextCatalog(UiLanguage.Italian);
        Assert.Equal("Nuovo documento Ledger", catalog.Format("status.document.created", "Ledger"));
    }

    [Fact]
    public void UnConteggioDiUnoSceglieLaFormaSingolare()
    {
        var catalog = new TextCatalog(UiLanguage.Italian);

        Assert.Equal("PDF scritto: 1 pagina", catalog.Format("status.export.done", 1));
        Assert.Equal("PDF scritto: 3 pagine", catalog.Format("status.export.done", 3));
    }

    [Fact]
    public void UnContatoreSenzaSegnoEnormeNonFaCadereLaFormattazione()
    {
        var catalog = new TextCatalog(UiLanguage.English);

        Assert.Equal("⟦nessuna.chiave⟧", catalog.Format("nessuna.chiave", ulong.MaxValue));
    }
}
