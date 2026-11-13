using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Settings;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI;
using PulseOffice.Sheets.Values;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class LedgerEditorTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-ledger-" + Guid.NewGuid().ToString("N"));

    public LedgerEditorTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class Ledger(ServiceProvider provider, CommandRegistry registry, ShellWindow window) : IAsyncDisposable
    {
        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public IActiveSheetContext Sheet => provider.GetRequiredService<IActiveSheetContext>();

        public Worksheet Active => Sheet.Workbook!.Sheets[Sheet.SheetIndex];

        public CellValue Value(int row, int column) => Active.GetValue(new CellAddress(row, column));

        public void Type(string text)
        {
            window.KeyTextInput(text);
            Press(Key.Enter, PhysicalKey.Enter);
        }

        public void Write(string text) => window.KeyTextInput(text);

        public Avalonia.Controls.TextBox FontSizeBox() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
            .OfType<Avalonia.Controls.TextBox>().First(box => box.Classes.Contains("ribbonNumber"));

        public PulseOffice.UI.Controls.Ribbon.RibbonSlot Slot(string command) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
            .OfType<PulseOffice.UI.Controls.Ribbon.RibbonSlot>().First(slot => slot.CommandId == command);

        public string? FocusedName() => window.FocusManager?.GetFocusedElement() is Avalonia.Controls.Control focused ? Avalonia.Automation.AutomationProperties.GetName(focused) : null;

        public string? FocusedControlName() => (window.FocusManager?.GetFocusedElement() as Avalonia.Controls.Control)?.Name;

        public SheetGridView Grid() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
            .OfType<SheetGridView>().First();

        public SkiaSharp.SKBitmap Frame()
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            using var frame = window.CaptureRenderedFrame()!;
            using var stream = new MemoryStream();
            frame.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            return SkiaSharp.SKBitmap.Decode(stream.ToArray());
        }

        public Avalonia.Point GridOrigin() => Grid().TranslatePoint(default, window)!.Value;

        public void Drag(Avalonia.Point from, Avalonia.Point to)
        {
            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(new Avalonia.Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), RawInputModifiers.LeftMouseButton);
            window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            window.MouseUp(to, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        public string? FormulaBar() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window)
            .OfType<Avalonia.Controls.TextBox>().Single(box => box.Name == "FormulaBar").Text;

        public void Press(Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            window.KeyPress(key, modifiers, physical, null);
            window.KeyRelease(key, modifiers, physical, null);
            Dispatcher.UIThread.RunJobs();
        }

        public void Select(int row, int column, int toRow, int toColumn)
        {
            Sheet.SelectRange(new CellRange(new CellAddress(row, column), new CellAddress(toRow, toColumn)));
            Dispatcher.UIThread.RunJobs();
        }

        public bool IsChecked(string id) =>
            registry.Find(new CommandId(id)) is { CheckedWhen: { } isChecked }
            && isChecked(provider.GetRequiredService<IDocumentWorkspace>().Context);

        public bool CanRun(string id) =>
            registry.Find(new CommandId(id)) is { } command
            && command.IsEnabled(provider.GetRequiredService<IDocumentWorkspace>().Context);

        public async Task Run(string id)
        {
            var command = registry.Find(new CommandId(id));
            Assert.NotNull(command);
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(command, workspace.Context);
            Dispatcher.UIThread.RunJobs();
        }

        public void Start(string id)
        {
            var command = registry.Find(new CommandId(id));
            Assert.NotNull(command);
            provider.GetRequiredService<CommandExecutor>().Execute(command, provider.GetRequiredService<IDocumentWorkspace>().Context);
            Dispatcher.UIThread.RunJobs();
        }

        public IEnumerable<T> Find<T>() => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<T>();

        public double Brightness()
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            using var frame = window.CaptureRenderedFrame()!;
            using var stream = new MemoryStream();
            frame.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            using var bitmap = SkiaSharp.SKBitmap.Decode(stream.ToArray());
            var samples = Enumerable.Range(0, 400).Select(index => bitmap.GetPixel(index * 37 % bitmap.Width, index * 53 % bitmap.Height));
            return samples.Average(pixel => (pixel.Red + pixel.Green + pixel.Blue) / 3.0);
        }

        public async ValueTask DisposeAsync()
        {
            window.Close();
            await provider.DisposeAsync();
        }
    }

    private async Task<Ledger> Open()
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        ((PulseOffice.UI.Controls.Services.IDocumentFocusTarget)workspace.Active!.View).FocusDocument();
        Dispatcher.UIThread.RunJobs();
        return new Ledger(provider, registry, window);
    }

    [Fact]
    public Task Trascinare_un_file_sulla_finestra_lo_apre() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        var window = ledger.Service<ShellWindow>();
        ledger.Service<IDocumentArrivals>().AcceptDrops(window);
        var path = Path.Combine(Path.GetTempPath(), "pulseoffice-drop-" + Guid.NewGuid().ToString("N") + ".ods");
        using (var output = File.Create(path))
            new PulseOffice.Formats.Odf.OdsWriter().Write(ledger.Sheet.Workbook!, output);
        try
        {
            var workspace = ledger.Service<IDocumentWorkspace>();
            var before = workspace.Documents.Count;
            var lookup = window.StorageProvider.TryGetFileFromPathAsync(new Uri(path));
            for (var spin = 0; spin < 1000 && !lookup.IsCompleted; spin++)
            {
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
            Assert.True(lookup.IsCompleted, "storage lookup");
            var file = await lookup;
            Assert.Single(ledger.Service<IDocumentArrivals>().Openable([Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(file!)]));
            var data = new Avalonia.Input.DataTransfer();
            data.Add(Avalonia.Input.DataTransferItem.CreateFile(file!));

            window.DragDrop(new Point(400, 400), Avalonia.Input.Raw.RawDragEventType.DragEnter, data, Avalonia.Input.DragDropEffects.Copy, RawInputModifiers.None);
            window.DragDrop(new Point(400, 400), Avalonia.Input.Raw.RawDragEventType.DragOver, data, Avalonia.Input.DragDropEffects.Copy, RawInputModifiers.None);
            window.DragDrop(new Point(400, 400), Avalonia.Input.Raw.RawDragEventType.Drop, data, Avalonia.Input.DragDropEffects.Copy, RawInputModifiers.None);
            for (var spin = 0; spin < 1000 && workspace.Documents.Count == before; spin++)
            {
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal(before + 1, workspace.Documents.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }, CancellationToken.None);

    [Fact]
    public Task Incollare_una_tabella_html_di_un_altra_app_porta_valori_e_formattazione() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        var rich = new ClipboardProbe(ledger.Service<PulseOffice.Exchange.IExchangeClipboard>());
        await rich.SetAsync(new ClipboardContent
        {
            Text = "Nome\tValore\nRossi\t12",
            Html = "<table><tr><td><b>Nome</b></td><td style=\"background:#FFFF00\">Valore</td></tr><tr><td>Rossi</td><td>12</td></tr></table>",
        });
        ledger.Select(0, 0, 0, 0);

        await ledger.Service<ISheetClipboardService>().PasteAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Nome", ledger.Value(0, 0).ToString());
        Assert.Equal(12, ledger.Value(1, 1).AsNumber());
        var styles = ledger.Sheet.Workbook!.Styles;
        Assert.True(styles.Get(ledger.Active.StyleIdAt(new CellAddress(0, 0))).Font.Bold);
        Assert.Equal("#FFFF00", styles.Get(ledger.Active.StyleIdAt(new CellAddress(0, 1))).BackgroundHex);

        ledger.Select(0, 0, 1, 1);
        await ledger.Service<ISheetClipboardService>().CopyAsync();
        var copied = await rich.GetAsync();
        Assert.Contains("<table>", copied.Html, StringComparison.Ordinal);
        Assert.Contains("font-weight:600", copied.Html, StringComparison.Ordinal);
        Assert.Contains("text-align:right", copied.Html, StringComparison.Ordinal);
        Assert.Contains("font-family:'", copied.Html, StringComparison.Ordinal);
    }, CancellationToken.None);

    [Fact]
    public Task Una_tabella_html_con_bordo_incollata_porta_i_bordi_nelle_celle() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        var rich = new ClipboardProbe(ledger.Service<PulseOffice.Exchange.IExchangeClipboard>());
        await rich.SetAsync(new ClipboardContent
        {
            Text = "a\tb",
            Html = "<table border=\"1\"><tr><td>a</td><td style=\"border:1.5pt solid #FF0000\">b</td><td style=\"border:none\">c</td></tr></table>",
        });
        ledger.Select(0, 0, 0, 0);

        await ledger.Service<ISheetClipboardService>().PasteAsync();
        Dispatcher.UIThread.RunJobs();

        var styles = ledger.Sheet.Workbook!.Styles;
        var plain = styles.Get(ledger.Active.StyleIdAt(new CellAddress(0, 0))).Borders;
        Assert.True(plain.Top.IsVisible && plain.Left.IsVisible && plain.Right.IsVisible && plain.Bottom.IsVisible);
        Assert.Equal(PulseOffice.Sheets.Styling.BorderStyle.Thin, plain.Top.Style);
        var red = styles.Get(ledger.Active.StyleIdAt(new CellAddress(0, 1))).Borders.Bottom;
        Assert.Equal(("#FF0000", PulseOffice.Sheets.Styling.BorderStyle.Medium), (red.ColorHex, red.Style));
        Assert.True(styles.Get(ledger.Active.StyleIdAt(new CellAddress(0, 2))).Borders.IsEmpty);
    }, CancellationToken.None);

    [Fact]
    public Task Maiusc_F10_sulla_griglia_apre_il_menu_della_cella_da_tastiera() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Select(1, 1, 1, 1);
        ledger.Grid().Focus();
        Dispatcher.UIThread.RunJobs();
        ledger.Press(Key.F10, PhysicalKey.F10, RawInputModifiers.Shift);
        var menu = ledger.Grid().ContextMenu;
        Assert.NotNull(menu);
        Assert.True(menu!.IsOpen);
        ledger.Press(Key.Escape, PhysicalKey.Escape);
        Assert.False(menu.IsOpen);
    }, CancellationToken.None);

    [Fact]
    public Task Scrivere_in_cella_riempire_ordinare_e_annullare() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("3");
        ledger.Type("1");
        ledger.Type("2");
        ledger.Type("=SUM(A1:A3)");

        Assert.Equal(3, ledger.Value(0, 0).AsNumber());
        Assert.Equal(6, ledger.Value(3, 0).AsNumber());

        ledger.Select(0, 0, 2, 0);
        await ledger.Run("ledger.data.sortAscending");
        Assert.Equal([1d, 2d, 3d], [ledger.Value(0, 0).AsNumber(), ledger.Value(1, 0).AsNumber(), ledger.Value(2, 0).AsNumber()]);
        Assert.Equal(6, ledger.Value(3, 0).AsNumber());

        await ledger.Run("shell.edit.undo");
        Assert.Equal([3d, 1d, 2d], [ledger.Value(0, 0).AsNumber(), ledger.Value(1, 0).AsNumber(), ledger.Value(2, 0).AsNumber()]);
        await ledger.Run("shell.edit.redo");
        Assert.Equal(1, ledger.Value(0, 0).AsNumber());

        ledger.Select(0, 0, 4, 0);
        await ledger.Run("ledger.fill.down");
        Assert.Equal(1, ledger.Value(4, 0).AsNumber());
        await ledger.Run("shell.edit.undo");
        Assert.Equal(2, ledger.Value(1, 0).AsNumber());
        Assert.Equal(6, ledger.Value(3, 0).AsNumber());
        Assert.True(ledger.Value(4, 0).IsEmpty);
    }, CancellationToken.None);

    [Fact]
    public Task Elimina_pivot_toglie_solo_quella_sotto_il_cursore_e_si_annulla() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        var workbook = ledger.Sheet.Workbook!;
        var sheet = ledger.Active.Name;
        PulseOffice.Sheets.Pivot.PivotDefinition Pivot(string id, int row) => new()
        {
            Id = id,
            Name = id,
            SourceSheet = sheet,
            SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            Destination = new CellAddress(row, 5),
            DestinationSheet = sheet,
            OutputRange = new CellRange(new CellAddress(row, 5), new CellAddress(row + 3, 6)),
        };
        workbook.Pivots.Add(Pivot("prima", 0));
        workbook.Pivots.Add(Pivot("seconda", 10));

        ledger.Select(1, 1, 1, 1);
        await ledger.Run("ledger.pivot.delete");
        Assert.Equal(["prima", "seconda"], workbook.Pivots.Select(pivot => pivot.Id));

        ledger.Select(11, 6, 11, 6);
        await ledger.Run("ledger.pivot.delete");
        Assert.Equal(["prima"], workbook.Pivots.Select(pivot => pivot.Id));

        await ledger.Run("shell.edit.undo");
        Assert.Equal(["prima", "seconda"], workbook.Pivots.Select(pivot => pivot.Id).Order());
    }, CancellationToken.None);

    [Fact]
    public Task Il_pulsante_Scrittura_file_sta_nella_barra_con_icona_nome_accessibile_e_area_di_clic() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Start("ledger.macro.editor");
        Dispatcher.UIThread.RunJobs();
        var texts = ledger.Service<PulseOffice.Localization.ITextCatalog>();
        var button = Assert.Single(ledger.Find<PulseOffice.Scripting.Editor.MacroWriteAccessButton>());
        var header = Assert.IsType<Avalonia.Controls.Grid>(button.Parent);
        var tools = header.Children.OfType<Avalonia.Controls.WrapPanel>().Single();
        Assert.Contains(tools.Children.OfType<Avalonia.Controls.Button>(), candidate => Avalonia.Automation.AutomationProperties.GetName(candidate) == texts["macro.run"]);

        var buttonTop = button.TranslatePoint(default, ledger.Service<ShellWindow>())!.Value.Y;
        var firstTop = tools.Children[0].TranslatePoint(default, ledger.Service<ShellWindow>())!.Value.Y;
        Assert.InRange(buttonTop, firstTop - 4, firstTop + 4);
        Assert.NotNull(button.Background);
        Assert.True(button.Bounds.Height >= 24 && button.Bounds.Width >= 80, button.Bounds.ToString());
        Assert.Equal(texts["macro.fileWrite.off"], Avalonia.Automation.AutomationProperties.GetName(button));
        Assert.Equal(texts["macro.fileWrite.tip"], Avalonia.Automation.AutomationProperties.GetHelpText(button));
        Assert.Equal("macro.fileWrite", Avalonia.Automation.AutomationProperties.GetAutomationId(button));
        Assert.NotEmpty(button.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>());
        Assert.True(button.IsHitTestVisible && button.IsEffectivelyEnabled && button.IsEffectivelyVisible);
    }, CancellationToken.None);

    [Fact]
    public Task Il_pulsante_Scrittura_file_chiede_conferma_e_concede_il_permesso() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Start("ledger.macro.editor");
        var button = Assert.Single(ledger.Find<PulseOffice.Scripting.Editor.MacroWriteAccessButton>());
        Assert.False(button.IsGranted);

        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var prompt = Assert.Single(ledger.Find<PulseOffice.UI.Controls.ChoicePromptView>());
        var accept = ledger.Find<Avalonia.Controls.Button>().First(candidate => Equals(candidate.Tag, true) || (candidate.Content as string) == ledger.Service<PulseOffice.Localization.ITextCatalog>()["macro.fileWrite.accept"]);
        accept.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.IsGranted);
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.False(button.IsGranted);
    }, CancellationToken.None);

    [Fact]
    public Task Unire_inserire_righe_e_bloccare_i_riquadri() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("10");
        ledger.Type("=A1*2");

        ledger.Select(0, 0, 0, 0);
        await ledger.Run("ledger.sheet.insertRowsAbove");
        Assert.True(ledger.Value(0, 0).IsEmpty);
        Assert.Equal(20, ledger.Value(2, 0).AsNumber());
        Assert.Equal("=A2*2", ledger.Active.TryGetCell(new CellAddress(2, 0), out var cell) ? cell.Formula?.Replace(" ", "") : null);

        await ledger.Run("ledger.sheet.deleteRows");
        Assert.Equal(20, ledger.Value(1, 0).AsNumber());

        ledger.Select(3, 0, 4, 2);
        await ledger.Run("ledger.merge.cells");
        Assert.Equal(new CellRange(new CellAddress(3, 0), new CellAddress(4, 2)), ledger.Active.Merges.At(new CellAddress(4, 1)));
        await ledger.Run("shell.edit.undo");
        Assert.Null(ledger.Active.Merges.At(new CellAddress(4, 1)));

        ledger.Select(2, 1, 2, 1);
        await ledger.Run("ledger.view.freezeAtSelection");
        Assert.Equal(new FrozenPanes(2, 1), ledger.Active.Frozen);
        await ledger.Run("ledger.view.unfreeze");
        Assert.Equal(FrozenPanes.None, ledger.Active.Frozen);
    }, CancellationToken.None);

    [Fact]
    public Task Le_formule_si_scrivono_nella_lingua_dell_interfaccia_e_la_sola_lettura_blocca_la_scrittura() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        var texts = ledger.Service<PulseOffice.Localization.ITextCatalog>();
        var before = texts.Language;
        try
        {
            texts.Use(PulseOffice.Localization.UiLanguage.Italian);
            ledger.Type("4");
            ledger.Type("=SOMMA(A1;5)");
            Assert.Equal(9, ledger.Value(1, 0).AsNumber());
            Assert.True(ledger.Active.TryGetCell(new CellAddress(1, 0), out var cell));
            using var xlsx = new MemoryStream();
            new PulseOffice.Formats.OpenXml.XlsxWriter().Write(ledger.Sheet.Workbook!, xlsx);
            using var package = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(new MemoryStream(xlsx.ToArray()), false);
            var formula = package.WorkbookPart!.WorksheetParts.SelectMany(part => part.Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.CellFormula>()).Single();
            Assert.Equal("SUM(A1,5)", formula.Text.Replace(" ", ""));
        }
        finally
        {
            texts.Use(before);
        }

        ledger.Service<IDocumentWorkspace>().Active!.IsReadOnly = true;
        Dispatcher.UIThread.RunJobs();
        ledger.Type("99");
        Assert.True(ledger.Value(2, 0).IsEmpty);
        ledger.Select(0, 0, 1, 0);
        await ledger.Run("ledger.data.sortDescending");
        Assert.Equal(4, ledger.Value(0, 0).AsNumber());
    }, CancellationToken.None);

    [Fact]
    public Task Copiare_incollare_e_incollare_testo_a_tabulazioni() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("7");
        ledger.Type("=A1+1");

        ledger.Select(0, 0, 1, 0);
        await ledger.Run("ledger.clipboard.copy");
        ledger.Select(0, 2, 0, 2);
        await ledger.Run("ledger.clipboard.paste");
        Assert.Equal(7, ledger.Value(0, 2).AsNumber());
        Assert.Equal(8, ledger.Value(1, 2).AsNumber());
        Assert.True(ledger.Active.TryGetCell(new CellAddress(1, 2), out var moved));
        Assert.Equal("=C1+1", moved.Formula?.Replace(" ", ""));

        await ledger.Service<PulseOffice.UI.Controls.Services.ISystemClipboardText>().SetTextAsync("1\t2\n3\t4");
        ledger.Select(4, 0, 4, 0);
        await ledger.Run("ledger.clipboard.pasteText");
        Assert.Equal([1d, 2d, 3d, 4d], [ledger.Value(4, 0).AsNumber(), ledger.Value(4, 1).AsNumber(), ledger.Value(5, 0).AsNumber(), ledger.Value(5, 1).AsNumber()]);
    }, CancellationToken.None);

    [Fact]
    public Task Filtro_automatico_trova_e_sostituisci_e_tema_scuro() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("Nome");
        ledger.Type("beta");
        ledger.Type("alfa");
        ledger.Type("beta");

        Assert.Equal("alfa", ledger.Value(2, 0).ToString());

        ledger.Select(0, 0, 0, 0);
        await ledger.Run("ledger.data.autoFilter");
        Assert.Equal(new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)), ledger.Active.AutoFilter.Range);
        ledger.Sheet.ApplyRowFilter(ledger.Active, new HashSet<int> { 2 });
        Dispatcher.UIThread.RunJobs();
        Assert.True(ledger.Active.Layout.IsRowHidden(2));
        await ledger.Run("ledger.data.autoFilter");
        Assert.Null(ledger.Active.AutoFilter.Range);
        Assert.False(ledger.Active.Layout.IsRowHidden(2));

        var find = new LedgerFindTarget(ledger.Sheet, ledger.Service<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            ledger.Service<PulseOffice.Localization.ITextCatalog>(), ledger.Service<PulseOffice.Sheets.ISheetEditGuard>(), new PulseOffice.Sheets.Engine.Operations.WorkbookSearch(), readOnly: false);
        find.ReplaceAll(new PulseOffice.UI.Controls.Search.FindRequest("beta", "gamma", new HashSet<string>()));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("gamma", ledger.Value(1, 0).ToString());
        Assert.Equal("gamma", ledger.Value(3, 0).ToString());
        Assert.Equal("alfa", ledger.Value(2, 0).ToString());
        await ledger.Run("shell.edit.undo");
        Assert.Equal("beta", ledger.Value(1, 0).ToString());

        var theme = ledger.Service<PulseOffice.UI.Theming.IThemeService>();
        var before = theme.Preference;
        try
        {
            theme.ThemeChanged += Repaint;
            theme.Apply(PulseOffice.UI.Theming.ThemePreference.Dark);
            Dispatcher.UIThread.RunJobs();
            Assert.True(theme.IsDark);
            Assert.True(ledger.Brightness() < 100, $"luminosità {ledger.Brightness()}");
            theme.Apply(PulseOffice.UI.Theming.ThemePreference.Light);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ledger.Brightness() > 150, $"luminosità {ledger.Brightness()}");
        }
        finally
        {
            theme.Apply(before);
            theme.ThemeChanged -= Repaint;
            PulseOffice.UI.Theming.Palette.Apply(Application.Current!, theme.IsDark);
        }

        void Repaint(object? sender, EventArgs e) => PulseOffice.UI.Theming.Palette.Apply(Application.Current!, theme.IsDark);
    }, CancellationToken.None);

    [Fact]
    public Task Una_riga_scritta_con_Tab_oltre_il_bordo_della_finestra_non_perde_valori_e_Invio_torna_a_capo() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        for (var column = 0; column < 40; column++)
        {
            ledger.Write((column + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            ledger.Press(column < 39 ? Key.Tab : Key.Enter, column < 39 ? PhysicalKey.Tab : PhysicalKey.Enter);
        }

        ledger.Press(Key.F2, PhysicalKey.F2);
        ledger.Write("41");
        ledger.Press(Key.Enter, PhysicalKey.Enter);

        Assert.All(Enumerable.Range(0, 40), column => Assert.Equal(column + 1, ledger.Value(0, column).AsNumber()));
        Assert.Equal(41, ledger.Value(1, 0).AsNumber());
    }, CancellationToken.None);

    [Fact]
    public Task La_barra_della_formula_segue_cio_che_si_scrive_nella_cella_ed_Esc_la_ripristina() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("7");
        ledger.Press(Key.Up, PhysicalKey.ArrowUp);
        ledger.Press(Key.F2, PhysicalKey.F2);
        ledger.Write("+1");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("7+1", ledger.FormulaBar());

        ledger.Press(Key.Escape, PhysicalKey.Escape);
        Assert.Equal("7", ledger.FormulaBar());
    }, CancellationToken.None);

    [Fact]
    public Task Una_data_o_una_percentuale_scritte_in_cella_diventano_numeri_formattati() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        try
        {
            ledger.Type("31/12/2026");
            ledger.Type("12,5%");

            Assert.Equal(46387, ledger.Value(0, 0).AsNumber());
            Assert.Equal(0.125, ledger.Value(1, 0).AsNumber(), 9);
            var workbook = ledger.Sheet.Workbook!;
            string Format(int row) => workbook.NumberFormats.CodeOf(workbook.Styles.Get(ledger.Active[new CellAddress(row, 0)].StyleId).NumberFormatId);
            Assert.Equal("dd/mm/yyyy", Format(0));
            Assert.Equal("0.00%", Format(1));

            await ledger.Run("shell.edit.undo");
            await ledger.Run("shell.edit.undo");
            Assert.True(ledger.Value(0, 0).IsEmpty);
            Assert.Equal(0, ledger.Active[new CellAddress(0, 0)].StyleId);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }, CancellationToken.None);

    [Fact]
    public Task Invio_nella_casella_della_dimensione_del_carattere_applica_la_dimensione_senza_chiudere_l_app() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("Titolo");
        ledger.Select(0, 0, 0, 0);

        var box = ledger.FontSizeBox();
        box.Focus();
        box.Text = "16";
        ledger.Press(Key.Enter, PhysicalKey.Enter);

        var workbook = ledger.Sheet.Workbook!;
        Assert.Equal(16, workbook.Styles.Get(ledger.Active[new CellAddress(0, 0)].StyleId).Font.Size);
    }, CancellationToken.None);

    [Fact]
    public Task Un_errore_imprevisto_nell_interfaccia_non_chiude_l_app_ma_lo_dice_e_salva_per_il_recupero() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("42");
        var guard = ledger.Service<PulseOffice.Shell.IInterfaceFaultGuard>();

        Assert.True(guard.Absorb(new InvalidOperationException("prova")));
        Assert.Contains(ledger.Service<PulseOffice.UI.Controls.Services.IStatusBarService>().Segments,
            segment => segment.Text?.Contains("recupero", StringComparison.OrdinalIgnoreCase) == true
                || segment.Text?.Contains("recovery", StringComparison.OrdinalIgnoreCase) == true);

        for (var fault = 0; fault < 4; fault++) guard.Absorb(new InvalidOperationException("prova"));
        Assert.False(guard.Absorb(new InvalidOperationException("prova")));
        var idle = ledger.Service<IDocumentRecovery>().WhenIdleAsync();
        for (var spin = 0; spin < 1000 && !idle.IsCompleted; spin++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Assert.True(idle.IsCompleted);
        Assert.NotEmpty(Directory.GetFiles(_dataHome, "*.dati", SearchOption.AllDirectories));
    }, CancellationToken.None);

    [Fact]
    public Task I_pulsanti_dei_formati_numerici_della_ribbon_sono_attivi_con_celle_selezionate() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("42");
        ledger.Select(0, 0, 3, 3);
        ledger.Brightness();

        foreach (var command in new[] { "ledger.format.bold", "ledger.number.currency", "ledger.number.percent" })
            Assert.True(ledger.Slot(command).IsEffectivelyEnabled, command);
    }, CancellationToken.None);

    [Fact]
    public Task Il_testo_di_celle_unite_si_vede_anche_quando_la_prima_colonna_e_fuori_vista() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("Bilancio annuale");
        ledger.Select(0, 0, 0, 39);
        await ledger.Run("ledger.merge.centre");
        ledger.Select(5, 0, 5, 0);

        int DarkPixelsInFirstRow()
        {
            using var bitmap = ledger.Frame();
            var origin = ledger.GridOrigin();
            var grid = ledger.Grid();
            var top = (int)(origin.Y + grid.Geometry.ColumnHeaderHeight + 3);
            var dark = 0;
            for (var x = (int)origin.X + 60; x < Math.Min(bitmap.Width, (int)(origin.X + grid.Bounds.Width)); x++)
                for (var y = top; y < top + 12; y++)
                    if (bitmap.GetPixel(x, y) is var pixel && pixel.Red + pixel.Green + pixel.Blue < 450) dark++;
            return dark;
        }

        ledger.Grid().EnsureVisible(new CellAddress(5, 30));
        Assert.True(DarkPixelsInFirstRow() > 20);
    }, CancellationToken.None);

    [Fact]
    public Task Una_cella_con_convalida_a_elenco_apre_l_elenco_con_Alt_Giu() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Active.Validations.Add(new DataValidation
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
            Kind = ValidationKind.List,
            ListValues = ["Entrata", "Uscita"],
        });
        ledger.Select(0, 0, 0, 0);
        var view = (LedgerView)ledger.Service<PulseOffice.Shell.IDocumentWorkspace>().Active!.View;

        Assert.True(ledger.Grid().ChoicesAvailable(new CellAddress(0, 0)));
        Assert.False(ledger.Grid().ChoicesAvailable(new CellAddress(1, 0)));
        ledger.Press(Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        Assert.True(view.Choices.IsOpen);
        ledger.Press(Key.Down, PhysicalKey.ArrowDown);
        ledger.Press(Key.Enter, PhysicalKey.Enter);

        Assert.False(view.Choices.IsOpen);
        Assert.Equal("Uscita", ledger.Value(0, 0).AsText());
    }, CancellationToken.None);

    [Fact]
    public Task Su_un_foglio_protetto_la_cella_bloccata_non_apre_l_editor() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("1");
        ledger.Press(Key.Up, PhysicalKey.ArrowUp);
        ledger.Active.Protection = ledger.Active.Protection with { IsEnabled = true };

        ledger.Press(Key.F2, PhysicalKey.F2);
        ledger.Write("99");

        Assert.Equal("1", ledger.FormulaBar());
        Assert.Equal(1, ledger.Value(0, 0).AsNumber());
    }, CancellationToken.None);

    [Fact]
    public Task Enter_in_the_formula_bar_without_changes_on_a_locked_cell_moves_on_without_an_error() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("1");
        ledger.Press(Key.Up, PhysicalKey.ArrowUp);
        ledger.Active.Protection = ledger.Active.Protection with { IsEnabled = true };
        var locked = ledger.Service<PulseOffice.Localization.ITextCatalog>()["status.protection.locked"];
        var status = ledger.Service<PulseOffice.UI.Controls.Services.IStatusBarService>();
        var reported = false;
        status.Changed += (_, _) => reported |= status.Segments.Any(segment => segment.Text == locked);

        await ledger.Run("ledger.view.focusFormulaBar");
        ledger.Press(Key.Enter, PhysicalKey.Enter);

        Assert.False(reported);
        Assert.Equal(new CellAddress(1, 0), ledger.Sheet.Selection.Start);
        Assert.Equal(1, ledger.Value(0, 0).AsNumber());
    }, CancellationToken.None);

    [Fact]
    public Task Cambiando_foglio_la_ribbon_mostra_il_formato_della_cella_del_nuovo_foglio() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("Titolo");
        ledger.Select(0, 0, 0, 0);
        await ledger.Run("ledger.format.bold");
        ledger.Brightness();
        Assert.True(ledger.Slot("ledger.format.bold").Item.IsChecked);

        await ledger.Run("ledger.worksheet.insert");
        ledger.Sheet.SelectSheet(1);
        Dispatcher.UIThread.RunJobs();
        ledger.Brightness();

        Assert.Equal(1, ledger.Sheet.SheetIndex);
        Assert.NotEqual(true, ledger.Slot("ledger.format.bold").Item.IsChecked);
    }, CancellationToken.None);

    [Fact]
    public Task I_nomi_creati_dalle_intestazioni_hanno_riferimenti_assoluti_e_valgono_ovunque() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("Entrate");
        ledger.Type("10");
        ledger.Type("20");
        ledger.Select(0, 0, 2, 0);
        await ledger.Run("ledger.name.fromLabels");

        var name = Assert.Single(ledger.Sheet.Workbook!.DefinedNames);
        Assert.Contains("$A$2:$A$3", name.Expression, StringComparison.Ordinal);
    }, CancellationToken.None);

    [Fact]
    public Task Ingrandendo_il_carattere_la_riga_si_alza_per_contenerlo() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Type("Titolo");
        ledger.Select(0, 0, 0, 3);
        await ledger.Run("ledger.merge.centre");
        var before = ledger.Active.Layout.RowHeight(0);

        var box = ledger.FontSizeBox();
        box.Focus();
        box.Text = "20";
        ledger.Press(Key.Enter, PhysicalKey.Enter);
        Dispatcher.UIThread.RunJobs();

        Assert.True(ledger.Active.Layout.RowHeight(0).Value > before.Value, $"{before} -> {ledger.Active.Layout.RowHeight(0)}");
    }, CancellationToken.None);
    [Fact]
    public Task La_maniglia_di_regolazione_di_una_forma_segue_il_puntatore_e_si_annulla() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Select(2, 2, 2, 2);
        var cell = ledger.Grid().ActiveCellBounds();
        var placed = new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(2, 2),
            Size = new PulseOffice.Foundation.Units.DocumentSize(new PulseOffice.Foundation.Units.Twip(3000), new PulseOffice.Foundation.Units.Twip(1500)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "forma", Shape = PulseOffice.Drawing.Shapes.ShapeKind.RoundedRectangle },
        };
        ledger.Active.Objects.Add(placed);
        ledger.Sheet.SelectObjects([placed]);
        Dispatcher.UIThread.RunJobs();
        var side = Math.Min(GridGeometry.ToPixels(placed.Size.Width), GridGeometry.ToPixels(placed.Size.Height));
        var origin = ledger.GridOrigin();

        ledger.Drag(new Avalonia.Point(origin.X + cell.X + side / 6, origin.Y + cell.Y), new Avalonia.Point(origin.X + cell.X + side * 0.4, origin.Y + cell.Y));

        var adjusted = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Assert.Single(ledger.Active.Objects).Content);
        var preset = Assert.IsType<PulseOffice.Drawing.Shapes.Geometry.PresetGeometry>(adjusted.Computed);
        Assert.InRange(preset.Adjustments.Single().Value, 39000, 41000);
        Assert.Equal(new CellAddress(2, 2), Assert.Single(ledger.Active.Objects).Anchor);

        await ledger.Run("shell.edit.undo");
        Assert.Null(Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Assert.Single(ledger.Active.Objects).Content).Computed);
    }, CancellationToken.None);

    [Fact]
    public Task Scrivere_con_una_forma_selezionata_entra_nel_testo_della_forma_e_non_nella_cella() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Select(2, 2, 2, 2);
        var placed = new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(2, 2),
            Size = new PulseOffice.Foundation.Units.DocumentSize(new PulseOffice.Foundation.Units.Twip(3000), new PulseOffice.Foundation.Units.Twip(1500)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "forma", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse },
        };
        ledger.Active.Objects.Add(placed);
        ledger.Sheet.SelectObjects([placed]);
        Dispatcher.UIThread.RunJobs();

        ledger.Write("Ciao ");
        Assert.True(ledger.Grid().LiveShape is { Text: "Ciao " });
        Assert.False(ledger.IsChecked("ledger.format.bold"));
        await ledger.Run("ledger.format.bold");
        Assert.True(ledger.IsChecked("ledger.format.bold"));
        await ledger.Run("ledger.format.italic");
        await ledger.Run("ledger.format.underline");
        Assert.True(ledger.IsChecked("ledger.format.italic") && ledger.IsChecked("ledger.format.underline"));
        ledger.Write("mondo");
        ledger.Press(Key.Escape, PhysicalKey.Escape);

        var shape = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Assert.Single(ledger.Active.Objects).Content);
        Assert.Equal("Ciao mondo", shape.Text);
        Assert.Contains(shape.Runs!, run => run.Bold);
        Assert.Equal(CellValue.Empty, ledger.Value(2, 2));
        Assert.Null(ledger.Grid().LiveShape);

        Assert.Equal("mondo", string.Concat(shape.Runs!.Where(run => run.Bold && run.Italic && run.Underline).Select(run => run.Text)));
        await ledger.Run("shell.edit.undo");
        Assert.Null(Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Assert.Single(ledger.Active.Objects).Content).Text);
    }, CancellationToken.None);

    [Fact]
    public Task Con_il_testo_di_una_forma_in_modifica_restano_attivi_solo_i_comandi_del_carattere() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Select(2, 2, 2, 2);
        var placed = new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(2, 2),
            Size = new PulseOffice.Foundation.Units.DocumentSize(new PulseOffice.Foundation.Units.Twip(3000), new PulseOffice.Foundation.Units.Twip(1500)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "forma", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse },
        };
        ledger.Active.Objects.Add(placed);
        Assert.True(ledger.CanRun("ledger.number.percent"));
        ledger.Sheet.SelectObjects([placed]);
        Dispatcher.UIThread.RunJobs();
        ledger.Write("Ciao");

        Assert.True(ledger.CanRun("ledger.format.bold"));
        Assert.True(ledger.CanRun("ledger.font.size"));
        Assert.True(ledger.CanRun("ledger.font.color"));
        Assert.False(ledger.CanRun("ledger.number.percent"));
        Assert.False(ledger.CanRun("ledger.fill.color"));
        Assert.False(ledger.CanRun("ledger.border.apply"));
        Assert.False(ledger.CanRun("ledger.align.wrap"));
        Assert.False(ledger.CanRun("ledger.format.cells"));
        Assert.False(ledger.CanRun("ledger.style.apply"));

        ledger.Press(Key.Escape, PhysicalKey.Escape);
        Assert.False(ledger.CanRun("ledger.number.percent"));
        ledger.Sheet.SelectObjects([]);
        Dispatcher.UIThread.RunJobs();
        Assert.True(ledger.CanRun("ledger.number.percent"));
        Assert.True(ledger.CanRun("ledger.format.cells"));
    }, CancellationToken.None);

    [Fact]
    public Task La_griglia_annuncia_la_cella_attiva_ai_lettori_di_schermo() => Session.Dispatch(async () =>
    {
        await using var ledger = await Open();
        ledger.Select(1, 1, 1, 1);
        ledger.Type("1234,5");
        ledger.Select(1, 1, 1, 1);

        var name = Avalonia.Automation.AutomationProperties.GetName(ledger.Grid());
        Assert.Contains("B2", name);
        Assert.Contains("1234,5", name);

        ledger.Select(5, 5, 5, 5);
        Assert.Contains("F6", Avalonia.Automation.AutomationProperties.GetName(ledger.Grid()));
        Assert.Contains("vuota", Avalonia.Automation.AutomationProperties.GetName(ledger.Grid()));

        await ledger.Run("ledger.view.focusNameBox");
        Assert.Equal("Casella del nome", ledger.FocusedName());
        await ledger.Run("ledger.view.focusFormulaBar");
        Assert.Equal("FormulaBar", ledger.FocusedControlName());
    }, CancellationToken.None);
}
