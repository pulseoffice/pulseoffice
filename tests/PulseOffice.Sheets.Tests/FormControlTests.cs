using PulseOffice.Document.Forms;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class FormControlCatalogTests
{
    private readonly FormControlCatalog _catalog = new();

    [Fact]
    public void IlCatalogoDescriveTuttiITipiDiControllo()
    {
        var kinds = Enum.GetValues<FormControlKind>();

        Assert.Equal(kinds.Length, _catalog.All.Count);

        foreach (var kind in kinds) Assert.Equal(kind, _catalog.Describe(kind).Kind);
    }

    [Fact]
    public void UnEtichettaNonRiceveIlFuoco()
    {
        Assert.False(_catalog.Create(FormControlKind.Label, "Etichetta1").TabStop);
        Assert.True(_catalog.Create(FormControlKind.TextBox, "Testo1").TabStop);
    }

    [Fact]
    public void LaBarraDiScorrimentoNasceConIlProprioIntervallo()
    {
        var control = _catalog.Create(FormControlKind.ScrollBar, "Barra1");

        Assert.NotNull(control.Range);
        Assert.Equal(0, control.Range!.Minimum);
        Assert.Equal(100, control.Range.Maximum);
    }

    [Fact]
    public void OgniControlloNasceConUnaMisuraNonNulla()
    {
        foreach (var descriptor in _catalog.All)
        {
            Assert.True(descriptor.DefaultSize.Width.Value > 0);
            Assert.True(descriptor.DefaultSize.Height.Value > 0);
        }
    }
}

public sealed class TabOrderingTests
{
    private readonly TabOrdering _ordering = new();

    private static FormControlProperties Control(
        string name, int index, bool enabled = true, bool stop = true) =>
        new() { Name = name, TabIndex = index, Enabled = enabled, TabStop = stop };

    [Fact]
    public void LOrdineSiRinumeraSenzaBuchi()
    {
        var normalized = _ordering.Normalize(
            [Control("a", 7), Control("b", 2), Control("c", 5)]);

        Assert.Equal(["b", "c", "a"], normalized.Select(control => control.Name));
        Assert.Equal([0, 1, 2], normalized.Select(control => control.TabIndex));
    }

    [Fact]
    public void SpostareUnControlloRinumeraGliAltri()
    {
        var moved = _ordering.Move(
            [Control("a", 0), Control("b", 1), Control("c", 2)], "c", -2);

        Assert.Equal(["c", "a", "b"], moved.Select(control => control.Name));
    }

    [Fact]
    public void SpostareOltreIlBordoSiFermaAlBordo()
    {
        var moved = _ordering.Move([Control("a", 0), Control("b", 1)], "a", -5);

        Assert.Equal(["a", "b"], moved.Select(control => control.Name));
    }

    [Fact]
    public void IlFuocoSaltaCioCheNonLoPuoRicevere()
    {
        IReadOnlyList<FormControlProperties> controls =
        [Control("a", 0), Control("etichetta", 1, stop: false), Control("b", 2, enabled: false), Control("c", 3)];

        Assert.Equal("c", _ordering.Next(controls, "a", backwards: false)?.Name);
    }

    [Fact]
    public void IlFuocoGiraInTondo()
    {
        IReadOnlyList<FormControlProperties> controls = [Control("a", 0), Control("b", 1)];

        Assert.Equal("a", _ordering.Next(controls, "b", backwards: false)?.Name);
        Assert.Equal("b", _ordering.Next(controls, "a", backwards: true)?.Name);
    }

    [Fact]
    public void SenzaControlliRaggiungibiliNonCEUnProssimo()
    {
        Assert.Null(_ordering.Next([Control("a", 0, stop: false)], null, backwards: false));
    }
}

public sealed class FormControlBindingTests
{
    private readonly FormControlBinding _binding = new(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant());

