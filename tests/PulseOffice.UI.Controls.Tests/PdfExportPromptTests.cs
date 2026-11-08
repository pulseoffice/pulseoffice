using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Printing;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class PdfExportPromptTests
{
    [Fact]
    public Task Un_intervallo_senza_pagine_blocca_l_esportazione_sotto_il_campo() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = new PdfExportView(new PdfExportDefaults("t", "a", "s", "k", 4), new PulseOffice.Printing.PageRangeParser(), new TextCatalog(UiLanguage.Italian), new AvaloniaPageCanvasFactory(new DefaultDisplayFonts()));
        var window = new Window { Content = view, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.RangeText = "tdf-noi";
        Assert.False(view.CanExport);

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.RangeText = "2-3";
        Assert.True(view.CanExport);
        Assert.Equal([1, 2], view.ExportedPages);
        view.Confirm();
        Assert.Equal([1, 2], Assert.IsType<PdfExportChoice>(result).Options.Pages);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Immagini_e_sicurezza_arrivano_nelle_opzioni_dell_esportatore() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = new PdfExportView(new PdfExportDefaults("t", "a", "s", "k", 2), new PulseOffice.Printing.PageRangeParser(), new TextCatalog(UiLanguage.Italian), new AvaloniaPageCanvasFactory(new DefaultDisplayFonts()));
        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.QualityEnabled);
        view.SetImages(jpeg: true, quality: 60, maxResolution: 150);
        Assert.True(view.QualityEnabled);

        view.SetPasswords("apri", "apr", "", "");
        Assert.False(view.CanExport);
        view.SetPasswords("apri", "apri", "capo", "capo");
        Assert.True(view.CanExport);
        view.SetPermissions(PulseOffice.Formats.Pdf.PdfPrintPermission.LowResolution, copy: false, changes: true, comments: true);

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.Confirm();
        var options = Assert.IsType<PdfExportChoice>(result).Options;
        Assert.Equal(PulseOffice.Formats.Pdf.PdfImageCompression.Jpeg, options.Images.Compression);
        Assert.Equal(60, options.Images.JpegQuality);
        Assert.Equal(150, options.Images.MaxResolution);
        var security = Assert.IsType<PulseOffice.Formats.Pdf.PdfSecurityOptions>(options.Security);
        Assert.Equal("apri", security.OpenPassword);
        Assert.Equal("capo", security.PermissionsPassword);
        Assert.Equal(PulseOffice.Formats.Pdf.PdfPrintPermission.LowResolution, security.Printing);
        Assert.False(security.AllowCopy);

        view.ArchiveChecked = true;
        Assert.False(view.SecurityEnabled);
        view.SetPasswords("x", "y", "", "");
        Assert.True(view.CanExport);
        view.Confirm();
        Assert.Null(Assert.IsType<PdfExportChoice>(result).Options.Security);
        window.Close();
    }, CancellationToken.None);

    private sealed class DefaultDisplayFonts : PulseOffice.UI.Controls.Fonts.IDisplayFonts
    {
        public Avalonia.Media.FontFamily Resolve(string? family) => Avalonia.Media.FontFamily.Default;

        public string? Effective(string? family) => family;
    }
}
