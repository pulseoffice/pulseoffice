using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Charts;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
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
public sealed class LedgerPrintTests(HeadlessDesktopSession headless)
{
    private sealed class Images : IPageCanvas
    {
        public int Count { get; private set; }

        public DocumentSize PageSize => new(Twip.FromPoints(595), Twip.FromPoints(842));

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * 5.0);

        public void FillRectangle(DocumentRect rect, PrintColor color) { }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness) { }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, TextAlignment alignment = TextAlignment.Left, TextBaseline baseline = TextBaseline.Middle) { }

        public void DrawPath(PrintPath path) { }

        public void DrawImage(PageImage image, DocumentRect bounds) => Count++;
    }

    [Fact]
    public Task Il_grafico_del_foglio_finisce_nella_stampa_e_nel_PDF() => headless.Session.Dispatch(async () =>
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();

        var sheet = provider.GetRequiredService<IActiveSheetContext>().Workbook!.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Mese"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Ricavi"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Gen"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(10));
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(3, 0),
            Size = new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(5)),
            Content = new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = sheet.Name, SourceRange = "A1:B2" },
        });

        var document = provider.GetRequiredService<ISheetPrintDocumentBuilder>().Build("prova")!;
        var images = new Images();
        for (var page = 0; page < document.PageCount; page++) document.PaintPage(page, images);

        Assert.Equal(1, images.Count);
        window.Close();
        await provider.DisposeAsync();
    }, CancellationToken.None);
}
