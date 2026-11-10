using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.GridParts;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class GridAndDataAuditTests
{
    private readonly TableEditModel _tables = new();

    [Fact]
    public void AllineareInVerticaleUnaCellaNonNePerdeIlFormato()
    {
        var look = new TableCellLook { FillHex = "#FF0000", Bold = true };
        var formats = new[] { new TableCellFormat(0, 0, CellVerticalAlign.Center) { Look = look } };

        var top = _tables.WithVerticalAlign(formats, 0, 0, 0, 0, CellVerticalAlign.Top);
        var bottom = _tables.WithVerticalAlign(formats, 0, 0, 0, 1, CellVerticalAlign.Bottom);

        Assert.Equal(look, Assert.Single(top).Look);
        Assert.Equal(CellVerticalAlign.Top, top[0].VerticalAlign);
        Assert.Equal(2, bottom.Count);
        Assert.Equal(look, bottom.Single(format => format.Column == 0).Look);
        Assert.All(bottom, format => Assert.Equal(CellVerticalAlign.Bottom, format.VerticalAlign));
        Assert.Empty(_tables.WithVerticalAlign([new TableCellFormat(0, 0, CellVerticalAlign.Center)], 0, 0, 0, 0, CellVerticalAlign.Top));
    }

    [Fact]
    public void LaTabellaModificataTieneNumeriETipiDelleColonne()
    {
        var original = new DataSnapshot
        {
            Columns = [new DataColumn("Name", DataValueKind.Text), new DataColumn("Amount", DataValueKind.Number)],
            Rows = [["Pens", 10.5], ["Ink", 3.0]],
        };
        var edited = 12.25.ToString(CultureInfo.CurrentCulture);

        var composed = _tables.Compose(
            original,
            ["Name", "Amount", "Note"],
            [["Pens", _tables.Display(10.5), "x"], ["Ink", edited, string.Empty], ["Paper", string.Empty, string.Empty]]);

        Assert.Equal(DataValueKind.Number, composed.Columns[1].Kind);
        Assert.Equal(DataValueKind.Text, composed.Columns[2].Kind);
        Assert.Equal(10.5, composed.Rows[0][1]);
        Assert.Equal(12.25, composed.Rows[1][1]);
        Assert.Null(composed.Rows[2][1]);
        Assert.Equal("x", composed.Rows[0][2]);
    }

    [Fact]
    public void UnTestoInUnaColonnaNumericaLaRendeDiTesto()
    {
        var original = new DataSnapshot
        {
            Columns = [new DataColumn("Amount", DataValueKind.Number)],
            Rows = [[10.0], [20.0]],
        };

        var composed = _tables.Compose(original, ["Amount"], [[_tables.Display(10.0)], ["n/a"]]);

        Assert.Equal(DataValueKind.Text, composed.Columns[0].Kind);
        Assert.Equal(10.0, composed.Rows[0][0]);
        Assert.Equal("n/a", composed.Rows[1][0]);
    }

    [Fact]
    public void UnaConnessioneNuovaConLoStessoNomeNonSovrascriveQuellaEsistente()
    {
        var identifiers = new PulseOffice.Data.ConnectionIds();

        Assert.Equal("sales-db", identifiers.NewId("Sales DB", []));
        Assert.Equal("sales-db-2", identifiers.NewId("sales db", ["sales-db"]));
        Assert.Equal("sales-db-3", identifiers.NewId("Sales DB!", ["sales-db", "SALES-DB-2"]));
        Assert.Equal("connection", identifiers.NewId("!!!", []));
    }

    [Fact]
    public void IlRiepilogoDiUnaColonnaInteraNonScorreUnMilioneDiCelle()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(2));
        sheet.SetValue(new CellAddress(5, 0), CellValue.Number(4));
        sheet.SetValue(new CellAddress(9, 0), CellValue.Text("x"));
        sheet.SetValue(new CellAddress(9, 1), CellValue.Number(100));

        var watch = Stopwatch.StartNew();
        var totals = new SelectionSummary().Summarize(
            sheet, new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, 0)));
        watch.Stop();

        Assert.Equal(3, totals.Filled);
        Assert.Equal(2, totals.Numbers);
        Assert.Equal(6, totals.Sum);
        Assert.Equal(3, totals.Average);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UnaPercentualeScrittaInUnaColonnaFormattataNeTieneLoSfondo()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var columnStyle = workbook.Styles.Derive(0, style => style with { BackgroundHex = "#FFFF00" });
        sheet.Layout.SetColumnStyle(1, columnStyle);

        var styleId = new CellEntryStyle().StyleFor(workbook, sheet, new CellAddress(3, 1), "0%");

        Assert.NotNull(styleId);
        var style = workbook.Styles.Get(styleId.Value);
        Assert.Equal("#FFFF00", style.BackgroundHex);
        Assert.NotEqual(0, style.NumberFormatId);
        Assert.Null(new CellEntryStyle().StyleFor(workbook, sheet, new CellAddress(3, 1), null));
    }

    [Fact]
    public void Cell_snap_edges_stop_at_the_last_visible_row_and_column_when_the_rest_is_hidden()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Layout.HideRows(20, CellAddress.MaxRows - 1);
        workbook.Sheets[0].Layout.HideColumns(10, CellAddress.MaxColumns - 1);
        var engine = new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        var viewport = new GridViewport(engine, DesignTokens.For(InterfaceDensity.Compact), 0, () => new Size(1200, 800), new PulseOffice.Sheets.UI.Outline.OutlineGutter());

        var edges = viewport.CellEdges();

        Assert.InRange(edges.Horizontals.Count, 20, 22);
        Assert.InRange(edges.Verticals.Count, 10, 12);
        Assert.Equal(edges.Horizontals.Count, edges.Horizontals.Distinct().Count());
    }

    [Fact]
    public void IlBordoDiUnaColonnaBloccataSiTrovaAncheConIlFoglioScorso()
    {
        var workbook = Workbook.CreateDefault();
        var engine = new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        var viewport = new GridViewport(engine, DesignTokens.For(InterfaceDensity.Compact), 0, () => new Size(1200, 800), new PulseOffice.Sheets.UI.Outline.OutlineGutter());
        viewport.FreezePanes(0, 2);
        viewport.Scroll = new Vector(3000, 0);

        var geometry = viewport.Geometry;
        var border = new Point(geometry.RowHeaderWidth + geometry.ColumnWidth(0), geometry.ColumnHeaderHeight / 2);

        Assert.Equal(0, viewport.ColumnBorderAt(border));
        Assert.Equal(-1, viewport.ColumnBorderAt(new Point(border.X, geometry.ColumnHeaderHeight + 40)));
    }
}
