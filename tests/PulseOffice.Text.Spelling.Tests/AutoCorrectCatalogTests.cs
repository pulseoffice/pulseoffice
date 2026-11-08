using System.IO.Compression;
using System.Text;
using PulseOffice.Text.Spelling;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class AutoCorrectCatalogTests
{
    private readonly AutoCorrectFixture _fixture = new();

    [Fact]
    public void LElencoItalianoInclusoHaMigliaiaDiVociEsterno()
    {
        var italian = _fixture.Defaults.For("it");

        Assert.True(italian.Replacements.Count > 1000);
        Assert.Equal("perché", italian.Replacements["perchè"].To);
        Assert.Equal(EntryOrigin.Bundled, italian.Replacements["perchè"].Origin);
        Assert.Contains("ecc.", italian.Abbreviations.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LeVociNostreCompletanoQuelleIncluse()
    {
        var italian = _fixture.Defaults.For("it");

        Assert.Equal(EntryOrigin.BuiltIn, italian.Replacements["qual'è"].Origin);
        Assert.Equal("–", italian.Replacements["--"].To);
    }

    [Theory]
    [InlineData("it-IT", "it")]
    [InlineData("it", "it")]
    [InlineData("en", "en-US")]
    [InlineData("pt_BR", "pt-BR")]
    [InlineData("xx-YY", "en-US")]
    public void LaLinguaRicadeSullaBase(string asked, string expected) =>
        Assert.Equal(expected, _fixture.Defaults.Resolve(asked));

    [Fact]
    public void TutteLeLingueEsternoSonoDisponibili()
    {
        Assert.True(_fixture.Defaults.Languages.Count > 40);
        Assert.Contains("de", _fixture.Defaults.Languages);
    }

    [Fact]
    public void LaLicenzaELaProvenienzaStannoNellOutput()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "autocorrect");

        Assert.Contains("LibreOffice", File.ReadAllText(Path.Combine(folder, "NOTICE")));
        Assert.Contains("Mozilla Public License", File.ReadAllText(Path.Combine(folder, "LICENSE-MPL-2.0.txt")));
    }

    [Fact]
    public void UnFileDatCostruitoSiLegge()
    {
        var path = Path.Combine(Path.GetTempPath(), $"acor_{Guid.NewGuid():N}.dat");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Write(archive, "DocumentList.xml",
                    Blocks(("abc", "alfa"), (".*(K)", "Ⓚ"), ("due parole", "x"), (".*a.*b", "y")));
                Write(archive, "SentenceExceptList.xml", Blocks(("tel.", null)));
                Write(archive, "WordExceptList.xml", Blocks(("QXs", null)));
            }

            var lists = new AcorListReader(new AutoCorrectEntryNormalizer()).Read(path);

            Assert.NotNull(lists);
            Assert.Equal("alfa", lists.Replacements["abc"]);
            Assert.Equal("Ⓚ", lists.Replacements["(K)"]);
            Assert.Equal(2, lists.Replacements.Count);
            Assert.Equal(["tel."], lists.Abbreviations);
            Assert.Equal(["QXs"], lists.TwoCapitalWords);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnFileRovinatoODtdVengonoIgnorati()
    {
        var reader = new AcorListReader(new AutoCorrectEntryNormalizer());
        var broken = Path.Combine(Path.GetTempPath(), $"acor_{Guid.NewGuid():N}.dat");
        var dtd = Path.Combine(Path.GetTempPath(), $"acor_{Guid.NewGuid():N}.dat");
        try
        {
            File.WriteAllText(broken, "non è uno zip");
            using (var archive = ZipFile.Open(dtd, ZipArchiveMode.Create))
                Write(archive, "DocumentList.xml", "<!DOCTYPE x [<!ENTITY e \"boom\">]><x>&e;</x>");

            Assert.Null(reader.Read(broken));
            Assert.Null(reader.Read(dtd));
        }
        finally
        {
            File.Delete(broken);
            File.Delete(dtd);
        }
    }

    [Fact]
    public void IlFileDatVeroEsternoSiLegge()
    {
        const string path = "/Applications/LibreOffice.app/Contents/Resources/autocorr/acor_it.dat";
        if (!File.Exists(path)) return;

        var lists = new AcorListReader(new AutoCorrectEntryNormalizer()).Read(path);

        Assert.NotNull(lists);
        Assert.True(lists.Replacements.Count > 1000);
        Assert.Contains("ecc.", lists.Abbreviations, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnJsonDiAltroFormatoNonSiLegge()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"format\":\"altro\",\"replacements\":{\"a\":\"b\"}}");
            Assert.Null(new AutoCorrectJsonReader(new AutoCorrectEntryNormalizer()).Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var stream = archive.CreateEntry(name).Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }

    private static string Blocks(params (string Short, string? Long)[] blocks)
    {
        var builder = new StringBuilder("<block-list:block-list xmlns:block-list=\"http://openoffice.org/2001/block-list\">");
        foreach (var (shortcut, value) in blocks)
        {
            builder.Append("<block-list:block block-list:abbreviated-name=\"").Append(System.Security.SecurityElement.Escape(shortcut)).Append('"');
            if (value is not null) builder.Append(" block-list:name=\"").Append(value).Append('"');
            builder.Append("/>");
        }

        return builder.Append("</block-list:block-list>").ToString();
    }
}

public sealed class AutoCorrectDraftTests
{
    private readonly AutoCorrectFixture _fixture = new();

    private ReplacementListDraft Replacements() =>
        new(new Dictionary<string, DefaultReplacement>(StringComparer.OrdinalIgnoreCase)
        {
            ["teh"] = new("the", EntryOrigin.Bundled),
            ["adn"] = new("and", EntryOrigin.BuiltIn),
        }, LanguageCustomization.None);

    [Theory]
    [InlineData("", "x", EntryProblem.Empty)]
    [InlineData("abc", " ", EntryProblem.Empty)]
    [InlineData("abc", "abc", EntryProblem.Same)]
    [InlineData("TEH", "x", EntryProblem.Duplicate)]
    [InlineData("due parole", "x", EntryProblem.Invalid)]
    [InlineData("nuovo", "vecchio", EntryProblem.None)]
    public void LaValidazioneDiceCosaNonVa(string from, string to, EntryProblem expected) =>
        Assert.Equal(expected, Replacements().Check(from, to));

    [Fact]
    public void AggiungereModificareEdEliminareCambianoLaPersonalizzazione()
    {
        var draft = Replacements();

        Assert.Equal(EntryProblem.None, draft.Add("ciao", "Ciao!"));
        Assert.Equal(EntryProblem.None, draft.Update("teh", "teh", "THE"));
        Assert.True(draft.Remove("adn"));

        var (replacements, removed) = draft.Customization();
        Assert.Equal("Ciao!", replacements["ciao"]);
        Assert.Equal("THE", replacements["teh"]);
        Assert.Equal(["adn"], removed);
        Assert.Equal(EntryOrigin.Modified, draft.Entries.Single(entry => entry.From == "teh").Origin);
        Assert.Equal(EntryOrigin.User, draft.Entries.Single(entry => entry.From == "ciao").Origin);
    }

    [Fact]
    public void ModificareUnaVoceSenzaCambiarneLaChiaveNonEUnDoppione()
    {
        var draft = Replacements();

        Assert.Equal(EntryProblem.None, draft.Update("teh", "Teh", "the!"));
        Assert.Equal(EntryProblem.Duplicate, draft.Update("teh", "adn", "x"));
    }

    [Fact]
    public void RipristinarePortaAllePredefinite()
    {
        var draft = Replacements();
        draft.Add("ciao", "Ciao!");
        draft.Remove("teh");

        Assert.False(draft.IsAtDefaults);
        draft.RestoreDefaults();

        Assert.True(draft.IsAtDefaults);
        Assert.Empty(draft.Customization().Replacements);
    }

    [Fact]
    public void LImportazioneUnisceComeVociDellUtente()
    {
        var draft = Replacements();

        var merged = draft.Merge(new Dictionary<string, string> { ["xyz"] = "XYZ", ["teh"] = "the", [""] = "x" });

        Assert.Equal(1, merged);
        Assert.Equal(EntryOrigin.User, draft.Entries.Single(entry => entry.From == "xyz").Origin);
    }

    [Fact]
    public void LeAbbreviazioniPrendonoIlPunto()
    {
        var draft = new WordListDraft(new AbbreviationKind(), new Dictionary<string, EntryOrigin>(), WordListCustomization.None);

        Assert.Equal(EntryProblem.None, draft.Add("pagg"));
        Assert.Equal(EntryProblem.Duplicate, draft.Add("pagg."));
        Assert.Equal(EntryProblem.Invalid, draft.Add("..."));
        Assert.Equal(["pagg."], draft.Customization().Added);
    }

    [Fact]
    public void LeParoleConDueMaiuscoleVannoVerificate()
    {
        var draft = new WordListDraft(new TwoCapitalsKind(), new Dictionary<string, EntryOrigin> { ["IDs"] = EntryOrigin.Bundled }, WordListCustomization.None);

        Assert.Equal(EntryProblem.Invalid, draft.Add("Casa"));
        Assert.Equal(EntryProblem.None, draft.Add("QXs"));
        Assert.True(draft.Remove("IDs"));
        Assert.Equal(["IDs"], draft.Customization().Removed);
    }

    [Fact]
    public void LaRicercaTrovaInEntrambeLeColonneEOrdina()
    {
        var query = new ReplacementQuery();
        var entries = new[]
        {
            new ReplacementEntry("b", "zeta", EntryOrigin.User),
            new ReplacementEntry("a", "Perché", EntryOrigin.Bundled),
            new ReplacementEntry("perche", "x", EntryOrigin.Bundled),
        };

        Assert.Equal(["a", "perche"], query.Apply(entries, "perche", ReplacementSort.FromAscending).Select(entry => entry.From));
        Assert.Equal(["perche", "b", "a"], query.Apply(entries, null, ReplacementSort.FromDescending).Select(entry => entry.From));
        Assert.Equal("zeta", query.Apply(entries, "", ReplacementSort.ToDescending)[0].To);
    }

    [Fact]
    public void IlProfiloConservaSoloLeLingueToccate()
    {
        var draft = new AutoCorrectDraftFactory(_fixture.Defaults).Create(AutoCorrectProfile.Default);
        draft.For("it-IT").Replacements.Add("qwe", "QWE");
        draft.For("de");
        draft.Options = draft.Options with { Dashes = false };

        var profile = draft.Profile();

        Assert.Equal(["it"], profile.Languages.Keys);
        Assert.False(profile.Options.Dashes);
    }

    [Fact]
    public void IlCodecFaIlGiroEIgnoraIRottami()
    {
        var codec = new AutoCorrectProfileCodec();
        var profile = new AutoCorrectProfile(false, AutoCorrectOptions.Default with { OrdinalSuperscript = true },
            new Dictionary<string, LanguageCustomization>
            {
                ["it"] = new(new Dictionary<string, string> { ["qwe"] = "QWE" }, ["perchè"],
                    new WordListCustomization(["pagg."], []), new WordListCustomization([], ["IDs"])),
            });

        var back = codec.Read(codec.Write(profile));

        Assert.False(back.Enabled);
        Assert.True(back.Options.OrdinalSuperscript);
        Assert.Equal("QWE", back.For("it").Replacements["qwe"]);
        Assert.Equal(["perchè"], back.For("it").RemovedReplacements);
        Assert.Equal(["pagg."], back.For("it").Abbreviations.Added);
        Assert.Equal(["IDs"], back.For("it").TwoCapitalWords.Removed);

        var damaged = codec.Read("{\"enabled\":\"forse\",\"languages\":{\"it\":{\"replacements\":{\"a\":3}}}}");
        Assert.True(damaged.Enabled);
        Assert.Empty(damaged.For("it").Replacements);
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => codec.Read("[1,2"));
    }

    [Fact]
    public void LeEccezioniAgisconoSulMotore()
    {
        var table = _fixture.Table;
        table.Customize(new Dictionary<string, LanguageCustomization>
        {
            ["it"] = LanguageCustomization.None with
            {
                Abbreviations = new WordListCustomization(["pagg."], []),
                TwoCapitalWords = new WordListCustomization(["QUota"], []),
            },
        });
        var corrector = new AutoCorrect(table, new WordTokenizer());
        var options = AutoCorrectOptions.Default with { Language = "it", SmartQuotes = false };

        Assert.Equal("Vedi pagg. tre", corrector.Apply("vedi pagg. tre", options));
        Assert.Equal("Vedi ecc. altro", corrector.Apply("vedi ecc. altro", options));
        Assert.Equal("La QUota resta", corrector.Apply("la QUota resta", options));
        Assert.Equal("Il Prezzo", corrector.Apply("il PRezzo", options));
    }
}

