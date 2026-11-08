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
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class LedgerDataDialogsTests(HeadlessDesktopSession headless)
{
    private UiThreadSession Session => headless.Session;

    [Fact]
    public Task Incolla_speciale_sceglie_a_icone_e_mostra_il_risultato() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new PasteSpecialView(provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<IPasteSketcher>());
        var window = Show(view);
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.Equal(6, view.Parts.Items.Count);
        Assert.Equal(string.Empty, view.Sketch.Shown!.Result[0][1].Text);
        Assert.True(view.Sketch.Shown.Result[0][2].Formula);

        view.Parts.Items[0].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PastePart.Values, view.Choice.Part);
        Assert.False(view.Sketch.Shown.Result[0][2].Formula);

        view.Operations.SelectKey("add");
        view.SkipBlanks.IsChecked = true;
        view.Transpose.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, view.Sketch.Shown.Result.Count);
        Assert.Equal("12", view.Sketch.Shown.Result[0][0].Text);
        Assert.Equal("2", view.Sketch.Shown.Result[1][0].Text);

        view.Parts.SelectKey("formats");
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.Operations.IsEnabled);
        Assert.Equal(PasteOperation.None, view.Choice.Request().Operation);

        view.LinkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var chosen = Assert.IsType<PasteSpecialChoice>(result);
        Assert.True(chosen.Request().Link);
        Assert.True(view.PreferredWidth >= 400);
        window.Close();
    }, CancellationToken.None);

    private Window Show(Control content, double width = 900, double height = 700)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private ServiceProvider Provider() =>
        ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);

    [Fact]
    public Task Convalida_con_tipi_a_icone_elenco_e_anteprime() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var kit = provider.GetRequiredService<LedgerDialogKit>();
        var rules = provider.GetRequiredService<PulseOffice.Sheets.UI.Dialogs.IValidationRules>();
        var editor = new PulseOffice.Sheets.UI.Dialogs.ValidationCriteriaEditor(
            new PulseOffice.Sheets.UI.Dialogs.ValidationCriteriaField("criteria", new PulseOffice.Sheets.UI.Dialogs.ValidationDraft(), rules,
                new FixedReferences(), kit.Icons),
            kit.Texts, kit.Metrics);
        var window = Show(editor);

        Assert.Equal(8, editor.Kind.Items.Count);
        Assert.False(editor.DropdownVisible);
        editor.Kind.SelectKey("list");
        foreach (var value in new[] { "Nord", "Sud", "Est", "Ovest", "Centro", "Isole", "Estero" }) editor.Values.Add(value);
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.DropdownVisible);
        Assert.Equal(["Nord", "Sud", "Est", "Ovest", "Centro", "Isole"], editor.DropdownShown);
        Assert.Equal(PulseOffice.Sheets.ValidationKind.List, editor.Draft.Kind);
        Assert.Equal(7, editor.Draft.ListValues.Count);

        var help = new ValidationHelpPane(kit, string.Empty, string.Empty);
        help.Title.Text = "Zona";
        Dispatcher.UIThread.RunJobs();
        help.Message.Text = "Scegli dall'elenco";
        Dispatcher.UIThread.RunJobs();
        Assert.True(help.BalloonVisible);
        Assert.Equal("Zona|Scegli dall'elenco", help.BalloonText);
        help.ShowOnSelect.IsChecked = false;
        Assert.False(help.BalloonVisible);

        var error = new ValidationErrorPane(kit, PulseOffice.Sheets.ValidationAlert.Stop, string.Empty, string.Empty);
        Assert.Equal(2, error.CardButtons.Count);
        error.Alert.SelectKey("warning");
        Assert.Equal(3, error.CardButtons.Count);
        Assert.Equal(PulseOffice.Sheets.ValidationAlert.Warning, error.Chosen);
        window.Close();
    }, CancellationToken.None);

    private sealed class FixedReferences : PulseOffice.UI.Controls.IReferenceSource
    {
        public bool IsValid(string text) => text.Length > 0;

        public PulseOffice.UI.Controls.References.IReferencePicker? Picker => null;
    }

    [Fact]
    public Task Serie_mostra_i_primi_valori_e_l_unita_solo_per_le_date() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var sheet = new PulseOffice.Sheets.Worksheet("Dati");
        sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), PulseOffice.Sheets.Values.CellValue.Number(2));
        var range = new PulseOffice.Sheets.Addressing.CellRange(new(0, 0), new(19, 0));
        var view = new SeriesView(provider.GetRequiredService<LedgerDialogKit>(),
            provider.GetRequiredService<ISeriesPreviewers>().For(sheet, range));
        var window = Show(view);
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.False(view.UnitVisible);
        Assert.Equal(8, view.PreviewValues.Count);
        Assert.Equal(["2", "3", "4"], view.PreviewValues.Take(3));

        view.Kind.SelectKey("growth");
        view.Step.Text = 3.ToString(System.Globalization.CultureInfo.CurrentCulture);
        Dispatcher.UIThread.RunJobs();
        view.Stop.Text = "100";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["2", "6", "18", "54"], view.PreviewValues);

        view.Step.Text = "x";
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.CanConfirm);
        view.Step.Text = "1";
        Dispatcher.UIThread.RunJobs();
        view.Stop.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();
        view.Kind.SelectKey("date");
        Assert.True(view.UnitVisible);
        Assert.Equal(DateTime.FromOADate(3).ToString("d", System.Globalization.CultureInfo.CurrentCulture), view.PreviewValues[1]);

        view.Kind.SelectKey("linear");
        ((Button)Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view.Footer!).OfType<Button>().Last()).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var choice = Assert.IsType<SeriesChoice>(result);
        Assert.Equal(PulseOffice.Sheets.SeriesKind.Linear, choice.Kind);
        Assert.True(choice.ByColumns);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_menu_del_filtro_cerca_conta_e_compone_la_condizione() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var facts = new FilterColumnFacts(
            [new FilterValueCount("Città", 3), new FilterValueCount("Nord", 2), new FilterValueCount("Sud", 1)], 2, false, []);
        var view = new FilterMenuView("Zona", facts, null, provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<IFilterCriteriaCodec>());
        var window = Show(view);
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.Equal(3, view.ValueBoxes.Count);
        Assert.Contains("8", view.Summary);
        view.Search.Text = "citta";
        Dispatcher.UIThread.RunJobs();
        Assert.Single(view.ValueBoxes);
        view.Search.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();
        view.ValueBoxes[1].IsChecked = false;
        Assert.Null(view.SelectAll.IsChecked);
        Assert.Contains("6", view.Summary);

        view.Condition.SelectedIndex = 3;
        view.ConditionValue.Text = "or";
        var choice = view.Choice;
        Assert.Equal("*or*", choice.Criteria);
        Assert.Equal(2, choice.AllowedValues!.Count);

        view.SortDescendingButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(Assert.IsType<FilterChoice>(result).SortDescending);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_menu_del_filtro_regge_un_colore_malformato_letto_dal_file() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var facts = new FilterColumnFacts(
            [new FilterValueCount("Nord", 1)], 0, false,
            [new ColorFilterCriterion(ColorFilterTarget.Background, "#GGGGGG"), new ColorFilterCriterion(ColorFilterTarget.Text, "rosso")]);
        var current = new ColumnFilter(0) { Color = new ColorFilterCriterion(ColorFilterTarget.Background, "#ZZZZZZ") };
        var view = new FilterMenuView("Zona", facts, current, provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<IFilterCriteriaCodec>());

        Assert.Equal(3, view.ColourButtons.Count);
        Assert.Equal("#ZZZZZZ", view.Choice.Color?.ColorHex);
    }, CancellationToken.None);

    [Fact]
    public Task The_filter_popup_composes_and_reloads_two_joined_conditions() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var facts = new FilterColumnFacts([new FilterValueCount("1", 1), new FilterValueCount("5", 1)], 0, true, []);
        var current = new ColumnFilter(0) { Criteria = ">=2", SecondCriteria = "<=4", Join = CriteriaJoin.Or };
        var view = new FilterMenuView("N", facts, current, provider.GetRequiredService<LedgerDialogKit>(), provider.GetRequiredService<IFilterCriteriaCodec>());

        Assert.Equal(1, view.Join.SelectedIndex);
        Assert.Equal("4", view.SecondConditionValue.Text);
        Assert.Equal(">=2", view.Choice.Criteria);
        Assert.Equal("<=4", view.Choice.SecondCriteria);
        Assert.Equal(CriteriaJoin.Or, view.Choice.Join);

        view.Condition.SelectedIndex = 0;
        Assert.Equal("<=4", view.Choice.Criteria);
        Assert.Null(view.Choice.SecondCriteria);
    }, CancellationToken.None);

    [Fact]
    public void Il_codec_dei_criteri_rilegge_cio_che_scrive()
    {
        var codec = new FilterCriteriaCodec();
        foreach (var condition in Enum.GetValues<FilterOperator>().Skip(1))
        {
            var written = codec.Write(new FilterConditionDraft(condition, "ab"));
            Assert.Equal(new FilterConditionDraft(condition, "ab"), codec.Read(written));
        }

        Assert.Null(codec.Write(new FilterConditionDraft(FilterOperator.Greater, " ")));
    }

    [Fact]
    public Task Il_filtro_avanzato_mostra_i_criteri_e_conta_le_righe() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var workbook = new PulseOffice.Sheets.Workbook();
        workbook.AddSheet("Dati");
        var sheet = workbook.Sheets[0];
        void Put(int row, int column, PulseOffice.Sheets.Values.CellValue value) => sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(row, column), value);
        Put(0, 0, PulseOffice.Sheets.Values.CellValue.Text("Zona"));
        Put(0, 1, PulseOffice.Sheets.Values.CellValue.Text("Totale"));
        string[] zones = ["Nord", "Sud", "Nord", "Est"];
        for (var row = 0; row < zones.Length; row++)
        {
            Put(row + 1, 0, PulseOffice.Sheets.Values.CellValue.Text(zones[row]));
            Put(row + 1, 1, PulseOffice.Sheets.Values.CellValue.Number(row * 10));
        }

        Put(0, 4, PulseOffice.Sheets.Values.CellValue.Text("Zona"));
        Put(1, 4, PulseOffice.Sheets.Values.CellValue.Text("Nord"));
        var view = new AdvancedFilterView(provider.GetRequiredService<LedgerDialogKit>(),
            provider.GetRequiredService<IAdvancedFilterProbes>().For(workbook, 0), "A1:B5", new FixedReferences());
        var window = Show(view);

        Assert.False(view.Confirm.IsEnabled);
        view.Criteria.GetVisualDescendants().OfType<TextBox>().First().Text = "E1:E2";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, view.CriteriaCells);
        Assert.Contains("2", view.CountText);
        Assert.True(view.Confirm.IsEnabled);

        view.Action.SelectKey("copy");
        Assert.False(view.Confirm.IsEnabled);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_filtro_avanzato_segue_il_tema_scuro() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var application = Application.Current!;
        PulseOffice.UI.Theming.Palette.Apply(application, true);
        try
        {
            var workbook = new PulseOffice.Sheets.Workbook();
            var sheet = workbook.AddSheet("Dati");
            sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), PulseOffice.Sheets.Values.CellValue.Text("Zona"));
            sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(1, 0), PulseOffice.Sheets.Values.CellValue.Text("Nord"));
            var view = new AdvancedFilterView(provider.GetRequiredService<LedgerDialogKit>(),
                provider.GetRequiredService<IAdvancedFilterProbes>().For(workbook, 0), new AdvancedFilterChoice("A1:A2", "A1:A2", false, string.Empty), null, new FixedReferences());
            var window = Show(view);

            application.TryGetResource("PulseTextPrimary", Avalonia.Styling.ThemeVariant.Dark, out var expected);
            var ink = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(expected).Color;
            var texts = view.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Text is "Zona" or "Nord").ToList();
            Assert.NotEmpty(texts);
            Assert.All(texts, text => Assert.Equal(ink, Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(text.Foreground).Color));
            window.Close();
        }
        finally
        {
            PulseOffice.UI.Theming.Palette.Apply(application, false);
        }
    }, CancellationToken.None);

    [Fact]
    public Task Sposta_copia_foglio_mostra_l_ordine_risultante_e_controlla_il_nome() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var view = new SheetMoveView(provider.GetRequiredService<LedgerDialogKit>(), new SheetMovePlan(["Uno", "Due", "Tre"], 0, new PulseOffice.Sheets.UI.WorksheetTabPolicy()), "Uno (2)");
        var window = Show(view);

        Assert.Equal(["Due", "Tre", "Uno"], view.ResultTabs);
        view.Positions.SelectedIndex = 2;
        Assert.Equal(["Due", "Uno", "Tre"], view.ResultTabs);

        view.Action.SelectKey("copy");
        view.CopyName.Text = "due";
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.Confirm.IsEnabled);
        view.CopyName.Text = "Quattro";
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.Confirm.IsEnabled);
        Assert.Equal(["Uno", "Due", "Quattro", "Tre"], view.ResultTabs);
        Assert.Equal(new SheetMoveChoice(2, true, "Quattro"), view.Choice);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_gestore_dei_nomi_cerca_modifica_aggiunge_ed_elimina() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var model = new NameManagerModel(
        [
            new NameEntry("Ricavi", "$Dati.$B$2:$B$9", null, "Ricavi"),
            new NameEntry("Aliquota", "$Dati.$E$1", "Dati", "Aliquota", "Dati"),
        ], name => name.Length > 0 && char.IsLetter(name[0]) && !name.Contains(' ', StringComparison.Ordinal));
        var view = new NameManagerView(provider.GetRequiredService<LedgerDialogKit>(), model, new FixedValues(), ["Dati", "Note"], "$Dati.$A$1", new FixedReferences());
        var window = Show(view);

        Assert.Equal(["Aliquota", "Ricavi"], view.ShownNames);
        Assert.Equal("Aliquota", view.NameBox.Text);
        Assert.Equal(1, view.Scope.SelectedIndex);

        view.Search.Text = "ric";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Ricavi"], view.ShownNames);
        view.Search.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();

        view.NameBox.Text = "con spazio";
        Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(view.Problem);
        Assert.False(view.Confirm.IsEnabled);
        view.NameBox.Text = "Iva";
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(view.Problem);

        Assert.True(view.Actions.Press("new"));
        Assert.Equal("Nome1", view.NameBox.Text);
        Assert.True(view.Actions.Press("delete"));
        Assert.Equal(["Iva", "Ricavi"], [.. model.Entries.Select(entry => entry.Name).Order()]);
        Assert.Equal("Dati", model.Entries.Single(entry => entry.Name == "Iva").Scope);
        window.Close();
    }, CancellationToken.None);

    private sealed class FixedValues : INameValuePreview
    {
        public string Value(string expression) => expression.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public Task La_pivot_si_compone_trascinando_nelle_zone_con_anteprima() => Session.Dispatch(async () =>
    {
        await using var provider = Provider();
        var sheet = new PulseOffice.Sheets.Worksheet("Dati");
        void Put(int row, int column, PulseOffice.Sheets.Values.CellValue value) => sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(row, column), value);
        Put(0, 0, PulseOffice.Sheets.Values.CellValue.Text("Zona"));
        Put(0, 1, PulseOffice.Sheets.Values.CellValue.Text("Totale"));
        string[] zones = ["Nord", "Sud", "Nord"];
        for (var row = 0; row < zones.Length; row++)
        {
            Put(row + 1, 0, PulseOffice.Sheets.Values.CellValue.Text(zones[row]));
            Put(row + 1, 1, PulseOffice.Sheets.Values.CellValue.Number(10 * (row + 1)));
        }

        var source = new PulseOffice.Sheets.Pivot.RangePivotSource(sheet, new PulseOffice.Sheets.Addressing.CellRange(new(0, 0), new(3, 1)));
        var view = new PulseOffice.Sheets.UI.PivotPromptView(source.Headers, "Pivot1", provider.GetRequiredService<LedgerDialogKit>(),
            new PulseOffice.Sheets.UI.SourcePivotPreviewer(provider.GetRequiredService<PulseOffice.Sheets.Pivot.IPivotEngine>(), source, new PulseOffice.Sheets.Pivot.PivotLabels("Totale generale", "Totale {0}")));
        var window = Show(view, 1000, 800);

        Assert.False(view.Confirm.IsEnabled);
        view.FieldBoxes[1].IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([1], view.Model.In(PulseOffice.Sheets.Pivot.PivotArea.Value));
        Assert.True(view.Confirm.IsEnabled);

        var rows = view.ZoneFrame(PulseOffice.Sheets.Pivot.PivotArea.Row);
        var target = rows.TranslatePoint(new Point(rows.Bounds.Width / 2, rows.Bounds.Height / 2), view)!.Value;
        view.Drop(0, target);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([0], view.Model.In(PulseOffice.Sheets.Pivot.PivotArea.Row));
        Assert.True(view.PreviewCells >= 6);

        view.Model.Aggregate(1, PulseOffice.Sheets.Pivot.PivotAggregation.Count);
        var fields = view.Model.Fields();
        Assert.Equal(PulseOffice.Sheets.Pivot.PivotAggregation.Count, fields.Single(field => field.Area == PulseOffice.Sheets.Pivot.PivotArea.Value).Aggregation);
        window.Close();
    }, CancellationToken.None);
}
