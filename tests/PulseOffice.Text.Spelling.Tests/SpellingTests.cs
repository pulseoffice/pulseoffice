using PulseOffice.Text.Spelling;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class WordTokenizerTests
{
    private readonly WordTokenizer _tokenizer = new();

    [Fact]
    public void LeParoleArrivanoConLaLoroPosizione()
    {
        var words = _tokenizer.Words("ciao mondo");

        Assert.Equal(2, words.Count);
        Assert.Equal(0, words[0].Start);
        Assert.Equal("mondo", words[1].Text);
        Assert.Equal(5, words[1].Start);
    }

    [Fact]
    public void LaPunteggiaturaNonEntraNellaParola()
    {
        var words = _tokenizer.Words("Ciao, mondo!");

        Assert.Equal(["Ciao", "mondo"], words.Select(word => word.Text));
    }

    [Fact]
    public void LApostrofoTieneInsiemeLaParola()
    {
        Assert.Equal(["dell'anno"], _tokenizer.Words("dell'anno").Select(word => word.Text));
    }

    [Fact]
    public void IlTrattinoTieneInsiemeLaParolaComposta()
    {
        Assert.Equal(["socio-economico"], _tokenizer.Words("socio-economico").Select(word => word.Text));
    }

    [Fact]
    public void NumeriEIndirizziNonSiControllano()
    {
        Assert.Empty(_tokenizer.Words("12345"));
        Assert.Empty(_tokenizer.Words("A1"));
        Assert.Empty(_tokenizer.Words("mario@example.org"));
    }

    [Fact]
    public void UnaLetteraSolaNonSiControlla()
    {
        Assert.Empty(_tokenizer.Words("a e o"));
    }

    [Fact]
    public void UnTestoVuotoNonDaParole()
    {
        Assert.Empty(_tokenizer.Words(string.Empty));
    }
}

public sealed class LanguageTagTests
{
    [Fact]
    public void IlTagSiNormalizza()
    {
        var tag = LanguageTag.Parse("it_IT");

        Assert.Equal("it", tag.Language);
        Assert.Equal("IT", tag.Region);
        Assert.Equal("it-IT", tag.Canonical);
    }

    [Fact]
    public void UnaLinguaSenzaRegioneCorrispondeAQualunqueRegione()
    {
        Assert.True(LanguageTag.Parse("it").Matches(LanguageTag.Parse("it-CH")));
        Assert.False(LanguageTag.Parse("it-IT").Matches(LanguageTag.Parse("it-CH")));
    }

    [Fact]
    public void UnTagVuotoNonEUnaLingua()
    {
        Assert.Equal(LanguageTag.None, LanguageTag.Parse("  "));
    }
}

public sealed class SpellCheckerTests
{
    private sealed class Dictionary(params string[] words) : ISpellDictionary
    {
        public LanguageTag Language { get; } = LanguageTag.Parse("it-IT");

        public bool Check(string word) => words.Contains(word, StringComparer.CurrentCultureIgnoreCase);

        public IReadOnlyList<string> Suggest(string word) => [word + "o", word + "a"];
    }

    private sealed class Provider(ISpellDictionary? dictionary) : ISpellDictionaryProvider
    {
        public IReadOnlyList<LanguageTag> Available => dictionary is null ? [] : [dictionary.Language];

        public ISpellDictionary? For(LanguageTag language) =>
            dictionary is not null && dictionary.Language.Matches(language) ? dictionary : null;

        public void Refresh() { }
    }

    private sealed class UserWords(params string[] known) : IUserDictionary
    {
        private readonly HashSet<string> _words = new(known, StringComparer.CurrentCultureIgnoreCase);

        public IReadOnlyCollection<string> Words => _words;

        public bool Knows(string word) => _words.Contains(word);

        public void Add(string word) => _words.Add(word);

        public bool Remove(string word)
        {
            var removed = _words.Remove(word);
            if (removed) Changed?.Invoke(this, EventArgs.Empty);
            return removed;
        }

        public event EventHandler? Changed;
    }

    private static SpellChecker Checker(ISpellDictionary? dictionary, params string[] userWords) =>
        new(new Provider(dictionary), new UserWords(userWords), new WordTokenizer());

