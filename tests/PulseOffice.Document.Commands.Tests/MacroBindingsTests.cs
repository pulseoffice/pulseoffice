using PulseOffice.Document.Forms;
using Xunit;

namespace PulseOffice.Document.Commands.Tests;

public sealed class MacroBindingsTests
{
    private readonly MacroBindingRules _rules = new();

    private static readonly MacroEventSlot[] Slots =
    [
        new("Activated", "t.a", "d.a", "Sheet1_Activated"),
        new("Calculated", "t.c", "d.c", "Sheet1_Calculated"),
    ];

    private static DocumentMacro Js(string name, string source = "app.Log(1);") => new(name, MacroLanguage.JavaScript, source);

    [Fact]
    public void LoStatoDistingueNessunaAssegnataEMancante()
    {
        IReadOnlyList<DocumentMacro> library = [Js("Saluta")];

        Assert.Equal(MacroBindingStatus.Unassigned, _rules.Status(null, library));
        Assert.Equal(MacroBindingStatus.Assigned, _rules.Status("saluta", library));
        Assert.Equal(MacroBindingStatus.Missing, _rules.Status("Altro", library));
    }

    [Fact]
    public void IlNomePropostoEvitaIDoppioni()
    {
        Assert.Equal("Sheet1_Activated", _rules.ProposeName("Sheet1_Activated", []));
        Assert.Equal("Sheet1_Activated 3", _rules.ProposeName("Sheet1_Activated", [Js("Sheet1_Activated"), Js("sheet1_activated 2")]));
        Assert.Equal("Macro", _rules.ProposeName("  ", []));
    }

    [Fact]
    public void LeAssegnazioniSannoSeSonoCambiate()
    {
        var assignments = new MacroAssignments(Slots, new Dictionary<string, string?> { ["Activated"] = " Saluta " });

        Assert.Equal("Saluta", assignments.For("Activated"));
        Assert.False(assignments.IsChanged);

        assignments.Assign("Calculated", "Conta");
        Assert.True(assignments.IsChanged);

        assignments.Clear("Calculated");
        Assert.False(assignments.IsChanged);
        Assert.Throws<ArgumentException>(() => assignments.Assign("Ignoto", "x"));
    }

    [Fact]
    public void UnaRinominaSegueLAssegnazione()
    {
        var assignments = new MacroAssignments(Slots, new Dictionary<string, string?> { ["Activated"] = "Saluta" });

        _rules.Reconcile(assignments, [Js("Saluta", "a"), Js("Altra", "b")], [Js("Benvenuto", "a"), Js("Altra", "b")]);

        Assert.Equal("Benvenuto", assignments.For("Activated"));
        Assert.Equal(MacroBindingStatus.Assigned, _rules.Status(assignments.For("Activated"), [Js("Benvenuto", "a")]));
    }

    [Fact]
    public void UnaMacroEliminataResaMancanteNonVieneIndovinata()
    {
        var assignments = new MacroAssignments(Slots, new Dictionary<string, string?> { ["Activated"] = "Saluta" });

        _rules.Reconcile(assignments, [Js("Saluta", "a")], [Js("Nuova", "diverso")]);

        Assert.Equal("Saluta", assignments.For("Activated"));
        Assert.Equal(MacroBindingStatus.Missing, _rules.Status("Saluta", [Js("Nuova", "diverso")]));
    }

    [Fact]
    public void UnaRinominaAmbiguaNonSiApplica()
    {
        var renames = _rules.Renames([Js("A", "x"), Js("B", "x")], [Js("C", "x")]);

        Assert.Empty(renames);
    }
}
