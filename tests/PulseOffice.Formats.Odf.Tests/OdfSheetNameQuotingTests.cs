using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfSheetNameQuotingTests
{
    [Theory]
    [InlineData("of:=[AOO109200.B14]", "='AOO109200'.B14")]
    [InlineData("of:=[2023.A1]", "='2023'.A1")]
    [InlineData("of:=[Interest.B20]", "=Interest.B20")]
    public void I_nomi_di_foglio_che_sembrano_riferimenti_si_citano(string odf, string atteso) =>
        Assert.Equal(atteso, new OdfFormulaSyntax().ToInternal(odf));
}
