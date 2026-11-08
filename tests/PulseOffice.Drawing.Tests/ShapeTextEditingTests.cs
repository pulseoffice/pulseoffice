using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ShapeTextEditingTests
{
    private readonly IShapeTextEditing _editing = new ShapeTextEditing();
    private readonly ShapeObject _empty = new() { Id = "s", Shape = ShapeKind.Rectangle };

    [Fact]
    public void Scrivere_in_una_forma_vuota_mette_il_testo_senza_sequenze()
    {
        var shape = _editing.WithText(_empty, "Ciao");
        Assert.Equal("Ciao", shape.Text);
        Assert.Null(shape.Runs);
    }

    [Fact]
    public void Svuotare_il_testo_toglie_testo_e_sequenze()
    {
        var shape = _editing.WithText(_editing.Format(_editing.WithText(_empty, "Ciao"), 0, 4, run => run with { Bold = true }), string.Empty);
        Assert.Null(shape.Text);
        Assert.Null(shape.Runs);
    }

    [Fact]
    public void La_formattazione_si_applica_alla_selezione_e_il_testo_inserito_prende_quella_a_sinistra()
    {
        var shape = _editing.Format(_editing.WithText(_empty, "uno due tre"), 4, 7, run => run with { Bold = true });
        Assert.Equal(3, shape.Runs!.Count);
        Assert.True(shape.Runs[1].Bold);

        var typed = _editing.WithText(shape, "uno duetto tre");
        Assert.Equal("uno duetto tre", typed.Text);
        Assert.Equal("duetto", typed.Runs!.Single(run => run.Bold).Text);
    }

    [Fact]
    public void Senza_selezione_la_formattazione_vale_per_tutto_il_testo_e_le_sequenze_uguali_si_fondono()
    {
        var shape = _editing.Format(_editing.WithText(_empty, "abc"), 1, 2, run => run with { Italic = true });
        var all = _editing.Format(shape, 2, 2, run => run with { Italic = true });
        Assert.True(Assert.Single(all.Runs!).Italic);
        Assert.True(_editing.RunAt(all, 0, 0).Italic);
    }

    [Fact]
    public void I_ritorni_a_capo_si_normalizzano()
    {
        Assert.Equal("a\nb", _editing.WithText(_empty, "a\r\nb").Text);
    }
}