    [Fact]
    public void LeParoleSconosciuteDiventanoErrori()
    {
        var mistakes = Checker(new Dictionary("ciao")).Check("ciao mndo", LanguageTag.Parse("it-IT"));

        var mistake = Assert.Single(mistakes);
        Assert.Equal("mndo", mistake.Word.Text);
        Assert.Equal(5, mistake.Word.Start);
    }

    [Fact]
    public void LeParoleDelDizionarioUtenteNonSonoErrori()
    {
        var mistakes = Checker(new Dictionary("ciao"), "mndo").Check("ciao mndo", LanguageTag.Parse("it-IT"));

        Assert.Empty(mistakes);
    }

    [Fact]
    public void SenzaDizionarioNonSiSegnalaNulla()
    {
        Assert.Empty(Checker(null).Check("qualunque cosa", LanguageTag.Parse("it-IT")));
        Assert.True(Checker(null).IsCorrect("qualunque", LanguageTag.Parse("it-IT")));
    }

    [Fact]
    public void ISuggerimentiSiFermanoAlNumeroChiesto()
    {
        var mistakes = Checker(new Dictionary()).Check("mndo", LanguageTag.Parse("it-IT"), suggestions: 1);

        Assert.Single(Assert.Single(mistakes).Suggestions);
    }

    [Fact]
    public void LApostrofoTipograficoSiControllaComeQuelloDritto()
    {
        Assert.Empty(Checker(new Dictionary("dell'anno")).Check("dell\u2019anno", LanguageTag.Parse("it-IT")));
    }

    [Fact]
    public void UnaLinguaSenzaDizionarioNonSegnalaErrori()
    {
        Assert.Empty(Checker(new Dictionary("ciao")).Check("ciao mndo", LanguageTag.Parse("de-DE")));
    }
}

public sealed class AutoCorrectTests
{
    private readonly AutoCorrect _autoCorrect = new(new AutoCorrectFixture().Table, new WordTokenizer());

    private string Apply(string text, AutoCorrectOptions? options = null) =>
        _autoCorrect.Apply(text, (options ?? AutoCorrectOptions.Default) with { Language = "it" });

    [Theory]
    [InlineData("il 1o posto e la 2a fila", "il 1\u00BA posto e la 2\u00AA fila")]
    [InlineData("the 21st and 12th", "the 21\u02E2\u1D57 and 12\u1D57\u02B0")]
    [InlineData("codice 11st e 3o4", "codice 11st e 3o4")]
    public void GliOrdinaliSiAlzanoSoloSeLaSceltaEAccesa(string input, string expected)
    {
        var options = new AutoCorrectOptions
        {
            OrdinalSuperscript = true, CapitaliseFirstLetter = false, Replacements = false,
        };

        Assert.Equal(expected, Apply(input, options));
        Assert.Equal(input, Apply(input, options with { OrdinalSuperscript = false }));
    }

    [Theory]
    [InlineData("scrivere a ada@esempio.org", "Scrivere a ada@esempio.org")]
    [InlineData("vai su www.sito.it oggi", "Vai su www.sito.it oggi")]
    [InlineData("pesa 3.5kg. poi basta", "Pesa 3.5kg. Poi basta")]
    public void UnPuntoSenzaSpazioDopoNonApreUnaFrase(string input, string expected) =>
        Assert.Equal(expected, Apply(input));

    [Theory]
    [InlineData("\"a lui piace\"", "\"A lui piace\"")]
    [InlineData("(vero) e basta", "(Vero) e basta")]
    [InlineData("fine.\" poi altro", "Fine.\" Poi altro")]
    public void UnaFraseCheIniziaConLeVirgolettePrendeLaMaiuscola(string input, string expected) =>
        Assert.Equal(expected, Apply(input, AutoCorrectOptions.Default with { SmartQuotes = false }));

    [Fact]
    public void LeVirgoletteTipograficheNonFermanoLaMaiuscola()
    {
        Assert.StartsWith("\u201CA", Apply("\"a\" disse"));
    }

    [Fact]
    public void UnaSostituzioneNotaVieneApplicata()
    {
        Assert.Equal("Perché no", Apply("perchè no"));
    }