    private static Worksheet Sheet()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Nord"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Sud"));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("Est"));
        return sheet;
    }

    [Fact]
    public void LeVociArrivanoDallIntervalloDiOrigine()
    {
        var items = _binding.ItemsFor(Sheet(), new FormControlProperties { SourceRange = "A1:A3" });

        Assert.Equal(["Nord", "Sud", "Est"], items);
    }

    [Fact]
    public void SenzaIntervalloRestanoLeVociScritteAMano()
    {
        var control = new FormControlProperties { Items = ["Uno", "Due"] };

        Assert.Equal(["Uno", "Due"], _binding.ItemsFor(Sheet(), control));
    }

    [Fact]
    public void UnaSpuntaScriveVeroOFalso()
    {
        var control = new FormControlProperties
        {
            LinkedCell = "B1",
            Value = new FormControlValue { Checked = CheckState.Checked },
        };

        var write = _binding.Publish(FormControlKind.CheckBox, control);

        Assert.Equal(new CellAddress(0, 1), write!.Address);
        Assert.Equal(CellValue.True, write.Value);
    }

    [Fact]
    public void UnElencoScriveLaPosizioneAPartireDaUno()
    {
        var control = new FormControlProperties
        {
            LinkedCell = "B1",
            Value = new FormControlValue { SelectedIndices = [1] },
        };

        Assert.Equal(2, _binding.Publish(FormControlKind.ListBox, control)!.Value.AsNumber());
    }

    [Fact]
    public void SenzaCellaCollegataNonSiScriveNulla()
    {
        Assert.Null(_binding.Publish(FormControlKind.TextBox, new FormControlProperties()));
    }

    [Fact]
    public void IlValoreDellaCellaTornaNelControllo()
    {
        var sheet = Sheet();
        sheet.SetValue(new CellAddress(0, 1), CellValue.True);

        var control = _binding.Absorb(
            sheet, FormControlKind.CheckBox, new FormControlProperties { LinkedCell = "B1" });

        Assert.Equal(CheckState.Checked, control.Value.Checked);
    }

    [Fact]
    public void UnNumeroFuoriDallIntervalloVieneRiportatoDentro()
    {
        var sheet = Sheet();
        sheet.SetValue(new CellAddress(0, 1), CellValue.Number(500));

        var control = _binding.Absorb(sheet, FormControlKind.ScrollBar, new FormControlProperties
        {
            LinkedCell = "B1",
            Range = new NumericRange(0, 100, 1, 10),
        });

        Assert.Equal(100, control.Value.Number);
    }

    [Fact]
    public void UnaCellaVuotaSuUnaSpuntaATreStatiDaLoStatoIndeterminato()
    {
        var control = _binding.Absorb(Sheet(), FormControlKind.CheckBox, new FormControlProperties
        {
            LinkedCell = "B1",
            TriState = true,
        });

        Assert.Equal(CheckState.Indeterminate, control.Value.Checked);
    }
}

public sealed class FormControlInteractionTests
{
    private readonly FormControlInteraction _interaction = new();

    private static FormControlProperties Control(FormControlValue? value = null) =>
        new() { Name = "c", Value = value ?? new FormControlValue() };

    [Fact]
    public void UnEtichettaNonRispondeAlClic()
    {
        Assert.False(_interaction.Responds(FormControlKind.Label));
        Assert.True(_interaction.Responds(FormControlKind.CheckBox));
    }

    [Fact]
    public void LaSpuntaSiAccendeESiSpegne()
    {
        var control = Control();

        var on = _interaction.Press(FormControlKind.CheckBox, control, 0.1, 0.5, 0, true)!;
        Assert.Equal(CheckState.Checked, on.Value.Checked);

        var off = _interaction.Press(FormControlKind.CheckBox, on, 0.1, 0.5, 0, true)!;
        Assert.Equal(CheckState.Unchecked, off.Value.Checked);
    }

    [Fact]
    public void LaSpuntaATreStatiPassaDaQuelloIndeterminato()
    {
        var control = Control() with { TriState = true };

        var on = _interaction.Press(FormControlKind.CheckBox, control, 0.1, 0.5, 0, true)!;
        var middle = _interaction.Press(FormControlKind.CheckBox, on, 0.1, 0.5, 0, true)!;

        Assert.Equal(CheckState.Indeterminate, middle.Value.Checked);
    }

