using System.Text;
using PulseOffice.Exchange;
using Xunit;

namespace PulseOffice.Robustness.Tests.Exchange;

public sealed class HostileExchangePackageTests
{
    private readonly JsonExchangeSerializer _serializer = new(new LoadedExchangeTypeCatalog(), new ExchangeContentGuard());
    private readonly CellGridConversions _conversions = new();

    private ExchangePackage? Read(string json) => _serializer.Read(Encoding.UTF8.GetBytes(json));

    private string Grid(int rows, int columns, string cells, int firstRow = 0) =>
        "{\"Version\":1,\"Id\":\"x\",\"Content\":{\"$type\":\"PulseOffice.Exchange.CellRangeContent\",\"SheetName\":\"S\",\"FirstRow\":" + firstRow +
        ",\"RowCount\":" + rows + ",\"ColumnCount\":" + columns + ",\"Rows\":" + cells + "}}";

    [Theory]
    [InlineData("{\"Version\":1,\"Id\":\"x\",\"Content\":{\"$type\":\"System.Diagnostics.Process\"}}")]
    [InlineData("{\"Version\":1,\"Id\":\"x\",\"Content\":{\"$type\":\"Unknown.Type\",\"A\":1}}")]
    [InlineData("{\"Version\":2,\"Id\":\"x\"}")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"Version\":1,\"Id\":\"x\",\"Content\":{\"$type\":\"PulseOffice.Exchange.CellRangeContent\",\"SheetName\":\"S\",\"RowCount\":1,\"ColumnCount\":1,\"Rows\":null}}")]
    [InlineData("{\"Version\":1,\"Id\":\"x\",\"Content\":{\"$type\":\"PulseOffice.Exchange.CellRangeContent\",\"SheetName\":\"S\",\"RowCount\":1,\"ColumnCount\":1,\"Rows\":[null]}}")]
    [InlineData("{\"Version\":1,\"Id\":\"x\",\"Content\":{\"$type\":\"PulseOffice.Exchange.ObjectsContent\",\"Objects\":[null]}}")]
    [InlineData("{\"Version\":1,\"Id\":\"x\",\"Content\":{\"$type\":\"PulseOffice.Exchange.ObjectsContent\",\"Objects\":null}}")]
    public void MalformedPackagesAreRejected(string json) => Assert.Null(Read(json));

    [Fact]
    public void DeepNestingIsRejected()
    {
        var json = new StringBuilder("{\"Version\":1,\"Id\":\"x\",\"Origin\":");
        json.Append('[', 5000).Append(']', 5000).Append('}');
        Assert.Null(Read(json.ToString()));
    }

    [Theory]
    [InlineData(2_000_000_000, 1)]
    [InlineData(1, 2_000_000_000)]
    [InlineData(-5, 1)]
    [InlineData(100_000, 100_000)]
    public void DeclaredDimensionsBeyondTheCellsAreRejected(int rows, int columns) =>
        Assert.Null(Read(Grid(rows, columns, "[[{\"Kind\":\"Text\",\"Text\":\"a\"}]]")));

    [Fact]
    public void NegativeOriginIsRejected() =>
        Assert.Null(Read(Grid(1, 1, "[[{\"Kind\":\"Text\",\"Text\":\"a\"}]]", firstRow: -3)));

    [Fact]
    public void HostileColoursDoNotBreakOutOfTheHtmlAttribute()
    {
        var package = Read(Grid(1, 1, "[[{\"Kind\":\"Text\",\"Text\":\"a\",\"ColorHex\":\"\\\"><img \",\"BackgroundHex\":\"red;x:y\"}]]"));
        Assert.NotNull(package);
        var html = _conversions.Html((CellRangeContent)package.Content!);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("red;x", html, StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedPayloadIsRejectedWithoutParsing()
    {
        var huge = new byte[JsonExchangeSerializer.MaximumBytes + 1];
        Assert.Null(_serializer.Read(huge));
    }

    [Fact]
    public void RandomBytesNeverThrow()
    {
        var random = new Random(1234);
        var valid = Encoding.UTF8.GetBytes(Grid(1, 2, "[[{\"Kind\":\"Number\",\"Number\":1,\"Formula\":\"=WEBSERVICE(\\\"http://x\\\")\"},{\"Kind\":\"Text\",\"Text\":\"b\"}]]"));
        for (var round = 0; round < 2000; round++)
        {
            var mutated = (byte[])valid.Clone();
            for (var flip = 0; flip < 1 + random.Next(4); flip++) mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
            var package = _serializer.Read(mutated);
            if (package?.Content is CellRangeContent grid)
            {
                _conversions.Html(grid);
                _conversions.Tabular(grid);
                _conversions.Table(grid, "t");
            }
        }
    }
}
