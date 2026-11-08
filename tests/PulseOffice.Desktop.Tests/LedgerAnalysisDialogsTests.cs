using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class LedgerAnalysisDialogsTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    private sealed class Cells : PulseOffice.UI.Controls.IReferenceSource
    {
        public bool IsValid(string text) => text.Length > 0;

        public PulseOffice.UI.Controls.References.IReferencePicker? Picker => null;
    }

    private sealed class NoPicker : PulseOffice.UI.Controls.References.IReferencePicker
    {
        public PulseOffice.UI.Icons.IconGeometry? Icon => null;

        public Task<string?> PickAsync(PulseOffice.UI.Controls.References.ReferencePickRequest request) => Task.FromResult<string?>(null);
    }

    private ServiceProvider Provider() =>
        ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);

    private Window Show(Control content)
    {
        var window = new Window { Width = 1000, Height = 760, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private Workbook Book(out Worksheet sheet)
    {
        var workbook = new Workbook();
        sheet = workbook.AddSheet("Dati");
        return workbook;
    }

    private void Put(Worksheet sheet, string address, CellValue value)
    {
        Assert.True(CellAddress.TryParseA1(address, out var cell));
        sheet.SetValue(cell, value);
    }

    [Fact]
    public Task Consolida_mostra_il_risultato_calcolato_e_controlla_le_aree() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var workbook = Book(out var sheet);
        Put(sheet, "A1", CellValue.Text("Voce"));
        Put(sheet, "B1", CellValue.Text("Q"));
        Put(sheet, "A2", CellValue.Text("Mele"));
        Put(sheet, "B2", CellValue.Number(3));
        Put(sheet, "D1", CellValue.Text("Voce"));
        Put(sheet, "E1", CellValue.Text("Q"));
        Put(sheet, "D2", CellValue.Text("Mele"));
        Put(sheet, "E2", CellValue.Number(4));
        var probes = provider.GetRequiredService<ILedgerDialogProbes>();
        var view = new ConsolidateView(provider.GetRequiredService<LedgerDialogKit>(), probes.Consolidation(workbook, 0),
            ["A1:B2"], "H1", new NoPicker(), new Cells());
        var window = Show(view);

        view.AddSource("D1:E2");
        view.AddSource("non è un'area");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, view.Choice.Sources.Count);
        Assert.Equal(11, view.Function.Items.Count);
        Assert.Contains(view.Sketch.Shown.SelectMany(row => row), cell => cell.Text == "7");
        Assert.True(view.CanConfirm);

        view.Function.SelectKey("max");
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(view.Sketch.Shown.SelectMany(row => row), cell => cell.Text == "4");
        object? result = null;
        view.Completed += (_, value) => result = value;
        view.Confirm();
        Assert.Equal(Sheets.Engine.Operations.SubtotalFunction.Max, Assert.IsType<ConsolidateChoice>(result).Function);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Numeri_casuali_mostrano_i_parametri_giusti_e_l_istogramma() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new RandomView(provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<ILedgerDialogProbes>().Random, "A1:A10", new Cells());
        var window = Show(view);

        Assert.True(view.ParameterVisible("minimum"));
        Assert.False(view.ParameterVisible("mean"));
        Assert.Equal(12, view.Heights.Count);
        Assert.True(view.CanConfirm);

        view.Distribution.SelectKey("normal");
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.ParameterVisible("minimum"));
        Assert.True(view.ParameterVisible("deviation"));

        view.Distribution.SelectKey("uniform");
        view.Minimum.Text = "5";
        view.Maximum.Text = "1";
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.CanConfirm);
        Assert.NotEmpty(view.Problem);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Random_numbers_ignore_invalid_values_in_hidden_parameters() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new RandomView(provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<ILedgerDialogProbes>().Random, "A1:A10", new Cells());
        var window = Show(view);

        view.Trials.Text = "abc";
        view.Probability.Text = "7";
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.CanConfirm);
        Assert.Equal(RandomDistribution.Uniform, view.Choice!.Distribution);

        view.Distribution.SelectKey("binomial");
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.CanConfirm);

        view.Probability.Text = "0.5";
        view.Trials.Text = "1e12";
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.CanConfirm);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_modulo_dati_scorre_i_record_e_ne_aggiunge_uno()=> Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var model = new DataFormModel(["Nome", "Età"], [["Anna", "30"], ["Bruno", "41"]]);
        var view = new DataFormView(provider.GetRequiredService<LedgerDialogKit>(), model);
        var window = Show(view);

        Assert.Equal("Anna", view.Fields[0].Text);
        Assert.False(view.CanConfirm);
        view.Next.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("Bruno", view.Fields[0].Text);
        Assert.Contains("2", view.PositionText, StringComparison.Ordinal);
        view.Fields[1].Text = "42";
        Dispatcher.UIThread.RunJobs();
        view.New.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(string.Empty, view.Fields[0].Text);
        view.Fields[0].Text = "Carla";
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.CanConfirm);

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.Confirm();
        var records = Assert.IsType<DataFormChoice>(result).Records;
        Assert.Equal(["Bruno", "42"], records[1]);
        Assert.Equal("Carla", records[2][0]);
        Assert.False(records.ContainsKey(0));
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Ricerca_obiettivo_mostra_la_soluzione_prima_di_applicarla() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var workbook = Book(out var sheet);
        var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(2));
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 1)), "A1*3");
        var view = new GoalSeekView(provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<ILedgerDialogProbes>().GoalSeek(engine, 0), "B1", new Cells(), provider.GetRequiredService<PulseOffice.UI.Controls.Preview.IDebounceTimerFactory>());
        var window = Show(view);

        Assert.False(view.CanConfirm);
        view.VariableCell.Text = "A1";
        view.Target.Text = "12";
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.CanConfirm);
        Assert.Contains("4", view.Solution, StringComparison.Ordinal);
        Assert.Equal(2, sheet[new CellAddress(0, 0)].Value.AsNumber());
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Gli_scenari_si_aggiungono_si_controllano_e_si_mostrano() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var workbook = Book(out var sheet);
        Put(sheet, "A1", CellValue.Number(5));
        var view = new ScenarioView(provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<ILedgerDialogProbes>().Scenarios(workbook, 0),
            provider.GetRequiredService<Sheets.UI.Dialogs.IScenarioPlanner>(), [], CellRange.Single(new CellAddress(0, 0)), new Cells());
        var window = Show(view);

        view.Act("add");
        view.Act("add");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, view.Rows.Count);
        Assert.Equal("5", view.Sketch.Shown[0][0].Text);
        view.NameBox.Text = view.Rows[0].Name;
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.CanConfirm);
        view.NameBox.Text = "Ottimista";
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.CanConfirm);
        Assert.True(view.Actions.Press("up"));
        Assert.Equal("Ottimista", view.Rows[0].Name);

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.SecondaryButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var rows = Assert.IsType<ScenarioChoice>(result).Rows;
        Assert.True(rows[0].Apply);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_protezione_chiede_la_conferma_e_porta_i_permessi() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new SheetProtectionView(provider.GetRequiredService<LedgerDialogKit>(), SheetProtection.None);
        var window = Show(view);

        Assert.Equal(9, view.Permissions.Count);
        view.Password.Text = "abc";
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.CanConfirm);
        view.Confirmation.Text = "abc";
        view.Permissions[7].IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.CanConfirm);
        Assert.True(view.Choice.Permissions.AllowSort);

        var unprotect = new SheetProtectionView(provider.GetRequiredService<LedgerDialogKit>(), SheetProtection.None with { IsEnabled = true });
        Assert.True(unprotect.CanConfirm);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Le_etichette_si_aggiungono_e_si_tolgono_dall_elenco() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var workbook = Book(out var sheet);
        Put(sheet, "A1", CellValue.Text("Mese"));
        var existing = new LabelRange(CellRange.Single(new CellAddress(5, 5)), CellRange.Single(new CellAddress(5, 6)), LabelOrientation.Rows);
        var view = new LabelsView(provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<ILedgerDialogProbes>().Labels(workbook, 0),
            [existing], "A1:C1", new Cells());
        var window = Show(view);

        Assert.True(view.AddButton.IsEnabled);
        Assert.True(view.Sketch.Shown[0][0].Marked);
        view.Orientation.SelectKey("columns");
        view.AddButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, view.Ranges.Count);
        Assert.Equal(LabelOrientation.Columns, view.Ranges[1].Orientation);
        view.Remove(0);
        Assert.Single(view.Ranges);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Statistiche_e_mescola_mostrano_l_anteprima() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var workbook = Book(out var sheet);
        for (var row = 0; row < 6; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));
        var probes = provider.GetRequiredService<ILedgerDialogProbes>();
        var statistics = new StatisticsView(provider.GetRequiredService<LedgerDialogKit>(), probes.Statistics(workbook, 0), "A1:A6", "C1", new Cells(), new Cells());
        var window = Show(statistics);
        Assert.NotEmpty(statistics.Sketch.Shown);
        Assert.True(statistics.CanConfirm);
        statistics.Procedure.SelectKey("anova");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(StatisticsProcedure.Anova, statistics.Choice.Procedure);

        var shuffle = new ShuffleView(provider.GetRequiredService<LedgerDialogKit>(), probes.Shuffle(workbook, 0), "A1:A6", new Cells());
        window.Content = shuffle;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(6, shuffle.Before.Shown.Count);
        Assert.Equal(
            shuffle.Before.Shown.Select(row => row[0].Text).Order(),
            shuffle.After.Shown.Select(row => row[0].Text).Order());
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_filtro_raggruppa_le_date_per_anno_e_mese() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var workbook = Book(out var sheet);
        var dateStyle = workbook.Styles.Derive(0, style => style with { NumberFormatId = Sheets.Formatting.NumberFormatCatalog.SystemShortDateId });
        Put(sheet, "A1", CellValue.Text("Data"));
        var days = new[] { new DateTime(2025, 3, 1), new DateTime(2025, 3, 9), new DateTime(2026, 1, 2) };
        for (var index = 0; index < days.Length; index++)
        {
            var address = new CellAddress(index + 1, 0);
            sheet.SetValue(address, CellValue.Number(days[index].ToOADate()));
            sheet.SetStyle(address, dateStyle);
        }

        var facts = new FilterColumnFactsReader().Read(sheet, new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)), 0, true, workbook.Styles, workbook.NumberFormats);
        var view = new FilterMenuView("Data", facts, null, provider.GetRequiredService<LedgerDialogKit>(), new FilterCriteriaCodec());
        var window = Show(view);

        Assert.True(view.Model.IsDateColumn);
        var years = view.Model.DateGroups();
        Assert.Equal([2025, 2026], years.Select(year => year.Value));
        Assert.Equal(2, view.DateBoxes.Count);
        view.DateExpanders[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(3, view.DateBoxes.Count);
        view.Model.Choose(years[0], false);
        Dispatcher.UIThread.RunJobs();
        Assert.Single(view.Choice.AllowedValues!);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Inserisci_funzione_ha_le_categorie_con_icona_e_conteggio() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var registry = provider.GetRequiredService<Sheets.Engine.Evaluation.Functions.IFunctionRegistry>();
        var view = new FunctionWizardView(registry, provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetRequiredService<Sheets.Engine.Localization.IFormulaLocalization>(),
            provider.GetRequiredService<IFunctionCategoryLooks>(), provider.GetRequiredService<PulseOffice.UI.Icons.IIconCatalog>());
        var window = Show(view);
        var icons = provider.GetRequiredService<PulseOffice.UI.Icons.IIconCatalog>();

        Assert.Null(view.Categories[0].Category);
        Assert.Equal(registry.Count, view.Categories[0].Count);
        Assert.All(view.Categories, entry => Assert.True(icons.Contains(entry.Icon), entry.Icon));
        view.SelectCategory(1);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(view.Categories[1].Count, view.ShownCount);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_linguetta_si_rinomina_in_linea() => Session.Dispatch(async () =>
    {
        var metrics = PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact);
        var strip = new PulseOffice.UI.Controls.Tabs.PageTabStrip(metrics, ["Uno", "Due"]);
        var host = new Panel { Children = { strip } };
        var window = Show(host);
        var overlays = new PulseOffice.UI.Controls.Tabs.TabOverlays(metrics);

        var editing = overlays.EditAsync(strip, strip.BoundsOf(1)!.Value, "Due", text => text == "Uno" ? "già usato" : null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Due", overlays.Editor!.Text);
        overlays.Editor.Text = "Uno";
        Dispatcher.UIThread.RunJobs();
        Assert.True(overlays.EditorProblem!.IsVisible);
        overlays.Editor.Text = "Tre";
        Dispatcher.UIThread.RunJobs();
        Assert.False(overlays.EditorProblem.IsVisible);
        overlays.Editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal("Tre", await editing);
        window.Close();
    }, CancellationToken.None);
}
