using PulseOffice.Formats.Odf;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class RegexFunctionNamesRound53Tests
{
    private readonly XlsxFormulaSyntax _xlsx = new();
    private readonly OdfFormulaSyntax _odf = new();

    [Theory]
    [InlineData("=REGEXTEST(A1;\"[0-9]+\";1)", "_xlfn.REGEXTEST(A1,\"[0-9]+\",1)", "COM.MICROSOFT.REGEXTEST(")]
    [InlineData("=REGEXEXTRACT(A1;\"(a)(b)\";2)", "_xlfn.REGEXEXTRACT(A1,\"(a)(b)\",2)", "COM.MICROSOFT.REGEXEXTRACT(")]
    [InlineData("=REGEXREPLACE(A1;\"x\";\"y\";-1;1)", "_xlfn.REGEXREPLACE(A1,\"x\",\"y\",-1,1)", "COM.MICROSOFT.REGEXREPLACE(")]
    public void ExcelRegularExpressionFunctionsCrossBothFormats(string formula, string xlsx, string ods)
    {
        Assert.Equal(xlsx, _xlsx.ToOpenXml(formula));
        Assert.Equal(formula, _xlsx.ToInternal(xlsx));

        var odf = _odf.ToOdf(formula);
        Assert.Contains(ods, odf);
        Assert.Equal(formula, _odf.ToInternal(odf));
    }
}
