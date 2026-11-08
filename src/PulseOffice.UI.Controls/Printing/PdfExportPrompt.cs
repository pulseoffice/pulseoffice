using PulseOffice.Localization;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Printing;

public sealed record PdfPreviewLayout(string Label, Func<IPrintableDocument> Build);

public sealed record PdfExportDefaults(string Title, string Author, string Subject, string Keywords, int PageCount)
{
    public bool HasComments { get; init; }

    public bool HasHeadings { get; init; } = true;

    public string? Language { get; init; }

    public Func<IPrintableDocument>? Preview { get; init; }

    public string LayoutLabel { get; init; } = string.Empty;

    public IReadOnlyList<PdfPreviewLayout> Layouts { get; init; } = [];
}

public sealed record PdfExportChoice(PulseOffice.Formats.Pdf.PdfExportOptions Options, int? Layout);

public interface IPdfExportPrompt
{
    Task<PdfExportChoice?> AskAsync(PdfExportDefaults defaults, CancellationToken ct = default);
}

public sealed class PdfExportPrompt(IDialogService dialogs, IPageRangeParser ranges, ITextCatalog texts, IPageCanvasFactory canvases) : IPdfExportPrompt
{
    public Task<PdfExportChoice?> AskAsync(PdfExportDefaults defaults, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        return dialogs.ShowAsync<PdfExportChoice>(
            new PdfExportView(defaults, ranges, texts, canvases),
            new DialogOptions { TitleKey = "dialog.exportPdf.title", PreferredWidth = 860 }, ct);
    }
}
