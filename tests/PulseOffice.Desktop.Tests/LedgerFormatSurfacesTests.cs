using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using PulseOffice.Document.Pages;
using PulseOffice.Foundation.Pages;
using PulseOffice.Localization;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.UI.CellFormat;
using PulseOffice.Sheets.UI.Conditional;
using PulseOffice.Sheets.UI.PageLayout;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class LedgerFormatSurfacesTests(HeadlessDesktopSession headless)
{
    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.Italian);

    private static FormatCellsServices Services() => new(
        new NumberFormatDesigner(), new NumberFormatParser(),
        new NumberFormatter(CultureInfo.GetCultureInfo("it-IT"), serial => new DateTime(1899, 12, 30).AddDays(serial)),
        new MeasureUnits(), new IconCatalog(), new AngleDialGeometry(),
        new PulseOffice.UI.Controls.TextFormat.CharacterFormatTabs(null!, new PulseOffice.UI.Controls.Text.LanguageChoices(new TextCatalog(UiLanguage.Italian)), new PulseOffice.UI.Icons.TextFormatIcons(), null!, new PulseOffice.UI.Controls.TextFormat.CharacterSampleLayout(), new TextCatalog(UiLanguage.Italian)),
        new CellCharacterFormat());

    [Fact]
    public void Il_carattere_condiviso_rilegge_il_carattere_della_cella_senza_perdite()
    {
        var mapper = new CellCharacterFormat();
        var font = new CellFont { Family = "Georgia", Size = 13, Bold = true, DoubleUnderline = true, Underline = true, Overline = true, Shadow = true, Script = CellScript.Superscript, ColorHex = "#2563EB" };
        var request = mapper.Request(font, "Prova");

        Assert.Equal(font, mapper.Apply(CellFont.Default, request.Current));
        Assert.False(request.Offers(PulseOffice.UI.Controls.TextFormat.CharacterFeatures.Highlight));
    }

    private sealed class NoReferences : IReferenceSource
    {
        public PulseOffice.UI.Controls.References.IReferencePicker? Picker => null;

        public bool IsValid(string text) => true;
    }

    [Fact]
    public Task I_numeri_mostrano_il_valore_della_cella_nel_formato_scelto() => headless.Session.Dispatch(() =>
    {
        var page = new NumberFormatPage("General", new NumberFormatCatalog(), CellValue.Number(-1234.5), "B3", Services(), _texts,
            DesignTokens.For(InterfaceDensity.Compact));
        page.Choose(new NumberFormatChoice { Category = NumberCategory.Number, Decimals = 1, Thousands = true, Negative = NegativeNumberStyle.Parentheses });
        Assert.Equal("#,##0.0;(#,##0.0)", page.Code);
        Assert.Equal("(1.234,5)", page.SampleText);
        page.Choose(new NumberFormatChoice { Category = NumberCategory.Percent, Decimals = 0 });
        Assert.Equal("0%", page.Code);
        Assert.Equal((int)NumberCategory.Percent, page.Categories.SelectedIndex);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task L_allineamento_aggiorna_stile_e_anteprima() => headless.Session.Dispatch(() =>
    {
        var page = new CellAlignmentPage(CellStyle.Default, "Testo", Services(), _texts, DesignTokens.For(InterfaceDensity.Compact));
        page.Horizontal.SelectedIndex = (int)CellHorizontalAlignment.Right;
        page.Rotation.Dial.Step(45);
        Assert.Equal(45, page.Style.RotationDegrees);
        Assert.Equal(45, page.Preview.Shown.RotationDegrees);
        Assert.Equal(45, page.Rotation.Field.Value);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task L_intestazione_si_scrive_con_i_pulsanti_e_si_vede_in_anteprima() => headless.Session.Dispatch(() =>
    {
        var paper = new PaperCatalog().Default;
        var input = new SheetPageSetupInput(new PageSetup { Paper = paper }, [paper], string.Empty, 0, 0, "Vendite", "conti.ods", new NoReferences());
        var view = new SheetPageSetupView(input, new SheetPageSetupServices(new PageSketchLayout(), new HeaderFooterTokens(), new MeasureUnits(), new IconCatalog()), _texts);
        view.Build();
        var header = view.HeaderEditor!;
        header.Target(2);
        header.Insert(view.HeaderFields.First(field => field.Code == "&A"));
        header.Zones[1].Text = "Pagina [Pagina] di [Pagine]";
        var result = view.Collect();
        Assert.Equal("&A", result.Setup.Header.Right);
        Assert.Equal("Pagina &P di &N", result.Setup.Header.Center);
        Assert.Equal("Vendite", view.Sketch().Header!.Right);
        Assert.Equal("Pagina 1 di 3", view.Sketch().Header!.Center);
        Assert.Equal(4, view.Previews.Count);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Le_varianti_di_prima_pagina_e_pari_e_il_primo_numero_restano_nel_risultato() => headless.Session.Dispatch(() =>
    {
        var paper = new PaperCatalog().Default;
        var setup = new PageSetup
        {
            Paper = paper,
            DifferentFirstPage = true,
            DifferentOddEven = true,
            FirstHeader = new HeaderFooter(Center: "Copertina"),
            EvenFooter = new HeaderFooter(Left: "&P"),
            FirstPageNumber = 3,
        };
        var input = new SheetPageSetupInput(setup, [paper], string.Empty, 0, 0, "Vendite", "conti.ods", new NoReferences());
        var view = new SheetPageSetupView(input, new SheetPageSetupServices(new PageSketchLayout(), new HeaderFooterTokens(), new MeasureUnits(), new IconCatalog()), _texts);
        view.Build();
        view.FirstHeaderEditor!.Zones[0].Text = "Indice";
        var result = view.Collect().Setup;
        Assert.Equal("Indice", result.FirstHeader.Left);
        Assert.Equal("Copertina", result.FirstHeader.Center);
        Assert.Equal("&P", result.EvenFooter.Left);
        Assert.True(result.DifferentFirstPage);
        Assert.True(result.DifferentOddEven);
        Assert.Equal(3, result.FirstPageNumber);
        return Task.CompletedTask;
    }, CancellationToken.None);

    private sealed class FixedEditing(ConditionalRule? answer) : IConditionalRuleEditing
    {
        public Task<ConditionalRule?> EditAsync(ConditionalRule rule, CancellationToken ct) => Task.FromResult(answer);
    }

    private sealed class PendingEditing : IConditionalRuleEditing
    {
        public int Calls { get; private set; }

        public TaskCompletionSource<ConditionalRule?> Answer { get; } = new();

        public Task<ConditionalRule?> EditAsync(ConditionalRule rule, CancellationToken ct)
        {
            Calls++;
            return Answer.Task;
        }
    }

    [Fact]
    public Task Invio_sul_gestore_delle_regole_e_gestito_subito_e_non_apre_due_editor() => headless.Session.Dispatch(() =>
    {
        var range = CellRange.Single(CellAddress.First);
        var rule = new ConditionalRuleCatalog().Default(ConditionalRuleKind.DataBar, range);
        var editing = new PendingEditing();
        var view = new ConditionalRulesManagerView([rule], range, editing, EditorServices(), _texts);
        view.List.SelectedIndex = 0;

        var first = new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter };
        view.List.RaiseEvent(first);
        var second = new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter };
        view.List.RaiseEvent(second);

        Assert.True(first.Handled);
        Assert.True(second.Handled);
        Assert.Equal(1, editing.Calls);
        editing.Answer.SetResult(null);
        return Task.CompletedTask;
    }, CancellationToken.None);

    private ConditionalEditorServices EditorServices() => new(
        new ConditionalRuleCatalog(), new ConditionalPainting(new PulseOffice.Drawing.ConditionalIcons.ConditionalIconCatalog(), new IconPainter()),
        new GalleryNavigator(), new MeasureUnits(), new IconCatalog());

    [Fact]
    public Task Il_gestore_ordina_duplica_ed_elimina_le_regole() => headless.Session.Dispatch(async () =>
    {
        var range = CellRange.Single(CellAddress.First);
        var catalog = new ConditionalRuleCatalog();
        var first = catalog.Default(ConditionalRuleKind.DataBar, range) with { Priority = 0 };
        var second = catalog.Default(ConditionalRuleKind.ColorScale, range) with { Priority = 1 };
        var created = catalog.Default(ConditionalRuleKind.IconSet, range);
        var view = new ConditionalRulesManagerView([first, second], range, new FixedEditing(created), EditorServices(), _texts);
        Assert.Equal(2, view.List.ItemCount);

        view.MoveVisible(1, 0);
        Assert.Same(second, view.Rules[0]);
        await view.Act(ConditionalRulesManagerView.ActionNew);
        Assert.Same(created, view.Rules[0]);
        view.List.SelectedIndex = 0;
        await view.Act(ConditionalRulesManagerView.ActionDelete);
        view.List.SelectedIndex = 0;
        await view.Act(ConditionalRulesManagerView.ActionDown);
        var result = view.Result();
        Assert.Equal([first.Visual, second.Visual], result.Select(rule => rule.Visual));
        Assert.Equal([0, 1], result.Select(rule => rule.Priority));
    }, CancellationToken.None);

    [Fact]
    public Task Il_gestore_sposta_nella_selezione_senza_scavalcare_le_regole_nascoste_e_duplica_sotto() => headless.Session.Dispatch(async () =>
    {
        var range = CellRange.Single(CellAddress.First);
        var elsewhere = CellRange.Single(new CellAddress(9, 9));
        var catalog = new ConditionalRuleCatalog();
        var shown = catalog.Default(ConditionalRuleKind.DataBar, range);
        var hidden = catalog.Default(ConditionalRuleKind.ColorScale, elsewhere);
        var moved = catalog.Default(ConditionalRuleKind.IconSet, range);
        var view = new ConditionalRulesManagerView([shown, hidden, moved], range, new FixedEditing(null), EditorServices(), _texts);
        view.Scope.SelectedIndex = 0;
        Assert.Equal(2, view.List.ItemCount);

        view.List.SelectedIndex = 1;
        await view.Act(ConditionalRulesManagerView.ActionUp);
        Assert.Equal([moved, shown, hidden], view.Rules);

        view.List.SelectedIndex = 0;
        await view.Act(ConditionalRulesManagerView.ActionDuplicate);
        Assert.Equal([moved, moved, shown, hidden], view.Rules);
        Assert.Equal(1, view.List.SelectedIndex);
    }, CancellationToken.None);

    [Fact]
    public Task L_editor_delle_barre_imposta_asse_e_direzione() => headless.Session.Dispatch(() =>
    {
        var catalog = new ConditionalRuleCatalog();
        var view = new ConditionalRuleEditorView(catalog.Default(ConditionalRuleKind.DataBar, CellRange.Single(CellAddress.First)), string.Empty,
            new NoReferences(), EditorServices(), _texts);
        var boxes = view.GetLogicalDescendants().OfType<ComboBox>().ToList();
        boxes.Single(box => Avalonia.Automation.AutomationProperties.GetName(box) == _texts["conditional.bar.axis"]).SelectedIndex = 1;
        boxes.Single(box => Avalonia.Automation.AutomationProperties.GetName(box) == _texts["conditional.bar.direction"]).SelectedIndex = 2;

        Assert.Equal(DataBarAxis.Middle, view.Rule.Bar.Axis);
        Assert.Equal(DataBarDirection.RightToLeft, view.Rule.Bar.Direction);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task L_editor_delle_icone_imposta_soglie_e_icone_per_posizione() => headless.Session.Dispatch(() =>
    {
        var catalog = new ConditionalRuleCatalog();
        var view = new ConditionalRuleEditorView(catalog.Default(ConditionalRuleKind.IconSet, CellRange.Single(CellAddress.First)), string.Empty,
            new NoReferences(), EditorServices(), _texts);
        var count = view.Rule.IconCount;
        var values = view.GetLogicalDescendants().OfType<TextBox>().Where(box => Avalonia.Automation.AutomationProperties.GetName(box) == _texts["conditional.icons.value"]).ToList();
        Assert.Equal(count - 1, values.Count);
        values[0].Text = "80";
        Assert.Equal(count, view.Rule.Thresholds.Count);
        Assert.Equal(80, view.Rule.Thresholds[^1].Value);

        var icon = view.GetLogicalDescendants().OfType<ComboBox>().First(box => Avalonia.Automation.AutomationProperties.GetName(box) == _texts.Format("conditional.icons.icon", 1));
        icon.SelectedIndex = 0;
        Assert.Equal(count, view.Rule.CustomIcons.Count);
        Assert.True(view.Rule.CustomIcons[^1].IsNone);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Primi_e_ultimi_in_percentuale_si_fermano_a_cento() => headless.Session.Dispatch(() =>
    {
        var rule = new ConditionalRuleCatalog().Default(ConditionalRuleKind.TopBottom, CellRange.Single(CellAddress.First)) with { Operands = ["500"] };
        var view = new ConditionalRuleEditorView(rule, string.Empty, new NoReferences(), EditorServices(), _texts);
        Assert.Equal("500", view.Draft.TypedOperands[0]);
        var op = view.GetLogicalDescendants().OfType<ComboBox>().Single(box => Avalonia.Automation.AutomationProperties.GetName(box) == _texts["condition.operator"]);
        op.SelectedIndex = 2;

        Assert.Equal(ConditionOperator.TopPercent, view.Rule.Operator);
        Assert.Equal("100", view.Draft.TypedOperands[0]);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task L_editor_cambia_tipo_e_l_anteprima_lo_segue() => headless.Session.Dispatch(() =>
    {
        var catalog = new ConditionalRuleCatalog();
        var view = new ConditionalRuleEditorView(catalog.Default(ConditionalRuleKind.CellValue, CellRange.Single(CellAddress.First)), string.Empty,
            new NoReferences(), EditorServices(), _texts);
        view.SwitchKind(ConditionalRuleKind.ColorScale);
        Assert.Equal(PulseOffice.Sheets.Formatting.ConditionalVisual.ColorScale, view.Preview.Rule!.Visual);
        view.SwitchKind(ConditionalRuleKind.CellValue);
        Assert.Equal(ConditionalRuleKind.CellValue, catalog.KindOf(view.Rule));
        Assert.Equal(catalog.Kinds.Count, view.Kinds.Items.Count);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task L_editor_imposta_deviazioni_standard_e_interruzione() => headless.Session.Dispatch(() =>
    {
        var catalog = new ConditionalRuleCatalog();
        var view = new ConditionalRuleEditorView(catalog.Default(ConditionalRuleKind.Average, CellRange.Single(CellAddress.First)), string.Empty,
            new NoReferences(), EditorServices(), _texts);
        var controls = view.GetLogicalDescendants().ToList();
        var deviations = controls.OfType<ComboBox>().Single(box => Avalonia.Automation.AutomationProperties.GetName(box) == _texts["condition.deviations"]);
        var stop = controls.OfType<CheckBox>().Single(box => Equals(box.Content, _texts["condition.stopIfTrue"]));

        deviations.SelectedIndex = 2;
        stop.IsChecked = true;

        Assert.Equal(2, view.Rule.StandardDeviations);
        Assert.True(view.Rule.StopIfTrue);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task La_sparkline_legge_i_dati_veri_per_l_anteprima() => headless.Session.Dispatch(() =>
    {
        var sheet = new Worksheet("Dati");
        for (var column = 0; column < 4; column++) sheet.SetValue(new CellAddress(0, column), CellValue.Number(column * 2 + 1));
        var view = new SparklineDialogView(sheet, "A1:D1", new NoReferences(),
            text => CellRange.TryParseA1(text, out var range) ? range : null,
            new SparklineDialogServices(new PulseOffice.Sheets.UI.SparklinePainter(), new SparklineData(), new GalleryNavigator()), _texts);
        var points = view.Points();
        Assert.Equal([1d, 3d, 5d, 7d], points.Values.Select(value => value ?? 0));
        Assert.Equal(3, view.Kinds.Items.Count);
        return Task.CompletedTask;
    }, CancellationToken.None);
}
