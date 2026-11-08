using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditLedgerFunctionsRound51Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly CellValue?[] ColumnA =
    [
        CellValue.Number(10), CellValue.Text("apple"), CellValue.Text("Banana"), CellValue.Number(20), null, CellValue.Number(30),
    ];

    private CellValue Compute(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 0; row < ColumnA.Length; row++)
        {
            if (ColumnA[row] is { } value)
            {
                engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), value);
            }

            engine.SetValue(new SheetCell(0, new CellAddress(row, 1)), CellValue.Number(row + 1));
            engine.SetValue(new SheetCell(0, new CellAddress(row, 2)), CellValue.Number(45292 + row));
        }

        engine.SetValue(new SheetCell(0, new CellAddress(0, 3)), CellValue.Number(5));
        engine.SetValue(new SheetCell(0, new CellAddress(1, 3)), CellValue.Number(1));
        engine.SetValue(new SheetCell(0, new CellAddress(2, 3)), CellValue.Number(3));
        engine.SetFormula(new SheetCell(0, new CellAddress(3, 3)), "=1/0");
        engine.SetValue(new SheetCell(0, new CellAddress(4, 3)), CellValue.Number(7));
        var cell = new SheetCell(0, new CellAddress(50, 5));
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell);
    }

    [Theory]
    [InlineData("=MATCH(\"b*\",A1:A6,0)", 3)]
    [InlineData("=MATCH(\"?pple\",A1:A6,0)", 2)]
    [InlineData("=MATCH(25,{10,20,30},1)", 2)]
    [InlineData("=MATCH(25,{30,20,10},-1)", 1)]
    [InlineData("=INDEX(B1:B6,MATCH(\"banana\",A1:A6,0))", 3)]
    [InlineData("=VLOOKUP(\"ban*\",A1:B6,2,FALSE)", 3)]
    [InlineData("=HLOOKUP(2,{1,2,3|10,20,30},2,TRUE)", 20)]
    [InlineData("=SUM(OFFSET(B1,1,0,3,1))", 9)]
    [InlineData("=SUM(INDIRECT(\"R2C2:R4C2\",FALSE))", 9)]
    [InlineData("=SUM(INDIRECT(\"Sheet1!B1:B2\"))", 3)]
    [InlineData("=XLOOKUP(5,{7,5,3,1},{70,50,30,10},,0,-2)", 50)]
    [InlineData("=XLOOKUP(4,{1,3,5,7},{10,30,50,70},,-1,2)", 30)]
    [InlineData("=SUMIFS(B1:B6,A1:A6,\">15\")", 10)]
    [InlineData("=SUMIFS(B1:B6,A1:A6,\"<>apple\")", 19)]
    [InlineData("=COUNTIFS(A1:A6,\"\")", 1)]
    [InlineData("=COUNTIFS(A1:A6,\"<>\")", 5)]
    [InlineData("=COUNTIFS(A1:A6,\"*\")", 2)]
    [InlineData("=COUNTIFS(A1:A6,\"=\")", 1)]
    [InlineData("=COUNTIF(C1:C6,\">=\"&DATE(2024,1,4))", 3)]
    [InlineData("=COUNTIF(A1:A6,\"~*\")", 0)]
    [InlineData("=AVERAGEIFS(B1:B6,A1:A6,\">=10\",C1:C6,\"<45297\")", 2.5)]
    [InlineData("=SUBTOTAL(9,B1:B6)", 21)]
    [InlineData("=SUBTOTAL(109,B1:B6)", 21)]
    [InlineData("=SUBTOTAL(2,A1:A6)", 3)]
    [InlineData("=SUBTOTAL(3,A1:A6)", 5)]
    [InlineData("=AGGREGATE(9,6,D1:D5)", 16)]
    [InlineData("=AGGREGATE(14,6,D1:D5,2)", 5)]
    [InlineData("=AGGREGATE(4,6,D1:D5)", 7)]
    [InlineData("=AGGREGATE(12,6,D1:D5)", 4)]
    [InlineData("=SUMPRODUCT((B1:B6>2)*(B1:B6<6))", 3)]
    [InlineData("=SUMPRODUCT(--(B1:B6>3),B1:B6)", 15)]
    [InlineData("=SUMPRODUCT({1,2},{3,4})", 11)]
    [InlineData("=NUMBERVALUE(\"1,234.5\",\".\",\",\")", 1234.5)]
    [InlineData("=NUMBERVALUE(\"1.234,5\",\",\",\".\")", 1234.5)]
    [InlineData("=NUMBERVALUE(\"2.5%\",\".\")", 0.025)]
    [InlineData("=NUMBERVALUE(\"3%%\",\".\")", 0.0003)]
    [InlineData("=UNICODE(\"😀\")", 128512)]
    [InlineData("=LEN(TRIM(\"  a   b  \"))", 3)]
    [InlineData("=LEN(CLEAN(CHAR(7)&\"a\"&CHAR(10)))", 1)]
    [InlineData("=MROUND(-10,-3)", -9)]
    [InlineData("=FLOOR.XCL(-2.5,-2)", -2)]
    [InlineData("=CEILING.XCL(-2.5,2)", -2)]
    [InlineData("=FLOOR.MATH(-5.5,2,-1)", -4)]
    [InlineData("=CEILING.PRECISE(-4.3,2)", -4)]
    [InlineData("=FLOOR.PRECISE(-3.2,-1)", -4)]
    [InlineData("=ISO.CEILING(4.3,-2)", 6)]
    [InlineData("=DECIMAL(\"FF\",16)", 255)]
    [InlineData("=DECIMAL(\"zap\",36)", 45745)]
    [InlineData("=ARABIC(\"MCMXCIX\")", 1999)]
    [InlineData("=ARABIC(\"\")", 0)]
    [InlineData("=GCD(24,36,48)", 12)]
    [InlineData("=LCM(4,6,10)", 60)]
    [InlineData("=GCD(0,0)", 0)]
    [InlineData("=MDETERM({1,2|2,4})", 0)]
    [InlineData("=MDETERM({3,6,1|1,1,0|3,10,2})", 1)]
    [InlineData("=BIN2DEC(\"1111111111\")", -1)]
    [InlineData("=HEX2DEC(\"FFFFFFFFFF\")", -1)]
    [InlineData("=OCT2DEC(\"7777777770\")", -8)]
    [InlineData("=IMABS(\"3+4i\")", 5)]
    [InlineData("=IMREAL(IMPRODUCT(\"3+4i\",\"5-3i\"))", 27)]
    [InlineData("=CONVERT(1,\"lbm\",\"kg\")", 0.45359237)]
    [InlineData("=CONVERT(68,\"F\",\"C\")", 20)]
    [InlineData("=CONVERT(1,\"km\",\"mi\")", 0.62137119)]
    [InlineData("=CONVERT(1,\"kbyte\",\"bit\")", 8000)]
    [InlineData("=CONVERT(1,\"kibyte\",\"byte\")", 1024)]
    [InlineData("=CONVERT(1,\"hr\",\"mn\")", 60)]
    [InlineData("=SHEET()", 1)]
    [InlineData("=SHEETS()", 1)]
    [InlineData("=CELL(\"row\",B3)", 3)]
    [InlineData("=SUM(INDIRECT(ADDRESS(2,2,1,TRUE,\"Sheet1\")))", 2)]
    [InlineData("=CELL(\"col\",C3)", 3)]
    public void NumericResultsMatchTheDocumentedValues(string formula, double expected)
    {
        var value = Compute(formula);
        Assert.True(value.Kind == ValueKind.Number, $"{formula} gave {value}");
        Assert.True(Math.Abs(value.AsNumber() - expected) <= 1e-6 * Math.Max(1, Math.Abs(expected)), $"{formula} gave {value.AsNumber().ToString("R", CultureInfo.InvariantCulture)}, expected {expected}");
    }

    [Theory]
    [InlineData("=MATCH(5,{1,3},0)", "#N/A")]
    [InlineData("=INDIRECT(\"Nope!A1\")", "#REF!")]
    [InlineData("=MDETERM({1,2,3|4,5,6})", "#VALUE!")]
    [InlineData("=MINVERSE({1,2|2,4})", "#NUM!")]
    [InlineData("=BIN2DEC(\"12\")", "#NUM!")]
    [InlineData("=BIN2DEC(\"11111111111\")", "#NUM!")]
    [InlineData("=DECIMAL(\"G\",16)", "#NUM!")]
    [InlineData("=BASE(5,1)", "#NUM!")]
    [InlineData("=ROMAN(4000)", "#VALUE!")]
    [InlineData("=GCD(-1,2)", "#NUM!")]
    [InlineData("=LCM(2^53,3)", "#NUM!")]
    [InlineData("=CONVERT(1,\"m\",\"kg\")", "#N/A")]
    [InlineData("=UNICHAR(0)", "#VALUE!")]
    [InlineData("=UNICHAR(55296)", "#N/A")]
    [InlineData("=MROUND(5,-2)", "#NUM!")]
    [InlineData("=FLOOR(2.5,-2)", "#NUM!")]
    [InlineData("=AGGREGATE(9,0,D1:D5)", "#DIV/0!")]
    [InlineData("=SUBTOTAL(12,B1:B6)", "#VALUE!")]
    [InlineData("=OFFSET(A1,-1,0)", "#REF!")]
    [InlineData("=MINVERSE({1,2,3|4,5,6})", "#VALUE!")]
    [InlineData("=MDETERM({1,\"a\"|2,3})", "#VALUE!")]
    public void ErrorResultsMatchTheDocumentedErrors(string formula, string expected)
    {
        var value = Compute(formula);
        Assert.True(value.Kind == ValueKind.Error, $"{formula} gave {value}");
        Assert.Equal(expected, value.ToString());
    }

    [Theory]
    [InlineData("=BASE(255,16,4)", "00FF")]
    [InlineData("=BASE(7,2)", "111")]
    [InlineData("=ROMAN(1999)", "MCMXCIX")]
    [InlineData("=ROMAN(499,4)", "ID")]
    [InlineData("=ROMAN(0)", "")]
    [InlineData("=DEC2BIN(-1)", "1111111111")]
    [InlineData("=DEC2HEX(-1)", "FFFFFFFFFF")]
    [InlineData("=DEC2BIN(3,4)", "0011")]
    [InlineData("=COMPLEX(3,-4)", "3-4i")]
    [InlineData("=COMPLEX(0,1,\"j\")", "j")]
    [InlineData("=IMSUM(\"3+4i\",\"5-3i\")", "8+i")]
    [InlineData("=IMCONJUGATE(\"3+4i\")", "3-4i")]
    [InlineData("=TRIM(\"  a   b  \")", "a b")]
    [InlineData("=UNICHAR(128512)", "😀")]
    [InlineData("=ADDRESS(2,3,4,FALSE)", "R[2]C[3]")]
    [InlineData("=CELL(\"address\",B3)", "$B$3")]
    [InlineData("=CELL(\"type\",A2)", "l")]
    [InlineData("=CELL(\"type\",A5)", "b")]
    [InlineData("=CELL(\"type\",B1)", "v")]
    public void TextResultsMatchTheDocumentedValues(string formula, string expected)
    {
        var value = Compute(formula);
        Assert.True(value.Kind == ValueKind.Text, $"{formula} gave {value}");
        Assert.Equal(expected, value.AsText());
    }

    [Fact]
    public void UnicodeOfALoneSurrogateIsItsCodeUnit()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Text("\uD83D"));
        var cell = new SheetCell(0, new CellAddress(1, 0));
        engine.SetFormula(cell, "=UNICODE(A1)");
        Assert.Equal(0xD83D, engine.GetValue(cell).AsNumber());
    }

    [Fact]
    public void WholeColumnConditionalSumsOverManyRowsStayLinear()
    {
        var (_, engine) = fixture.CreateWorkbook();
        const int rows = 20000;
        for (var row = 0; row < rows; row++)
        {
            engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), CellValue.Number(row % 10));
            engine.SetValue(new SheetCell(0, new CellAddress(row, 1)), CellValue.Number(1));
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var key = 0; key < 10; key++)
        {
            engine.SetFormula(new SheetCell(0, new CellAddress(key, 4)), $"=SUMIFS(B:B,A:A,{key})+COUNTIFS(A:A,\">=\"&{key})*0");
        }

        engine.SetFormula(new SheetCell(0, new CellAddress(20, 4)), "=ROWS(FILTER(A:A,A:A=3))");
        watch.Stop();
        for (var key = 0; key < 10; key++)
        {
            Assert.Equal(rows / 10, engine.GetValue(new SheetCell(0, new CellAddress(key, 4))).AsNumber());
        }

        Assert.Equal(rows / 10, engine.GetValue(new SheetCell(0, new CellAddress(20, 4))).AsNumber());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"whole-column formulas took {watch.Elapsed}");
    }

    [Theory]
    [InlineData("=ISFORMULA(D4)", true)]
    [InlineData("=ISFORMULA(D3)", false)]
    [InlineData("=ISBLANK(A5)", true)]
    public void LogicalResultsMatchTheDocumentedValues(string formula, bool expected)
    {
        var value = Compute(formula);
        Assert.True(value.Kind == ValueKind.Boolean, $"{formula} gave {value}");
        Assert.Equal(expected, value.AsBoolean());
    }
}
