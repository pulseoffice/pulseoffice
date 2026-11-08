using PulseOffice.Testing;
using Avalonia.Headless;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Objects;

public sealed class FormulaPictureTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public Task Una_formula_copiata_ha_anche_la_sua_immagine_PNG() => Session.Dispatch(() =>
    {
        var picture = new EmbeddedObjectPicture([new FormulaPicturePainter(new PulseOffice.UI.Controls.Formulas.FormulaObjectRenderer(new PulseOffice.UI.Controls.Formulas.FormulaRenderer(), new PulseOffice.Formulas.FormulaParser()))]);
        var formula = new FormulaObject { Id = "f", Expression = "a^2 + b^2 = c^2" };

        var png = picture.Png(formula, new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1.5)), System.Globalization.CultureInfo.InvariantCulture);

        Assert.NotNull(png);
    }, CancellationToken.None);
}
