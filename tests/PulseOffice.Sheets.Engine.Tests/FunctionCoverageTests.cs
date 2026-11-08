using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class FunctionCoverageTests : IClassFixture<EngineFixture>
{
    private readonly IFunctionRegistry Library;
    private readonly ISpecialFormRegistry Forms;

    public FunctionCoverageTests(EngineFixture fixture)
    {
        Library = fixture.Functions;
        Forms = fixture.SpecialForms;
    }

    [Fact]
    public void LInventarioRaccoglieMotoreEAddIn()
    {
        Assert.Equal(432, FunctionInventory.Core.Count);
        Assert.Equal(101, FunctionInventory.Analysis.Count);
        Assert.Equal(8, FunctionInventory.DateAddIn.Count);
        Assert.Equal(4, FunctionInventory.Pricing.Count);

        Assert.True(FunctionInventory.Required.Count < 432 + 101 + 8 + 4);
        Assert.True(FunctionInventory.Required.Count > 432);
    }

    [Fact]
    public void NessunaFunzioneRegistrataFuoriDallElencoUfficiale()
    {
        var unexpected = FunctionInventory.Unexpected(Library, Forms);
        Assert.True(unexpected.Count == 0,
            $"Funzioni registrate ma non previste dall'elenco ufficiale: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void LeFunzioniRegistrateSonoRaggiungibili()
    {
        foreach (var function in Library.All)
        {
            Assert.NotNull(Library.Find(function.Name));
            Assert.True(function.MinArguments >= 0);
            Assert.True(function.MaxArguments >= function.MinArguments);
        }
    }

    [Fact]
    public void ScriveIlRapportoDiCopertura()
    {
        var missing = FunctionInventory.Missing(Library, Forms);
        var coverage = FunctionInventory.CoverageFraction(Library, Forms);

        var report = Path.Combine(AppContext.BaseDirectory, "copertura-funzioni.txt");
        File.WriteAllText(report, $"""
            Copertura delle funzioni di Ledger
            Implementate: {FunctionInventory.Required.Count - missing.Count} su {FunctionInventory.Required.Count} ({coverage:P1})

            Mancanti ({missing.Count}):
            {string.Join(Environment.NewLine, missing)}
            """);

        Assert.True(coverage >= 0);
    }

    [Theory]

    [InlineData("SUM")]
    [InlineData("AVERAGE")]
    [InlineData("IF")]
    [InlineData("COUNT")]
    [InlineData("MIN")]
    [InlineData("MAX")]
    [InlineData("ROUND")]
    [InlineData("IFERROR")]
    public void LeFunzioniPiuUsateSonoGiaPresenti(string name) =>
        Assert.True(Library.Contains(name), $"Funzione essenziale mancante: {name}");
}
