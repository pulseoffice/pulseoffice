using PulseOffice.Text.Scripts;
using Xunit;

namespace PulseOffice.Text.Tests;

public class ScriptConversionTests
{
    private readonly WidthConversion _width = new();
    private readonly KanaConversion _kana = new();

    [Fact]
    public void LeLettereLatineDiventanoALarghezzaPiena()
    {
        Assert.Equal("ＰｕｌｓｅＯｆｆｉｃｅ", _width.ToFullWidth("PulseOffice"));
    }

    [Fact]
    public void ELoSpazioDiventaQuelloIdeografico()
    {
        Assert.Equal("Ａ　Ｂ", _width.ToFullWidth("A B"));
    }

    [Fact]
    public void ILatiniALarghezzaPienaTornanoNormali()
    {
        Assert.Equal("Ledger 42", _width.ToHalfWidth("Ｌｅｄｇｅｒ　４２"));
    }

    [Fact]
    public void ILKatakanaStrettoDiventaLargo()
    {
        Assert.Equal("カタカナ", _width.ToFullWidth("ｶﾀｶﾅ"));
    }

    [Fact]
    public void ILSegnoDiSonorizzazioneSiFondeConLaSuaSillaba()
    {
        Assert.Equal("ガ", _width.ToFullWidth("ｶﾞ"));
        Assert.Equal("パ", _width.ToFullWidth("ﾊﾟ"));
    }

    [Fact]
    public void ELaSillabaSonorizzataSiScomponeTornandoStretta()
    {
        Assert.Equal("ｶﾞ", _width.ToHalfWidth("ガ"));
    }

    [Fact]
    public void HiraganaEKatakanaSiConvertonoNeiDueVersi()
    {
        Assert.Equal("かたかな", _kana.ToHiragana("カタカナ"));
        Assert.Equal("ヒラガナ", _kana.ToKatakana("ひらがな"));
    }

    [Fact]
    public void CioCheNonEKanaRestaComEra()
    {
        Assert.Equal("Ledger 漢字 かな", _kana.ToHiragana("Ledger 漢字 かな"));
    }

    [Fact]
    public void UnCarattereFuoriDalPianoBaseSiConverteIntero()
    {
        var table = new TextConversionTable(
            TextConversionKind.SimplifiedToTraditional,
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["\U00020000"] = "丂", ["万"] = "萬" });

        Assert.Equal("丂萬", new TextConverter().Convert("\U00020000万", table));
    }

    [Fact]
    public void LaTabellaRovesciataMappaOgniCandidato()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(folder, "hhc_char.dic"), "가:伽佳\U00020000\n");

        var tables = new FolderTextConversionTables(folder, new TextConversionTableReader());
        var toHangul = tables.For(TextConversionKind.HanjaToHangul)!;
        var toHanja = tables.For(TextConversionKind.HangulToHanja)!;

        Assert.Equal("가가가", new TextConverter().Convert("伽佳\U00020000", toHangul));
        Assert.Equal("伽", new TextConverter().Convert("가", toHanja));
    }

    [Fact]
    public void LaConversioneAParoleVinceSuQuellaACarattere()
    {
        var table = new TextConversionTable(
            TextConversionKind.SimplifiedToTraditional,
            new Dictionary<string, string> { ["万历"] = "萬曆" },
            new Dictionary<string, string> { ["万"] = "萬", ["历"] = "歷" });

        Assert.Equal("萬曆", new TextConverter().Convert("万历", table));
        Assert.Equal("歷", new TextConverter().Convert("历", table));
    }

    [Fact]
    public void UnaTabellaRovesciataConverteNellAltroVerso()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        File.WriteAllText(Path.Combine(folder, "stc_char.dic"), "万:萬\n历:曆\n");

        var tables = new FolderTextConversionTables(folder, new TextConversionTableReader());

        var table = tables.For(TextConversionKind.TraditionalToSimplified);

        Assert.NotNull(table);
        Assert.Equal("万历", new TextConverter().Convert("萬曆", table));
    }

    [Fact]
    public void SenzaTabelleNonSiDichiaraNessunaConversione()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        Assert.Empty(new FolderTextConversionTables(folder, new TextConversionTableReader()).Available);
    }
}
