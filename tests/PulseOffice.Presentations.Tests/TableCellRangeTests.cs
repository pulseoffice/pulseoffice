using PulseOffice.Document.Tables;
using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class TableCellRangeTests
{
    [Fact]
    public void UnaSelezioneOltreIBordiSiRestringeAllaTabella()
    {
        var clamped = new TableCellRange(1, 0, 4, 3).ClampedTo(3, 2);

        Assert.Equal(new TableCellRange(1, 0, 2, 1), clamped);
    }

    [Fact]
    public void UnaSelezioneTuttaFuoriSparisce() =>
        Assert.Null(new TableCellRange(5, 0, 6, 0).ClampedTo(3, 2));
}
