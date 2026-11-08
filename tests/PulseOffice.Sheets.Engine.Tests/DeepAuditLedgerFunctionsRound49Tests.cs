using System.Globalization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditLedgerFunctionsRound49Tests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private CellValue Compute(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        var cell = new SheetCell(0, new CellAddress(50, 5));
        engine.SetFormula(cell, formula);
        return engine.GetValue(cell);
    }

    [Theory]
    [InlineData("=DATEDIF(DATE(2020,1,15),DATE(2021,3,10),\"Y\")", 1)]
    [InlineData("=DATEDIF(DATE(2020,1,15),DATE(2021,3,10),\"M\")", 13)]
    [InlineData("=DATEDIF(DATE(2020,1,15),DATE(2021,3,10),\"YM\")", 1)]
    [InlineData("=DATEDIF(DATE(2020,1,15),DATE(2021,3,10),\"YD\")", 54)]
    [InlineData("=DATEDIF(DATE(2020,1,15),DATE(2021,3,10),\"MD\")", 23)]
    [InlineData("=EDATE(DATE(2021,1,31),1)", 44255)]
    [InlineData("=EDATE(DATE(2020,3,31),-1)", 43890)]
    [InlineData("=EOMONTH(DATE(2020,1,15),1)", 43890)]
    [InlineData("=EOMONTH(DATE(2020,1,15),-13)", 43465)]
    [InlineData("=DATE(1900,3,1)", 61)]
    [InlineData("=WEEKDAY(DATE(1900,3,1))", 5)]
    [InlineData("=NETWORKDAYS.INTL(DATE(2024,1,1),DATE(2024,1,31),\"0000011\")", 23)]
    [InlineData("=NETWORKDAYS.INTL(DATE(2024,1,1),DATE(2024,1,31),11)", 27)]
    [InlineData("=NETWORKDAYS.INTL(DATE(2024,1,31),DATE(2024,1,1))", -23)]
    [InlineData("=WORKDAY.INTL(DATE(2024,1,5),1,\"0000011\")", 45299)]
    [InlineData("=WORKDAY.INTL(DATE(2024,1,8),-1)", 45296)]
    [InlineData("=WORKDAY(DATE(2024,1,6),0)", 45297)]
    [InlineData("=XNPV(0.09,{-10000,2750,4250,3250,2750},{39448,39508,39751,39859,39904})", 2086.6476)]
    [InlineData("=XIRR({-10000,2750,4250,3250,2750},{39448,39508,39751,39859,39904})", 0.3733625)]
    [InlineData("=HYPGEOM.DIST(1,4,8,20,TRUE)", 0.4654283)]
    [InlineData("=HYPGEOM.DIST(1,4,8,20,FALSE)", 0.3632611)]
    [InlineData("=NEGBINOM.DIST(10,5,0.25,TRUE)", 0.3135141)]
    [InlineData("=NEGBINOM.DIST(10,5,0.25,FALSE)", 0.0550487)]
    [InlineData("=BETA.INV(0.685470581,8,10,1,3)", 2)]
    [InlineData("=GAMMA.INV(0.068094,9,2)", 10.0000112)]
    [InlineData("=T.INV.2T(0.546449,60)", 0.6065329)]
    [InlineData("=F.DIST(15.2069,6,4,TRUE)", 0.99)]
    [InlineData("=F.DIST(15.2069,6,4,FALSE)", 0.0012238)]
    [InlineData("=PRICE(DATE(2008,2,15),DATE(2017,11,15),0.0575,0.065,100,2,0)", 94.6343616)]
    [InlineData("=YIELD(DATE(2008,2,15),DATE(2016,11,15),0.0575,95.04287,100,2,0)", 0.065)]
    [InlineData("=ACCRINT(DATE(2008,3,1),DATE(2008,8,31),DATE(2008,5,1),0.1,1000,2,0)", 16.6666667)]
    [InlineData("=ODDFPRICE(DATE(2008,11,11),DATE(2021,3,1),DATE(2008,10,15),DATE(2009,3,1),0.0785,0.0625,100,2,1)", 113.597717)]
    [InlineData("=ODDLPRICE(DATE(2008,2,7),DATE(2008,6,15),DATE(2007,10,15),0.0375,0.0405,100,2,0)", 99.8782860)]
    [InlineData("=ODDLYIELD(DATE(2008,4,20),DATE(2008,6,15),DATE(2007,12,24),0.0375,99.875,100,2,0)", 0.0451922)]
    [InlineData("=SUM(TAKE(SEQUENCE(5),-2))", 9)]
    [InlineData("=SUM(DROP(SEQUENCE(5),2))", 12)]
    [InlineData("=SUM(DROP(SEQUENCE(5),-4))", 1)]
    [InlineData("=COLUMNS(WRAPROWS(SEQUENCE(5),2))", 2)]
    [InlineData("=ROWS(WRAPROWS(SEQUENCE(5),2))", 3)]
    [InlineData("=SUM(WRAPROWS(SEQUENCE(5),2,0))", 15)]
    [InlineData("=SUM(CHOOSECOLS(SEQUENCE(2,3),-1))", 9)]
    [InlineData("=ROWS(VSTACK(SEQUENCE(2),SEQUENCE(1,3)))", 3)]
    [InlineData("=SUM(IFERROR(VSTACK(SEQUENCE(2),SEQUENCE(1,3)),100))", 409)]
    [InlineData("=SUM(SCAN(0,SEQUENCE(4),LAMBDA(a,b,a+b)))", 20)]
    [InlineData("=REDUCE(0,SEQUENCE(4),LAMBDA(a,b,a+b))", 10)]
    [InlineData("=SUM(MAKEARRAY(2,3,LAMBDA(r,c,r*c)))", 18)]
    [InlineData("=SUM(BYROW(SEQUENCE(2,3),LAMBDA(r,SUM(r))))", 21)]
    [InlineData("=SUM(BYCOL(SEQUENCE(2,3),LAMBDA(c,MAX(c))))", 15)]
    [InlineData("=SUM(MAP(SEQUENCE(3),SEQUENCE(3),LAMBDA(a,b,a*b)))", 14)]
    [InlineData("=LET(x,2,y,x*3,x+y)", 8)]
    [InlineData("=XMATCH(4,{1,3,5},-1)", 2)]
    [InlineData("=XMATCH(4,{1,3,5},1)", 3)]
    [InlineData("=XMATCH(5,{1,3,5,5},0,-1)", 4)]
    [InlineData("=XMATCH(\"b*\",{\"a\",\"bc\"},2)", 2)]
    [InlineData("=XLOOKUP(4,{1,3,5},{10,30,50},,1)", 50)]
    [InlineData("=XLOOKUP(4,{1,3,5},{10,30,50},,-1)", 30)]
    [InlineData("=XLOOKUP(9,{1,3,5},{10,30,50},-7)", -7)]
    [InlineData("=XLOOKUP(3,{1,3,5,7},{10,30,50,70},,0,2)", 30)]
    [InlineData("=SUM(UNIQUE({1|2|2|3}))", 6)]
    [InlineData("=ROWS(UNIQUE({1|2|2|3},,TRUE))", 2)]
    [InlineData("=INDEX(SORTBY({1|2|3},{3|1|2}),1)", 2)]
    [InlineData("=INDEX(SORT({3|1|2},1,-1),1)", 3)]
    [InlineData("=SUM(FILTER({1|2|3|4},{1|0|1|0}))", 4)]
    [InlineData("=COLUMNS(TEXTSPLIT(\"a,b,,c\",\",\"))", 4)]
    [InlineData("=COLUMNS(TEXTSPLIT(\"a,b,,c\",\",\",,TRUE))", 3)]
    [InlineData("=ROWS(TEXTSPLIT(\"a,b;c\",\",\",\";\"))", 2)]
    [InlineData("=COLUMNS(TEXTSPLIT(\"a,b;c\",\",\",\";\"))", 2)]
    [InlineData("=SUM(IFERROR(TEXTSPLIT(\"1,2;3\",\",\",\";\")*1,10))", 16)]
    [InlineData("=SUM(TEXTSPLIT(\"1,2;3\",\",\",\";\",,,0)*1)", 6)]
    [InlineData("=COLUMNS(TEXTSPLIT(\"aXbxc\",\"x\",,,1))", 3)]
    [InlineData("=COLUMNS(TEXTSPLIT(\"aXbxc\",\"x\"))", 2)]
    [InlineData("=COLUMNS(TEXTSPLIT(\"a, b,c\",{\",\",\", \"}))", 3)]
    [InlineData("=SUM(SEQUENCE(2,2,0,0.5))", 3)]
    [InlineData("=SUM(TRUE,1)", 2)]
    [InlineData("=TRUE+1", 2)]
    [InlineData("=SUM(\"3\",1)", 4)]
    [InlineData("=AVERAGE(TRUE,3)", 2)]
    [InlineData("=COUNT(TRUE,\"1\",\"x\")", 2)]
    [InlineData("=COUNTA(\"\",1)", 2)]
    [InlineData("=T.DIST(1,1,TRUE)", 0.75)]
    [InlineData("=BINOM.INV(6,0.5,0.75)", 4)]
    [InlineData("=CHISQ.INV.RT(0.05,10)", 18.3070381)]
    [InlineData("=DAYS360(DATE(2011,1,30),DATE(2011,12,31))", 330)]
    [InlineData("=DAYS360(DATE(2011,2,28),DATE(2011,3,31))", 30)]
    [InlineData("=YEARFRAC(DATE(2012,1,1),DATE(2012,7,30),1)", 0.5765027)]
    [InlineData("=YEARFRAC(DATE(2012,1,1),DATE(2012,7,30),0)", 0.5805556)]
    [InlineData("=COUPDAYS(DATE(2011,1,25),DATE(2011,11,15),2,1)", 181)]
    [InlineData("=COUPPCD(DATE(2011,1,25),DATE(2011,11,15),2,1)", 40497)]
    [InlineData("=ISOWEEKNUM(DATE(2021,1,3))", 53)]
    [InlineData("=WEEKNUM(DATE(2021,1,3),21)", 53)]
    [InlineData("=MROUND(-10,-3)", -9)]
    [InlineData("=CEILING.MATH(-4.5,1,1)", -5)]
    [InlineData("=ROUND(2.5,0)", 3)]
    [InlineData("=ROUND(-2.5,0)", -3)]
    [InlineData("=ROUND(1.005,2)", 1.01)]
    [InlineData("=MOD(-3,2)", 1)]
    [InlineData("=QUOTIENT(-7,2)", -3)]
    public void NumericResultsMatchTheDocumentedValues(string formula, double expected)
    {
        var value = Compute(formula);
        Assert.True(value.Kind == ValueKind.Number, $"{formula} gave {value}");
        Assert.True(Math.Abs(value.AsNumber() - expected) <= 1e-6 * Math.Max(1, Math.Abs(expected)), $"{formula} gave {value.AsNumber().ToString("R", CultureInfo.InvariantCulture)}, expected {expected}");
    }

    [Theory]
    [InlineData("=\"\"+1", "#VALUE!")]
    [InlineData("=XIRR({1,2},{39448,39508})", "#NUM!")]
    [InlineData("=SUM(WRAPROWS(SEQUENCE(5),2))", "#N/A")]
    [InlineData("=DATEDIF(DATE(2021,1,1),DATE(2020,1,1),\"D\")", "#NUM!")]
    [InlineData("=EDATE(1,200000)", "#NUM!")]
    [InlineData("=EOMONTH(1,-200000)", "#NUM!")]
    [InlineData("=TEXTSPLIT(\"a,b\",\",\",,,2)", "#VALUE!")]
    [InlineData("=HYPGEOM.DIST(5,4,8,20,TRUE)", "#NUM!")]
    public void ErrorResultsMatchTheDocumentedErrors(string formula, string expected)
    {
        var value = Compute(formula);
        Assert.True(value.Kind == ValueKind.Error, $"{formula} gave {value}");
        Assert.Equal(expected, value.ToString());
    }

    [Theory]
    [InlineData("=FIXED(1234.567,1)", "1,234.6")]
    [InlineData("=FIXED(-1234.567,-2,TRUE)", "-1200")]
    [InlineData("=TEXT(DATE(2024,3,5),\"yyyy-mm-dd\")", "2024-03-05")]
    [InlineData("=TEXT(0.256,\"0.0%\")", "25.6%")]
    [InlineData("=TEXT(1234.5,\"#,##0.00\")", "1,234.50")]
    [InlineData("=CONCAT(TEXTSPLIT(\"a1b2c\",{\"1\",\"2\"}))", "abc")]
    [InlineData("=TEXTBEFORE(\"a-b-c\",\"-\",-1)", "a-b")]
    [InlineData("=TEXTAFTER(\"a-b-c\",\"-\",2)", "c")]
    public void TextResultsMatchTheDocumentedValues(string formula, string expected)
    {
        var value = Compute(formula);
        Assert.True(value.Kind == ValueKind.Text, $"{formula} gave {value}");
        Assert.Equal(expected, value.AsText());
    }
}
