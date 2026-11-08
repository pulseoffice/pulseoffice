using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SubstitutionContextTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void UnaFormulaDipendenteDiUnAltroFoglioLeggeIlProprioFoglio()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var other = workbook.AddSheet("Altro");
        engine.SetValue(new SheetCell(1, A("A1")), CellValue.Number(100));
        engine.SetValue(new SheetCell(0, A("A1")), CellValue.Number(2));
        engine.SetFormula(new SheetCell(1, A("B1")), $"=A1+{workbook.Sheets[0].Name}.A1");
        engine.SetFormula(new SheetCell(0, A("D1")), "=Altro.B1+0");
        engine.SetValue(new SheetCell(0, A("A3")), CellValue.Number(7));

        engine.SetFormula(new SheetCell(0, A("C3")), "=MULTIPLE.OPERATIONS(D1;A1;A3)");

        Assert.Equal("Altro", other.Name);
        Assert.Equal(107d, engine.GetValue(new SheetCell(0, A("C3"))).AsNumber());
    }

    [Fact]
    public void RigaDellaFormulaDipendenteEQuellaDellaSuaCella()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, A("A1")), CellValue.Number(2));
        engine.SetFormula(new SheetCell(0, A("B1")), "=A1*ROW()");
        engine.SetFormula(new SheetCell(0, A("D3")), "=B1");
        engine.SetValue(new SheetCell(0, A("A5")), CellValue.Number(7));

        engine.SetFormula(new SheetCell(0, A("C5")), "=MULTIPLE.OPERATIONS(D3;A1;A5)");

        Assert.Equal(7d, engine.GetValue(new SheetCell(0, A("C5"))).AsNumber());
    }

    [Fact]
    public void UnaCatenaLungaDiFormuleNonEsaurisceLoStack()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        for (var row = 1; row < 20_000; row++)
            sheet[new CellAddress(row, 0)] = Cell.Blank with { Formula = $"=A{row}+1" };
        var engine = fixture.CreateEngine(workbook);
        engine.RecalculateAll();
        engine.SetValue(new SheetCell(0, A("C1")), CellValue.Number(5));

        engine.SetFormula(new SheetCell(0, A("D1")), "=MULTIPLE.OPERATIONS(A20000;A1;C1)");

        Assert.Equal(20_004d, engine.GetValue(new SheetCell(0, A("D1"))).AsNumber());
    }

    private sealed class ThreadRecordingWeb : IWebResourceAccess
    {
        public List<int> Threads { get; } = [];

        public string? Fetch(string url)
        {
            Threads.Add(Environment.CurrentManagedThreadId);
            return "1";
        }
    }

    [Fact]
    public void SubstitutedEvaluationRunsOnTheCallingThread()
    {
        var (_, engine) = fixture.CreateWorkbook();
        var web = new ThreadRecordingWeb();
        engine.Web = web;
        engine.SetValue(new SheetCell(0, A("A1")), CellValue.Number(2));
        engine.SetFormula(new SheetCell(0, A("B1")), "=A1+VALUE(WEBSERVICE(\"http://example.invalid\"))");
        engine.SetValue(new SheetCell(0, A("C1")), CellValue.Number(10));
        web.Threads.Clear();

        engine.SetFormula(new SheetCell(0, A("D1")), "=MULTIPLE.OPERATIONS(B1;A1;C1)");

        Assert.Equal(11d, engine.GetValue(new SheetCell(0, A("D1"))).AsNumber());
        Assert.NotEmpty(web.Threads);
        Assert.All(web.Threads, thread => Assert.Equal(Environment.CurrentManagedThreadId, thread));
    }

    [Fact]
    public void ALongChainEvaluatesOnASmallStack()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        for (var row = 1; row < 50_000; row++)
            sheet[new CellAddress(row, 0)] = Cell.Blank with { Formula = $"=A{row}+1" };
        var engine = fixture.CreateEngine(workbook);
        engine.RecalculateAll();
        engine.SetValue(new SheetCell(0, A("C1")), CellValue.Number(5));

        var result = CellValue.Empty;
        var worker = new Thread(() =>
        {
            engine.SetFormula(new SheetCell(0, A("D1")), "=MULTIPLE.OPERATIONS(A50000;A1;C1)");
            result = engine.GetValue(new SheetCell(0, A("D1")));
        }, 256 * 1024);
        worker.Start();
        worker.Join();

        Assert.Equal(50_004d, result.AsNumber());
    }
}