    [Fact]
    public void UnPulsanteDiOpzioneGiaAccesoNonSiSpegneDaSolo()
    {
        var control = Control(new FormControlValue { Checked = CheckState.Checked });

        Assert.Null(_interaction.Press(FormControlKind.OptionButton, control, 0.1, 0.5, 0, true));
    }

    [Fact]
    public void UnControlloDisattivatoNonReagisce()
    {
        var control = Control() with { Enabled = false };

        Assert.Null(_interaction.Press(FormControlKind.CheckBox, control, 0.1, 0.5, 0, true));
    }

    [Fact]
    public void NellElencoSiSelezionaLaRigaSottoIlPuntatore()
    {
        var selected = _interaction.Press(FormControlKind.ListBox, Control(), 0.5, 0.45, 5, true)!;

        Assert.Equal([2], selected.Value.SelectedIndices);
    }

    [Fact]
    public void UnClicOltreLUltimaVoceNonSelezionaNulla()
    {
        Assert.Null(_interaction.Press(FormControlKind.ListBox, Control(), 0.5, 0.9, 2, true));
    }

    [Fact]
    public void IlPulsanteDiIncrementoSaleESende()
    {
        var control = Control() with { Range = new NumericRange(0, 10, 2, 4) };

        var up = _interaction.Press(FormControlKind.SpinButton, control, 0.5, 0.2, 0, false)!;
        Assert.Equal(2, up.Value.Number);

        var down = _interaction.Press(FormControlKind.SpinButton, up, 0.5, 0.8, 0, false)!;
        Assert.Equal(0, down.Value.Number);
    }

    [Fact]
    public void IlPulsanteDiIncrementoNonSuperaIlProprioIntervallo()
    {
        var control = Control(new FormControlValue { Number = 10 }) with { Range = new NumericRange(0, 10, 5, 4) };

        Assert.Equal(10, _interaction.Press(FormControlKind.SpinButton, control, 0.5, 0.2, 0, false)!.Value.Number);
    }

    [Fact]
    public void LaBarraOrizzontaleLeggeLaPosizioneInLarghezza()
    {
        var control = Control() with { Range = new NumericRange(0, 200, 1, 10) };

        Assert.Equal(50, _interaction.Press(FormControlKind.ScrollBar, control, 0.25, 0.9, 0, true)!.Value.Number);
    }

    [Fact]
    public void LaBarraVerticaleLeggeLaPosizioneInAltezza()
    {
        var control = Control() with { Range = new NumericRange(0, 200, 1, 10) };

        Assert.Equal(150, _interaction.Press(FormControlKind.ScrollBar, control, 0.1, 0.75, 0, false)!.Value.Number);
    }

    [Fact]
    public void TrascinareLaBarraNeCambiaIlValore()
    {
        var control = Control() with { Range = new NumericRange(0, 100, 1, 10) };

        Assert.Equal(80, _interaction.Drag(FormControlKind.ScrollBar, control, 0.8, 0.5, true)!.Value.Number);
        Assert.Null(_interaction.Drag(FormControlKind.CheckBox, control, 0.8, 0.5, true));
    }
    [Fact]
    public void LElencoUsaLaStessaAltezzaDiRigaDelDisegno()
    {
        var layout = new ListBoxLayout();
        const double height = 100;
        var offset = layout.RowTop(3) + (layout.RowHeight / 2);

        var selected = _interaction.Press(FormControlKind.ListBox, Control(), 0.5, offset / height, 10, false, height)!;

        Assert.Equal([3], selected.Value.SelectedIndices);
    }

    [Fact]
    public void LaBarraSiFermaSuiPassiDalMinimo()
    {
        var control = Control() with { Range = new NumericRange(1, 21, 5, 10) };

        Assert.Equal(11, _interaction.Press(FormControlKind.ScrollBar, control, 0.45, 0.5, 0, true)!.Value.Number);
    }

