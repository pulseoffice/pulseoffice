using PulseOffice.Drawing.Freeform;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckDrawingStateTests
{
    [Fact]
    public void ScegliereUnNodoAvvisaChiDipendeDalloStato()
    {
        var state = new DeckDrawingState();
        state.ToggleEditingPoints();
        var raised = 0;
        state.Changed += (_, _) => raised++;

        state.Handle = new PathHandle(0, 1, PathHandlePart.Node);
        state.Handle = new PathHandle(0, 1, PathHandlePart.Node);

        Assert.Equal(1, raised);
    }
}
