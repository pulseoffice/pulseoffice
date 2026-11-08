using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditLedgerFunctionsRound18Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly CellValue NumberError = CellValue.FromError(CellError.Number);
    private static readonly CellValue ValueError = CellValue.FromError(CellError.Value);

    private static SheetCell At(int row, int column) => new(0, new CellAddress(row, column));

    private CellValue? Compute(string formula, TimeSpan limit, Action<CalculationEngine>? prepare = null, string? culture = null)
    {
        var (_, engine) = fixture.CreateWorkbook();
        CellValue? result = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (culture is not null) CultureInfo.CurrentCulture = new CultureInfo(culture);
                prepare?.Invoke(engine);
                engine.SetFormula(At(1200, 3), formula);
                result = engine.GetValue(At(1200, 3));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, 16 * 1024 * 1024) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(limit), $"{formula} did not finish within {limit}");
        Assert.True(failure is null, $"{formula} threw {failure}");
        return result;
    }

    private CellValue? Compute(string formula, Action<CalculationEngine>? prepare = null, string? culture = null) =>
        Compute(formula, TimeSpan.FromSeconds(10), prepare, culture);

    private double Number(string formula, Action<CalculationEngine>? prepare = null, string? culture = null)
    {
        var result = Compute(formula, prepare, culture);
        Assert.True(result is { Kind: ValueKind.Number }, $"{formula} gave {result}");
        return result!.Value.AsNumber();
    }

    private sealed class FakeWeb(string content) : IWebResourceAccess
    {
        public int Calls { get; private set; }

        public string? Fetch(string url)
        {
            Calls++;
            return content;
        }
    }

    [Theory]
    [InlineData("=ROWS(FILTERXML(\"<a><b>1</b><b>2</b><b>3</b></a>\";\"//b\"))", 3)]
    [InlineData("=SUM(FILTERXML(\"<a><b>1</b><b>2</b><b>3</b></a>\";\"//b\"))", 6)]
    [InlineData("=INDEX(FILTERXML(\"<a><b>x</b><b>7</b></a>\";\"//b\");2)", 7)]
    public void FilterXmlReturnsEveryMatchingNodeAsAColumn(string formula, double expected) =>
        Assert.Equal(expected, Number(formula));

    [Fact]
    public void FilterXmlKeepsNonFiniteWordsAsText() =>
        Assert.Equal(CellValue.Boolean(true), Compute("=ISTEXT(FILTERXML(\"<a>NaN</a>\";\"/a\"))"));

    [Fact]
    public void FilterXmlNonFiniteXPathNumberIsAValueError() =>
        Assert.Equal(ValueError, Compute("=FILTERXML(\"<a/>\";\"number(/a)\")"));

    [Fact]
    public void FilterXmlRefusesExternalEntities() =>
        Assert.Equal(ValueError, Compute("=FILTERXML(\"<!DOCTYPE a [<!ENTITY x SYSTEM \"\"file:///etc/hosts\"\">]><a>&x;</a>\";\"/a\")"));

    [Fact]
    public void WebServiceRefusesUrlsLongerThanExcelAllowsWithoutFetching()
    {
        var web = new FakeWeb("ok");
        var url = "http://example.com/" + new string('a', 2100);
        Assert.Equal(ValueError, Compute($"=WEBSERVICE(\"{url}\")", engine => engine.Web = web));
        Assert.Equal(0, web.Calls);
    }

    [Fact]
    public void WebServiceAnswerLongerThanACellIsAValueError() =>
        Assert.Equal(ValueError, Compute("=WEBSERVICE(\"http://example.com\")", engine => engine.Web = new FakeWeb(new string('x', 40000))));

    [Fact]
    public void WebServiceShortAnswerIsReturned() =>
        Assert.Equal(CellValue.Text("ok"), Compute("=WEBSERVICE(\"http://example.com\")", engine => engine.Web = new FakeWeb("ok")));

    [Theory]
    [InlineData("=NUMBERVALUE(\"1.5\";\"\")")]
    [InlineData("=NUMBERVALUE(\"1.5\";\".\";\"\")")]
    [InlineData("=NUMBERVALUE(\"1.5\";\".\";\".\")")]
    public void NumberValueWithAnEmptyOrClashingSeparatorIsAValueError(string formula) =>
        Assert.Equal(ValueError, Compute(formula));

    [Theory]
    [InlineData("=NUMBERVALUE(\"\")", 0)]
    [InlineData("=NUMBERVALUE(\"1,234.5\";\".\";\",\")", 1234.5)]
    [InlineData("=NUMBERVALUE(\"1.234,5\";\",xx\";\".yy\")", 1234.5)]
    [InlineData("=NUMBERVALUE(\"12%%\";\".\")", 0.0012)]
    public void NumberValueReadsLikeExcel(string formula, double expected) =>
        Assert.Equal(expected, Number(formula), 12);

    [Fact]
    public void DollarWithManyDecimalsIsText() =>
        Assert.Equal(CellValue.Boolean(true), Compute("=ISTEXT(DOLLAR(1.5;20))"));

    [Fact]
    public void SumProductOfDifferentShapesIsAValueError() =>
        Assert.Equal(ValueError, Compute("=SUMPRODUCT({1|2};{1;2})"));

    [Fact]
    public void SumProductOfSameShapes() => Assert.Equal(11, Number("=SUMPRODUCT({1|2};{3|4})"));

    [Theory]
    [InlineData("=CORREL({1E+100|2E+100|4E+100};{1E+100|2E+100|4E+100})")]
    [InlineData("=PEARSON({1E+100|2E+100|4E+100};{1E+100|2E+100|4E+100})")]
    [InlineData("=RSQ({1E+100|2E+100|4E+100};{1E+100|2E+100|4E+100})")]
    public void CorrelationOfLargeValuesDoesNotOverflow(string formula) =>
        Assert.Equal(1, Number(formula), 12);

    [Theory]
    [InlineData("=SUM(TREND({1|2|3|5};{1;2|2;4|3;6|4;8}))", 11)]
    [InlineData("=INDEX(LINEST({1|2|3|5};{1;2|2;4|3;6|4;8};TRUE;TRUE);4;2)", 2)]
    [InlineData("=SUM(LINEST({1|2|3|5};{1;2|2;4|3;6|4;8}))", 1.3 - 0.5)]
    [InlineData("=INDEX(LINEST({3|3|3};{5|5|5});1;2)", 3)]
    [InlineData("=INDEX(LINEST({3|3|3};{5|5|5});1;1)", 0)]
    public void RegressionDropsCollinearColumnsLikeExcel(string formula, double expected) =>
        Assert.Equal(expected, Number(formula), 9);

    [Fact]
    public void RegressionOfTinyValuesIsNotSingular() =>
        Assert.Equal(2, Number("=INDEX(LINEST({2E-20|4E-20|6E-20};{1E-20|2E-20|3E-20});1;1)"), 9);

    [Fact]
    public void EtsForecastFarBeyondTheDataDoesNotCrash()
    {
        var result = Compute("=FORECAST.ETS.ADD(10^15+0.5;{1|2|3|4|5|6|7|8|9|10|11|12};{1|2|3|4|5|6|7|8|9|10|11|12};4)");
        Assert.True(result is { Kind: ValueKind.Number } || result == NumberError, $"gave {result}");
    }

    [Fact]
    public void EtsSeasonalityAboveExcelLimitIsANumberError() =>
        Assert.Equal(NumberError, Compute("=FORECAST.ETS.ADD(13;{1|2|3|4|5|6|7|8|9|10|11|12};{1|2|3|4|5|6|7|8|9|10|11|12};8761)"));

    [Theory]
    [InlineData("=NETWORKDAYS(DATE(2024;1;1);DATE(2024;12;31))", 262)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;1);DATE(2024;1;31))", 23)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;31);DATE(2024;1;1))", -23)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;1);DATE(2024;1;31);DATE(2024;1;1))", 22)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;31);DATE(2024;1;1);DATE(2024;1;1))", -22)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;1);DATE(2024;1;31);DATE(2024;1;6))", 23)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;1);DATE(2024;1;31);{45293;45293;45400})", 22)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;6);DATE(2024;1;7))", 0)]
    [InlineData("=NETWORKDAYS(DATE(2024;1;3);DATE(2024;1;3))", 1)]
    [InlineData("=NETWORKDAYS.INTL(DATE(2024;1;1);DATE(2024;1;31);11)", 27)]
    [InlineData("=NETWORKDAYS.INTL(DATE(2024;1;1);DATE(2024;1;31);\"1111111\")", 0)]
    [InlineData("=NETWORKDAYS.INTL(DATE(2024;1;1);DATE(2024;1;31);\"0000000\")", 31)]
    [InlineData("=WORKDAY(DATE(2024;1;5);1)-DATE(2024;1;8)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;8);-1)-DATE(2024;1;5)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;1);10;DATE(2024;1;2))-DATE(2024;1;16)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;16);-10;DATE(2024;1;2))-DATE(2024;1;1)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;6);0)-DATE(2024;1;6)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;6);5)-DATE(2024;1;12)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;6);-5)-DATE(2024;1;1)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;1);261)-DATE(2024;12;31)", 0)]
    [InlineData("=WORKDAY(DATE(2024;1;1);259;{45293;45294})-DATE(2024;12;31)", 0)]
    [InlineData("=WORKDAY.INTL(DATE(2024;1;1);30;\"0000011\")-DATE(2024;2;12)", 0)]
    [InlineData("=WORKDAY.INTL(DATE(2024;1;1);6;11)-DATE(2024;1;8)", 0)]
    [InlineData("=WORKDAY.INTL(DATE(2024;1;1);7;\"0000000\")-DATE(2024;1;8)", 0)]
    public void WorkdayArithmeticMatchesExcel(string formula, double expected) =>
        Assert.Equal(expected, Number(formula));

    [Fact]
    public void WorkdayArithmeticOverTheWholeDateRangeIsImmediate()
    {
        var result = Compute("=SUMPRODUCT(NETWORKDAYS(SEQUENCE(300);2958465))", TimeSpan.FromSeconds(3));
        Assert.True(result is { Kind: ValueKind.Number }, $"gave {result}");
        var far = Compute("=WORKDAY(1;2000000)", TimeSpan.FromSeconds(3));
        Assert.True(far is { Kind: ValueKind.Number }, $"gave {far}");
    }

    [Fact]
    public void WorkdayBeyondTheCalendarIsANumberError() =>
        Assert.Equal(NumberError, Compute("=WORKDAY(1;1E+12)", TimeSpan.FromSeconds(3)));

    private static void Database(CalculationEngine engine)
    {
        engine.SetValue(At(0, 0), CellValue.Text("ID"));
        engine.SetValue(At(0, 1), CellValue.Text("Sales"));
        (double Id, double Sales)[] rows = [(1, 10), (2, 40), (3, 20), (4, 50)];
        for (var index = 0; index < rows.Length; index++)
        {
            engine.SetValue(At(index + 1, 0), CellValue.Number(rows[index].Id));
            engine.SetValue(At(index + 1, 1), CellValue.Number(rows[index].Sales));
        }
    }

    [Fact]
    public void DatabaseHeadersMatchRegardlessOfTheCurrentCulture() =>
        Assert.Equal(10, Number("=DSUM(A1:B5;\"id\";D1:D2)", engine =>
        {
            Database(engine);
            engine.SetValue(At(0, 3), CellValue.Text("sales"));
            engine.SetValue(At(1, 3), CellValue.Text(">0"));
        }, "tr-TR"));

    [Fact]
    public void DatabaseCalculatedCriterionIsEvaluatedForEveryRecord() =>
        Assert.Equal(90, Number("=DSUM(A1:B5;\"Sales\";D1:D2)", engine =>
        {
            Database(engine);
            engine.SetValue(At(0, 3), CellValue.Text("Above average"));
            engine.SetFormula(At(1, 3), "=B2>AVERAGE($B$2:$B$5)");
        }));

    [Fact]
    public void DatabaseCalculatedCriterionWithABlankHeader() =>
        Assert.Equal(2, Number("=DCOUNT(A1:B5;\"ID\";D1:D2)", engine =>
        {
            Database(engine);
            engine.SetFormula(At(1, 3), "=A2>2");
        }));

    [Theory]
    [InlineData("=IMSUM(\"1+i\";\"1+j\")")]
    [InlineData("=IMPRODUCT(\"2i\";\"3j\")")]
    [InlineData("=IMSUB(\"2i\";\"3j\")")]
    [InlineData("=IMDIV(\"2i\";\"3j\")")]
    public void ComplexArithmeticWithMixedSuffixesIsAValueError(string formula) =>
        Assert.Equal(ValueError, Compute(formula));

    [Theory]
    [InlineData("=IMSUM(\"2j\";\"3j\")", "5j")]
    [InlineData("=IMSUM(\"1+2j\";\"3\")", "4+2j")]
    [InlineData("=IMSUB(\"3+2j\";\"1+j\")", "2+j")]
    [InlineData("=IMPRODUCT(\"2j\";\"3j\")", "-6")]
    [InlineData("=IMSUM(\"1\";\"2\")", "3")]
    [InlineData("=IMSUM(\"1+i\";\"2\")", "3+i")]
    public void ComplexArithmeticKeepsTheSuffixOfItsOperands(string formula, string expected) =>
        Assert.Equal(CellValue.Text(expected), Compute(formula));

    [Fact]
    public void ComplexDivisionByZeroIsANumberError() =>
        Assert.Equal(NumberError, Compute("=IMDIV(\"1+i\";\"0\")"));
}