    [Fact]
    public void LaSostituzioneConservaLaMaiuscolaIniziale()
    {
        var table = new AutoCorrectFixture().Table;
        table.Customize(new Dictionary<string, LanguageCustomization>
        {
            ["it"] = LanguageCustomization.None with { Replacements = new Dictionary<string, string> { ["adn"] = "and" } },
        });

        var corrector = new AutoCorrect(table, new WordTokenizer());

        Assert.Equal("And poi", corrector.Apply("Adn poi", AutoCorrectOptions.Default with
        {
            CapitaliseFirstLetter = false,
            Language = "it",
        }));
    }

    [Fact]
    public void DueMaiuscoleInizialiDiventanoUna()
    {
        Assert.Equal("Casa mia", Apply("CAsa mia"));
    }

    [Fact]
    public void UnAcronimoNonVieneToccato()
    {
        Assert.Equal("ISTAT dice", Apply("ISTAT dice"));
    }

    [Fact]
    public void LaPrimaLetteraDellaFraseDiventaMaiuscola()
    {
        Assert.Equal("Ciao. Come stai?", Apply("ciao. come stai?"));
    }

    [Fact]
    public void LeVirgoletteDiventanoTipografiche()
    {
        Assert.Equal("Dice “ciao”", Apply("Dice \"ciao\""));
    }

    [Fact]
    public void IlTrattinoFraSpaziDiventaLineetta()
    {
        Assert.Equal("Roma – Milano", Apply("Roma - Milano"));
    }

    [Fact]
    public void UnaOpzioneSpentaNonCorregge()
    {
        var options = new AutoCorrectOptions
        {
            Replacements = false,
            CapitaliseFirstLetter = false,
            FixTwoInitialCapitals = false,
            SmartQuotes = false,
            Dashes = false,
        };

        Assert.Equal("perchè \"no\" - CAsa", Apply("perchè \"no\" - CAsa", options));
    }

    [Fact]
    public void ISimboliSiSostituisconoAnchseSenzaLettere()
    {
        Assert.Equal("© 2026", Apply("(c) 2026"));
    }

    [Theory]
    [InlineData("abc(c)def", "Abc(c)def")]
    [InlineData("vedi http://sito.it/a--b/(c)", "Vedi http://sito.it/a--b/(c)")]
    [InlineData("riga ----- fine", "Riga ----- fine")]
    [InlineData("roma -- milano", "Roma – milano")]
    [InlineData("marchio (C) noto", "Marchio © noto")]
    public void ISimboliRispettanoIConfini(string input, string expected) =>
        Assert.Equal(expected, Apply(input));

    [Theory]
    [InlineData("ecco \"a\" e \"b\"", "Ecco “a” e “b”")]
    [InlineData("disse (\"no\")", "Disse (“no”)")]
    [InlineData("alto 5\" largo", "Alto 5” largo")]
    public void LeVirgoletteSiApronoOChiudonoSecondoIlContesto(string input, string expected) =>
        Assert.Equal(expected, Apply(input));

    [Fact]
    public void UnTestoVuotoRestaVuoto()
    {
        Assert.Equal(string.Empty, Apply(string.Empty));
    }
}

public sealed class TokenizerMarksTests
{
    [Fact]
    public void GliAccentiCombinatiRestanoNellaParola()
    {
        var words = new WordTokenizer().Words("perche\u0301 no");

        Assert.Equal("perche\u0301", words[0].Text);
        Assert.Equal(2, words.Count);
    }
}

public sealed class HyphenationTests
{
    private static HyphenationPatterns Patterns() => HyphenationPatterns.Parse(
        LanguageTag.Parse("it"),
        [
            "% commento",
            "a1ba",
            "a1na",
            "1ta",
            "e1re",
            "mo=der=no",
        ]);

    [Fact]
    public void UnEccezioneDichiarataVieneRispettata()
    {
        Assert.Equal([2, 5], Patterns().Positions("moderno"));
    }

    [Fact]
    public void IPuntiDiSillabazioneRispettanoIlMargine()
    {
        var positions = Patterns().Positions("banana", minimumEdge: 3);

        Assert.All(positions, position => Assert.InRange(position, 3, 3));
    }