    [Fact]
    public void LIncrementoOrizzontaleUsaLAsseOrizzontale()
    {
        var control = Control(new FormControlValue { Number = 4 }) with { Range = new NumericRange(0, 10, 1, 4) };

        Assert.Equal(5, _interaction.Press(FormControlKind.SpinButton, control, 0.8, 0.9, 0, true)!.Value.Number);
        Assert.Equal(3, _interaction.Press(FormControlKind.SpinButton, control, 0.2, 0.1, 0, true)!.Value.Number);
    }

    [Fact]
    public void ScegliereUnOpzioneSpegneLeAltreDelloStessoGruppo()
    {
        FormControlObject Option(string group, CheckState state) => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            ControlKind = FormControlKind.OptionButton,
            Properties = new FormControlProperties
            {
                Group = group,
                Value = new FormControlValue { Checked = state },
            },
        };

        var controls = new List<FormControlObject?>
        {
            Option("a", CheckState.Checked),
            Option("a", CheckState.Checked),
            Option("b", CheckState.Checked),
        };

        var releases = new OptionGroups().Release(controls, 1);

        Assert.Equal([0], releases.Select(release => release.Index));
        Assert.Equal(CheckState.Unchecked, releases[0].Released.Value.Checked);
    }
}

public sealed class FormControlEventBindingTests
{
    private readonly FormControlCatalog _catalog = new();

    [Fact]
    public void UnEventoSiAggancioAUnaMacroESiStacca()
    {
        var control = new FormControlProperties { Name = "Invia" };

        var bound = control.Bind(FormControlEvent.Clicked, "Saluta");
        Assert.Equal("Saluta", bound.MacroFor(FormControlEvent.Clicked));

        var loose = bound.Bind(FormControlEvent.Clicked, null);
        Assert.Null(loose.MacroFor(FormControlEvent.Clicked));
    }

    [Fact]
    public void UnNomeDiMacroConSpaziVieneRipulito()
    {
        var bound = new FormControlProperties().Bind(FormControlEvent.Clicked, "  Saluta  ");

        Assert.Equal("Saluta", bound.MacroFor(FormControlEvent.Clicked));
    }

    [Fact]
    public void OgniControlloDichiaraIPropriEventi()
    {
        Assert.Contains(FormControlEvent.Clicked, _catalog.Describe(FormControlKind.PushButton).Events);
        Assert.Contains(FormControlEvent.ValueChanged, _catalog.Describe(FormControlKind.CheckBox).Events);
        Assert.Empty(_catalog.Describe(FormControlKind.GroupBox).Events);
    }

    [Fact]
    public void IGruppiDiProprietaCambianoConIlControllo()
    {
        Assert.True(_catalog.Describe(FormControlKind.ListBox).Groups.HasFlag(FormPropertyGroup.List));
        Assert.False(_catalog.Describe(FormControlKind.Label).Groups.HasFlag(FormPropertyGroup.List));

        Assert.True(_catalog.Describe(FormControlKind.ScrollBar).Groups.HasFlag(FormPropertyGroup.Scroll));
        Assert.True(_catalog.Describe(FormControlKind.PushButton).Groups.HasFlag(FormPropertyGroup.Button));
    }
}

public sealed class MacroLibraryTests
{
    [Fact]
    public void UnaMacroSiSalvaESiRitrovaSenzaBadareAlleMaiuscole()
    {
        var library = new MacroLibrary();
        library.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "x"));

        Assert.Equal("x", library.Find("saluta")!.Source);
    }

    [Fact]
    public void SalvareDueVolteSostituisceInvecheDiDuplicare()
    {
        var library = new MacroLibrary();
        library.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "uno"));
        library.Store(new DocumentMacro("Saluta", MacroLanguage.Python, "due"));

        Assert.Single(library.All);
        Assert.Equal(MacroLanguage.Python, library.Find("Saluta")!.Language);
    }

    [Fact]
    public void TogliereUnaMacroCheNonCEsisteNonCambiaNulla()
    {
        var library = new MacroLibrary();

        Assert.False(library.Remove("Assente"));
    }

}
