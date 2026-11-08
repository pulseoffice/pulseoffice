using Xunit;

namespace PulseOffice.Printing.Tests;

public sealed class PageRangeBoundsTests
{
    [Theory]
    [InlineData("1-2147483647")]
    [InlineData("2147483647-1")]
    [InlineData("-2000000000")]
    public async Task Un_intervallo_enorme_si_risolve_subito_entro_le_pagine_del_documento(string range)
    {
        var parse = Task.Run(() => new PageRangeParser().Parse(range, 3), TestContext.Current.CancellationToken);

        var pages = await parse.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.NotNull(pages);
        Assert.Equal(3, pages.Count);
        Assert.Equal([0, 1, 2], pages.Order());
    }

    [Fact]
    public void Gli_intervalli_normali_restano_come_prima()
    {
        var parser = new PageRangeParser();

        Assert.Equal([2, 1, 0], parser.Parse("3-1", 5));
        Assert.Equal([3, 4], parser.Parse("4-", 5));
        Assert.Equal([0, 1], parser.Parse("-2", 5));
        Assert.Equal([4], parser.Parse("5-9", 5));
    }
}
