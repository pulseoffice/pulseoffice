using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public sealed class Round208KeyTipAssignerTests
{
    private readonly KeyTipAssigner _assigner = new();

    public static TheoryData<string[]> TabTitles => new()
    {
        new[] { "Home", "Insert", "Draw", "Design", "Layout", "Formulas", "Data", "Review", "View" },
        new[] { "Home", "Inserisci", "Disegno", "Progettazione", "Layout", "Formule", "Dati", "Revisione", "Visualizza" },
        new[] { "Start", "Einfügen", "Zeichnen", "Entwurf", "Layout", "Formeln", "Daten", "Überprüfen", "Ansicht" },
        new[] { "Inicio", "Insertar", "Dibujar", "Diseño", "Disposición", "Fórmulas", "Datos", "Revisar", "Vista" },
        new[] { "Accueil", "Insertion", "Dessin", "Création", "Mise en page", "Formules", "Données", "Révision", "Affichage" },
    };

    [Theory]
    [MemberData(nameof(TabTitles))]
    public void Every_title_gets_a_distinct_typeable_tip_in_every_language(string[] titles)
    {
        var tips = _assigner.Assign(titles);

        Assert.Equal(titles.Length, tips.Count);
        Assert.All(tips, tip => Assert.Matches("^[A-Z1-9]$", tip));
        Assert.Equal(tips.Count, tips.Distinct().Count());
    }

    [Fact]
    public void A_tip_comes_from_the_title_first_letter_then_its_other_letters()
    {
        var tips = _assigner.Assign(["Home", "Help", "Über"]);

        Assert.Equal(new[] { "H", "E", "U" }, tips);
    }

    [Fact]
    public void Titles_beyond_the_single_keys_get_prefixed_tips_that_never_clash_with_singles()
    {
        var titles = Enumerable.Range(0, 50).Select(index => "Same").ToArray();

        var tips = _assigner.Assign(titles);

        Assert.Equal(tips.Count, tips.Distinct().Count());
        var singles = tips.Where(tip => tip.Length == 1).ToHashSet();
        Assert.DoesNotContain(tips.Where(tip => tip.Length == 2), tip => singles.Contains(tip[..1]));
    }

    [Theory]
    [InlineData("a", 'A')]
    [InlineData("é", 'E')]
    [InlineData("7", '7')]
    [InlineData("Tab", null)]
    public void Typed_symbols_fold_to_tip_letters(string symbol, char? expected) =>
        Assert.Equal(expected, _assigner.Typed(symbol));
}
