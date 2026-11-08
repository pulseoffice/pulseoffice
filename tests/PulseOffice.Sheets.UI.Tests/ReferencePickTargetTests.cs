using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI.Dialogs;
using PulseOffice.UI.Controls.References;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class ReferencePickTargetTests
{
    private sealed class Listener : IReferencePickListener
    {
        public List<string> Seen { get; } = [];

        public int Commits { get; private set; }

        public void SelectionChanged(string reference) => Seen.Add(reference);

        public void Commit() => Commits++;

        public void Cancel()
        {
        }
    }

    private sealed class FakeView
    {
        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public ActiveSheetContext Context { get; } = new();

        public int Sheet { get; set; }

        public CellRange Selection { get; set; } = CellRange.Single(CellAddress.First);

        public ISheetReferencePickSink? Sink { get; private set; }

        public FakeView()
        {
            Workbook.AddSheet("Dati 2024");
            Attach();
        }

        public void Attach() => Context.Attach(new SheetTarget
        {
            Workbook = Workbook,
            SheetIndex = Sheet,
            History = new UndoStack(),
            Engine = null!,
            Selection = () => Selection,
            WriteSelection = range => { Selection = range; Sink?.SelectionChanged(); },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => 1,
            WriteZoom = _ => { },
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = index => { Sheet = index; Attach(); Sink?.SelectionChanged(); },
            WriteSelectedObject = _ => { },
            BeginReferencePick = sink =>
            {
                Sink = sink;
                return new Release(() => Sink = null);
            },
        });

        private sealed class Release(Action end) : IDisposable
        {
            public void Dispose() => end();
        }
    }

    private static CellRange R(string text)
    {
        Assert.True(CellRange.TryParseA1(text, out var range));
        return range;
    }

    [Fact]
    public void SullOrigineIlRiferimentoNonNominaIlFoglio()
    {
        var view = new FakeView();
        var target = new SheetReferencePickTarget(view.Context, new SheetRangeReferences(), ReferenceShape.Range);
        var listener = new Listener();

        using (target.Begin(string.Empty, listener))
        {
            view.Context.SelectRange(R("B2:C5"));
        }

        Assert.Equal("B2:C5", listener.Seen[^1]);
    }

    [Fact]
    public void SuUnAltroFoglioIlRiferimentoLoNominaFraApiciEPoiTornaAllOrigine()
    {
        var view = new FakeView();
        view.Selection = R("D4");
        var target = new SheetReferencePickTarget(view.Context, new SheetRangeReferences(), ReferenceShape.Range);
        var listener = new Listener();

        using (target.Begin(string.Empty, listener))
        {
            view.Context.SelectSheet(1);
            view.Context.SelectRange(R("A1:B3"));
        }

        Assert.Equal("'Dati 2024'.A1:B3", listener.Seen[^1]);
        Assert.Equal(0, view.Sheet);
        Assert.Equal(R("D4"), view.Selection);
        Assert.Null(view.Sink);

        var parsed = new SheetRangeReferences().Parse(view.Workbook, listener.Seen[^1], 0);
        Assert.Equal(new SheetRangeReference(1, R("A1:B3")), parsed);
    }

    [Fact]
    public void LaFormaCellaPrendeSoloLaCellaAttiva()
    {
        var view = new FakeView();
        var target = new SheetReferencePickTarget(view.Context, new SheetRangeReferences(), ReferenceShape.Cell);
        var listener = new Listener();

        using (target.Begin(string.Empty, listener)) view.Context.SelectRange(R("C3:E9"));

        Assert.Equal("C3", listener.Seen[^1]);
    }

    [Fact]
    public void IlTestoCorrenteSiPreselezionaSulFoglio()
    {
        var view = new FakeView();
        var target = new SheetReferencePickTarget(view.Context, new SheetRangeReferences(), ReferenceShape.Range);

        using (target.Begin("'Dati 2024'.B2:B8", new Listener()))
        {
            Assert.Equal(1, view.Sheet);
            Assert.Equal(R("B2:B8"), view.Selection);
        }

        Assert.Equal(0, view.Sheet);
    }

    [Fact]
    public void LEspressioneNominaSempreIlFoglio()
    {
        var view = new FakeView();
        var target = new SheetReferencePickTarget(
            view.Context, new SheetRangeReferences(), ReferenceShape.Range, ReferenceQualification.Always);
        var listener = new Listener();

        using (target.Begin(string.Empty, listener)) view.Context.SelectRange(R("A1:A4"));

        Assert.Equal(view.Workbook.Sheets[0].Name + ".A1:A4", listener.Seen[^1]);
    }

    [Fact]
    public void InvioEDEscPassanoAllaBarra()
    {
        var view = new FakeView();
        var target = new SheetReferencePickTarget(view.Context, new SheetRangeReferences(), ReferenceShape.Range);
        var listener = new Listener();

        using (target.Begin(string.Empty, listener)) view.Sink!.Commit();

        Assert.Equal(1, listener.Commits);
    }

    [Theory]
    [InlineData("Foglio2", "Foglio2.A1")]
    [InlineData("Dati 2024", "'Dati 2024'.A1")]
    [InlineData("L'anno", "'L''anno'.A1")]
    [InlineData("2024", "'2024'.A1")]
    [InlineData("a.b", "'a.b'.A1")]
    public void IlNomeDelFoglioSiQuotaQuandoServeESiRilegge(string name, string expected)
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet(name);
        var references = new SheetRangeReferences();

        var text = references.Qualify(workbook, 1, "A1", 0);

        Assert.Equal(expected, text);
        Assert.Equal(new SheetRangeReference(1, R("A1")), references.Parse(workbook, text, 0));
        Assert.Equal("A1", references.Qualify(workbook, 1, "A1", 1));
    }

    [Fact]
    public void LElencoDiAreeConservaLeAreePrecedentiESostituisceSoloLUltima()
    {
        var view = new FakeView();
        var target = new SheetReferencePickTarget(view.Context, new SheetRangeReferences(), ReferenceShape.RangeList);
        var listener = new Listener();

        using (target.Begin("A1:A3; C1:C3", listener)) view.Context.SelectRange(R("E1:E5"));

        Assert.Equal("A1:A3; E1:E5", listener.Seen[^1]);
    }

    [Fact]
    public void LElencoVuotoDaUnaSolaArea()
    {
        var view = new FakeView();
        var target = new SheetReferencePickTarget(view.Context, new SheetRangeReferences(), ReferenceShape.RangeList);
        var listener = new Listener();

        using (target.Begin(string.Empty, listener)) view.Context.SelectRange(R("B2"));

        Assert.Equal("B2", listener.Seen[^1]);
    }
}