public sealed class TextLanguageTests
{
    private readonly TextLanguageResolver _resolver = new();

    [Fact]
    public void VinceIlPrimoCandidatoDichiarato()
    {
        Assert.Equal("de-DE", _resolver.Resolve(null, " ", "de-DE", "fr").Canonical);
        Assert.Equal("fr", _resolver.Resolve("fr", "de-DE").Canonical);
    }

    [Fact]
    public void SenzaCandidatiSiUsaLaLinguaDiSistema() =>
        Assert.Equal(
            LanguageTag.Parse(System.Globalization.CultureInfo.CurrentCulture.Name),
            _resolver.Resolve(null, null));

    [Fact]
    public void LeSostituzioniSeguonoLaLinguaDelTesto()
    {
        var corrector = new AutoCorrect(new AutoCorrectFixture().Table, new WordTokenizer());
        var options = AutoCorrectOptions.Default with { CapitaliseFirstLetter = false };
        var italian = _resolver.Resolve("it-IT", "en-US").Canonical;
        var english = _resolver.Resolve(null, "en-US").Canonical;

        Assert.Equal("perché teh", corrector.Apply("perchè teh", options with { Language = italian }));
        Assert.Equal("perchè the", corrector.Apply("perchè teh", options with { Language = english }));
    }
}
