using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.Localization;
using PulseOffice.Presentations.UI;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Tables;
using PulseOffice.UI.Controls.Tables.Insert;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class TableInsertTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    private readonly TableStylePresets _presets = new();

    private TableInsertDesigner Designer(int rows = 3, int columns = 3) =>
        new(_presets.All, new TableInsertRequest("command.folio.insert.table.title") { Rows = rows, Columns = columns });

    [Fact]
    public void La_griglia_si_allarga_oltre_la_misura_iniziale_ma_resta_nei_limiti()
    {
        var model = new TableSizeModel(new TableInsertLimits(30, 12), TableGridExtent.Dialog, 3, 3);
        Assert.Equal(TableGridExtent.Dialog.InitialRows, model.GridRows);
        Assert.Equal(TableGridExtent.Dialog.InitialColumns, model.GridColumns);

        model.Hover(8, 10);
        Assert.Equal(9, model.GridRows);
        Assert.Equal(11, model.GridColumns);
        Assert.Equal(3, model.Rows);

        model.Hover(9, 11);
        model.Hover(10, 12);
        Assert.Equal(12, model.GridColumns);
        model.Hover(10, 40);
        Assert.Equal(12, model.ShownColumns);

        model.CommitHover();
        Assert.False(model.Hovering);
        Assert.Equal((10, 12), (model.Rows, model.Columns));

        model.Commit(400, 0);
        Assert.Equal((30, 1), (model.Rows, model.Columns));
        Assert.Equal(TableGridExtent.Dialog.MaxRows, model.GridRows);

        model.Move(-100, 2);
        Assert.Equal((1, 3), (model.Rows, model.Columns));
    }

    [Fact]
    public void Uscire_dalla_griglia_ripristina_la_misura_scelta()
    {
        var model = new TableSizeModel(TableInsertLimits.Default, TableGridExtent.Menu, 2, 2);
        var changes = 0;
        model.Changed += (_, _) => changes++;
        model.Hover(5, 6);
        Assert.Equal((5, 6), (model.ShownRows, model.ShownColumns));
        model.EndHover();
        Assert.Equal((2, 2), (model.ShownRows, model.ShownColumns));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Il_progettista_compone_la_scelta_con_stile_intestazione_e_bande()
    {
        var designer = Designer(4, 5);
        var changes = 0;
        designer.Changed += (_, _) => changes++;

        designer.ChooseLook(2);
        designer.HeaderRow = false;
        designer.BandedRows = false;
        designer.BandedRows = false;
        designer.Size.Rows = 6;

        var choice = designer.Choice();
        Assert.Equal(new TableInsertChoice(6, 5, _presets.All[2], false, false), choice);
        Assert.Equal(4, changes);

        designer.MoveLook(-10);
        Assert.Equal(0, designer.LookIndex);
        designer.MoveLook(100);
        Assert.Equal(_presets.All.Count - 1, designer.LookIndex);
    }

    [Fact]
    public void L_anteprima_si_limita_ma_lo_dichiara()
    {
        var designer = Designer(40, 20);
        Assert.Equal(TableInsertDesigner.PreviewRowLimit, designer.PreviewRows);
        Assert.Equal(TableInsertDesigner.PreviewColumnLimit, designer.PreviewColumns);
        Assert.True(designer.PreviewTruncated);
        designer.Size.Commit(4, 3);
        Assert.Equal((4, 3), (designer.PreviewRows, designer.PreviewColumns));
        Assert.False(designer.PreviewTruncated);
    }

    [Fact]
    public Task Il_dialogo_segue_tastiera_e_mouse_e_conferma_con_invio() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = View(provider, Designer());
        var window = new Window { Width = 1000, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        object? result = null;
        view.Completed += (_, value) => result = value;

        view.Grid.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal((4, 4), (view.Designer.Size.Rows, view.Designer.Size.Columns));
        Assert.Equal("4 × 4", view.SizeText);
        Assert.Equal((4, 4), (view.Preview.Rows, view.Preview.Columns));

        var target = view.Grid.TranslatePoint(view.Grid.CenterOf(5, 3), window)!.Value;
        window.MouseMove(target);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("3 × 5", view.SizeText);
        Assert.Equal((5, 3), (view.Preview.Rows, view.Preview.Columns));
        window.MouseDown(target, MouseButton.Left);
        window.MouseUp(target, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal((5, 3), (view.Designer.Size.Rows, view.Designer.Size.Columns));

        var tile = view.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("tile") && Avalonia.Automation.AutomationProperties.GetName(button) == provider.GetRequiredService<ITableStyleGallery>().Name(_presets.All[4]));
        tile.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(_presets.All[5], view.Preview.Look);
        Assert.All(view.Thumbnails, thumbnail => Assert.True(thumbnail.HeaderRow));

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(new TableInsertChoice(5, 3, _presets.All[5], true, true), result);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_scheda_disegnata_cambia_con_frecce_e_clic() => Session.Dispatch(() =>
    {
        var strip = new PageTabStrip(PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact), ["Tabella", "Bordi", "Sfondo"]);
        var chosen = new List<int>();
        strip.SelectionChanged += (_, index) => chosen.Add(index);
        var window = new Window { Width = 500, Height = 120, Content = new StackPanel { Children = { strip } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(strip.Focusable);
        strip.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Assert.Equal([1, 2], chosen);
        Assert.Equal(2, strip.Selected);

        window.MouseDown(strip.TranslatePoint(new Point(8, strip.Bounds.Height / 2), window)!.Value, MouseButton.Left);
        Assert.Equal(0, strip.Selected);
        strip.Select(1);
        Assert.Equal([1, 2, 0, 1], chosen);
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public void In_Deck_la_tabella_porta_lo_stile_cella_per_cella()
    {
        var look = _presets.All[2];
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot
            {
                Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Text)],
                Rows = [["", ""], ["", ""], ["", ""]],
            },
        };

        var styled = new DeckTableLooks().Apply(table, look, true, true);
        Assert.True(styled.Style.OwnLook);
        Assert.True(styled.Style.HeaderRow);
        Assert.Equal(8, styled.CellFormats.Count);
        var header = styled.CellFormats.Single(format => format.Row == TableCellFormat.HeaderRow && format.Column == 0).Look!;
        Assert.Equal("#2563EB", header.FillHex);
        Assert.Equal("#FFFFFF", header.TextHex);
        Assert.True(header.Bold);
        Assert.Null(styled.CellFormats.Single(format => format.Row == 0 && format.Column == 0).Look!.FillHex);
        Assert.Equal("#DBEAFE", styled.CellFormats.Single(format => format.Row == 1 && format.Column == 1).Look!.FillHex);

        var plain = new DeckTableLooks().Apply(table, look, false, false);
        Assert.Equal(6, plain.CellFormats.Count);
        Assert.All(plain.CellFormats, format => Assert.Null(format.Look!.FillHex));
    }

    private ServiceProvider Provider() =>
        ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);

    private TableInsertView View(IServiceProvider provider, TableInsertDesigner designer) => new(
        designer,
        new TableInsertRequest("command.folio.insert.table.title"),
        new TableInsertServices(
            provider.GetRequiredService<ITableStyleGallery>(),
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<ITextCatalog>()));
}
