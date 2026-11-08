using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Templates;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class TemplateGalleryTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-templates-" + Guid.NewGuid().ToString("N"));

    public TemplateGalleryTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    [Fact]
    public Task I_modelli_dei_tre_moduli_hanno_la_miniatura_della_prima_pagina_e_il_filtro_per_modulo() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        provider.GetRequiredService<ShellWindow>().Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var operations = provider.GetRequiredService<IDocumentOperationsCatalog>();
        var library = provider.GetRequiredService<ITemplateLibrary>();
        Directory.CreateDirectory(library.Folder);

        foreach (var module in new[] { "documents", "sheets", "presentations" })
        {
            await executor.ExecuteAsync(registry.Find(new CommandId($"shell.file.new.{module}"))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            var document = workspace.Active!;
            if (document.Model is PulseOffice.Sheets.Workbook workbook)
                workbook.Sheets[0].SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), PulseOffice.Sheets.Values.CellValue.Text("Bilancio"));
            var writer = operations.For(document)!;
            Assert.True(writer.WriteTo(document, Path.Combine(library.Folder, $"Modello {module}.{writer.TemplateExtension}")));
        }

        var entries = library.Entries();
        Assert.Equal(3, entries.Count);
        Assert.All(entries, entry => Assert.True(library.FirstPages(entry)?.PageCount > 0, entry.Name));

        var cards = entries.Select(entry => new TemplateCard(
            entry.Path, entry.Name, entry.Module.ToString(), ModuleIdentity.For(entry.Module).BrandName,
            ModuleIdentity.For(entry.Module).AccentHex, entry.Modified, () => library.FirstPages(entry))).ToList();
        var view = new TemplateGalleryView(cards, provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            new PulseOffice.UI.Controls.Galleries.GalleryNavigator(), provider.GetRequiredService<PulseOffice.UI.Controls.Printing.IPageCanvasFactory>());
        var window = new Window { Content = view, Width = 900, Height = 560 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, view.Gallery!.Items.Count);
        Assert.NotNull(view.Selected);
        Assert.False(view.LargePreview!.ShowsPlaceholder);

        view.Filter.SelectKey(nameof(ModuleKind.Presentations));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Deck", Assert.Single(view.Gallery.Items).Value.ModuleName);
        Assert.Equal("Deck", view.Selected!.ModuleName);

        object? chosen = null;
        view.Completed += (_, value) => chosen = value;
        view.Confirm();
        Assert.Same(view.Selected, chosen);

        var thumbnail = new PageThumbnail(cards[0].Pages, Avalonia.Media.Colors.Blue, "F", provider.GetRequiredService<PulseOffice.UI.Controls.Printing.IPageCanvasFactory>()) { Width = 120, Height = 160 };
        thumbnail.Measure(new Size(120, 160));
        thumbnail.Arrange(new Rect(0, 0, 120, 160));
        using var bitmap = new RenderTargetBitmap(new PixelSize(120, 160), new Vector(96, 96));
        bitmap.Render(thumbnail);
        var image = new BitmapPixels().Read(bitmap);
        var center = ((80 * image.Width) + 60) * 4;
        Assert.Equal(0xFF, image.Pixels[center + 2]);
        Assert.Equal(0, image.Pixels[3]);

        window.Close();
    }, CancellationToken.None);
}
