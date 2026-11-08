using PulseOffice.Text.Redaction;
using PulseOffice.Text.Scripts;
using Xunit;

namespace PulseOffice.Text.Tests;

public sealed class RedactionAndConversionTests
{
    [Fact]
    public void UnModelloNonValidoOscuraTuttoInveceDiCadere()
    {
        var redactor = new TextRedactor();

        Assert.Equal(redactor.Mask, redactor.Redact("dato riservato", [new RedactionPattern("rotto", "([")]));
    }

    [Fact]
    public void LeParoleDellaTabellaSiLeggonoSenzaGliSpazi()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);

        try
        {
            File.WriteAllText(Path.Combine(folder, "stc_word.dic"), "﻿万历 > 萬曆\n");

            var table = new FolderTextConversionTables(folder, new TextConversionTableReader())
                .For(TextConversionKind.SimplifiedToTraditional)!;

            Assert.Equal("萬曆", new TextConverter().Convert("万历", table));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData("Consegna il 12-05-2024 alle 10.30")]
    [InlineData("Importo 12.500 euro")]
    public void DateOrariEImportiNonSonoTelefoni(string text)
    {
        var redactor = new TextRedactor();

        Assert.Equal(text, redactor.Redact(text, redactor.Patterns.Where(pattern => pattern.Name == "phone")));
    }

    [Fact]
    public void UnNumeroDiTelefonoVieneOscurato()
    {
        var redactor = new TextRedactor();

        Assert.Equal($"Chiama {redactor.Mask}", redactor.Redact("Chiama 06 1234 5678", redactor.Patterns));
    }

    [Fact]
    public void UnNumeroInternazionaleSenzaSpaziVieneOscurato()
    {
        var redactor = new TextRedactor();

        Assert.Equal($"Tel. {redactor.Mask}.", redactor.Redact("Tel. +393331234567.", redactor.Patterns));
    }

    [Fact]
    public void ISegniDiRipetizioneSeguonoLaConversioneDeiKana()
    {
        var kana = new KanaConversion();

        Assert.Equal("\u3044\u309D", kana.ToHiragana("\u30A4\u30FD"));
        Assert.Equal("\u30A4\u30FE", kana.ToKatakana("\u3044\u309E"));
    }
}
