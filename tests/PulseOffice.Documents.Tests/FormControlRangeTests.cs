using PulseOffice.Document.Forms;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class FormControlRangeTests
{
    private readonly FormControlInteraction _interaction = new();

    [Fact]
    public void UnPulsanteDiRotazioneConMinimoEMassimoInvertitiNonSollevaEccezioni()
    {
        var control = new FormControlProperties { Name = "spin", Range = new NumericRange(10, 0, 1, 10), Value = new FormControlValue { Number = 5 } };

        var pressed = _interaction.Press(FormControlKind.SpinButton, control, 0.5, 0.2, 0, false);

        Assert.Equal(6, pressed!.Value.Number);
    }

    [Fact]
    public void UnaBarraDiScorrimentoConMinimoEMassimoInvertitiNonSollevaEccezioni()
    {
        var control = new FormControlProperties { Name = "scroll", Range = new NumericRange(100, 0, 1, 10) };

        var dragged = _interaction.Drag(FormControlKind.ScrollBar, control, 0.25, 0, true);

        Assert.InRange(dragged!.Value.Number!.Value, 0, 100);
    }
}