    [Fact]
    public void UnaParolaTroppoCortaNonSiSpezza()
    {
        Assert.Empty(Patterns().Positions("ab"));
    }

    [Fact]
    public void LIntestazioneDiLibhyphenNonDiventaUnModello()
    {
        var patterns = HyphenationPatterns.Parse(LanguageTag.Parse("it"),
            ["UTF-8", "LEFTHYPHENMIN 3", "RIGHTHYPHENMIN 3", "c/c=,1,1", "a1na"]);

        Assert.Equal(3, patterns.LeftMinimum);
        Assert.Empty(patterns.Positions("banana"));
        Assert.Equal([2, 4], HyphenationPatterns.Parse(LanguageTag.Parse("it"), ["UTF-8", "a1na"]).Positions("banana", 2));
    }

    [Fact]
    public void DopoNextLevelValgonoIModelliDelSecondoLivello()
    {
        var patterns = HyphenationPatterns.Parse(LanguageTag.Parse("it"), ["UTF-8", "n1a", "NEXTLEVEL", "a1na"]);

        Assert.Equal([2, 4], patterns.Positions("banana", 2));
    }

    [Fact]
    public void LaSillabazioneNonSpezzaUnaCoppiaSurrogataNeUnAccentoCombinato()
    {
        var word = "ab\U0002000Bcde\u0301fg";
        var low = word[3].ToString();
        var patterns = HyphenationPatterns.Parse(LanguageTag.Parse("it"), ["UTF-8", "1" + low, "1\u0301", "1c"]);

        Assert.Equal([4], patterns.Positions(word, 2));
    }

    [Fact]
    public void LaSillabazioneUsaLaLinguaDelDizionario()
    {
        var patterns = HyphenationPatterns.Parse(LanguageTag.Parse("tr"), ["UTF-8", "i1i"]);

        Assert.Equal([2], patterns.Positions("İİİİ", 2));
    }

    [Fact]
    public void IlCommentoNonDiventaUnModello()
    {
        Assert.Empty(Patterns().Positions("commento", minimumEdge: 20));
    }
}

public sealed class ThesaurusTests
{
    private static MyThesThesaurus Parse() => MyThesThesaurus.Parse(
        LanguageTag.Parse("it"),
        [
            "casa|2",
            "(sostantivo)|abitazione|dimora|alloggio",
            "(sostantivo)|famiglia|casato",
            "veloce|1",
            "(aggettivo)|rapido|celere",
        ]);

    [Fact]
    public void UnaParolaHaISuoiSignificati()
    {
        var meanings = Parse().Lookup("casa");

        Assert.Equal(2, meanings.Count);
        Assert.Equal("sostantivo", meanings[0].PartOfSpeech);
        Assert.Contains("dimora", meanings[0].Synonyms);
    }

    [Fact]
    public void LaRicercaNonBadaAlleMaiuscole()
    {
        Assert.NotEmpty(Parse().Lookup("Veloce"));
    }

    [Fact]
    public void UnaParolaAssenteNonDaNulla()
    {
        Assert.Empty(Parse().Lookup("inesistente"));
    }
}

public sealed class DeclaredEncodingTextTests
{
    [Fact]
    public void UnFileDichiaratoLatin1SiLeggeConGliAccenti()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dat");

        try
        {
            File.WriteAllBytes(path, [.. "ISO8859-1\ncitt"u8.ToArray(), 0xE0, (byte)'|', (byte)'1', (byte)'\n']);

            var lines = new PulseOffice.Text.Spelling.Hunspell.DeclaredEncodingText().ReadLines(path);

            Assert.Equal("città|1", lines[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public sealed class ThesaurusDuplicatesTests
{
    [Fact]
    public void UnaVoceRipetutaNonCancellaLaPrecedente()
    {
        var thesaurus = MyThesThesaurus.Parse(LanguageTag.Parse("it-IT"),
            ["UTF-8", "casa|1", "(s.f.)|abitazione", "casa|1", "(s.f.)|dimora"]);

        Assert.Equal(2, thesaurus.Lookup("casa").Count);
    }
}
