using PulseOffice.UI.Controls.Notifications;
using System.Text;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Pdf;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Presentations;
using PulseOffice.Printing;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public sealed class DeckExportCommands(
    IDocumentWorkspace workspace,
    IDialogService dialogs,
    IFileDialogService files,
    IVideoEncoderProvider videoEncoders,
    IDeckPublisher publisher,
    IStatusBarService status,
    ITextCatalog texts,
    IAtomicFileWriter atomic,
    Design.ImageExportServices imageExport,
    IDeckVideoFrameSourceFactory videoFrames,
    IVideoSlideDuration videoDurations,
    IDiagnosticTexts diagnostics)
{
    private const int VideoFps = 30;

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Export("deck.export.images", ExportImagesAsync),
        Export("deck.export.html", ExportHtmlAsync),
        Export("deck.export.video", ExportVideoAsync),
    ];

    private CommandDefinition Export(string id, Func<CancellationToken, Task> run) =>
        new()
        {
            Id = new CommandId(id),
            Module = ModuleKind.Presentations,
            MenuPath = "file/export",
            RibbonPath = "file/export",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active?.Model is Presentation { Slides.Count: > 0 },
            Execute = (_, ct) => run(ct),
        };

    private async Task ExportImagesAsync(CancellationToken ct)
    {
        if (workspace.Active?.Model is not Presentation deck) return;
        var currentIndex = workspace.Active.View is DeckView view ? Math.Clamp(view.CurrentSlideIndex, 0, deck.Slides.Count - 1) : 0;
        var options = await dialogs.ShowAsync<Design.ImageExportResult>(
            new Design.ImageExportView(deck, deck.Slides[currentIndex], publisher.ExportWidth, imageExport),
            new DialogOptions { TitleKey = "deck.export.images.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (options is null) return;

        var chosen = await files.SaveFileAsync(
            texts["deck.export.images.title"], Name(deck) + ".png",
            [new FileFilter("PNG", ["png"])], ct).ConfigureAwait(true);

        if (chosen is null) return;

        var directory = Path.GetDirectoryName(chosen.Path) ?? Directory.GetCurrentDirectory();
        var stem = Path.GetFileNameWithoutExtension(chosen.Path);
        var renderer = publisher.Renderer();

        var visible = options.CurrentOnly ? [deck.Slides[currentIndex]] : deck.Slides.Where(slide => !slide.Hidden).ToList();
        var targets = visible.Select((slide, i) => (Slide: slide, Path: Path.Combine(directory, $"{stem}-{i + 1}.png"))).ToList();
        var existing = targets.Count(target => File.Exists(target.Path));
        if (existing > 0 && !await ConfirmOverwriteAsync(existing, directory, ct).ConfigureAwait(true)) return;

        var written = 0;
        foreach (var (slide, path) in targets)
        {
            var png = renderer.RenderPng(deck, slide, options.Width);
            using var content = new MemoryStream(png);
            await atomic.ReplaceAsync(path, content, ct).ConfigureAwait(true);
            written++;
        }

        status.Report(texts.Format("deck.export.done", written), NotificationSeverity.Success);
    }

    private async Task<bool> ConfirmOverwriteAsync(int existing, string directory, CancellationToken ct)
    {
        var view = new ChoicePromptView(
            texts.Format("deck.export.images.overwrite", existing),
            directory,
            [
                new PromptChoice(texts["deck.export.images.replace"], true),
                new PromptChoice(texts["close.cancel"], false, IsDefault: true, IsCancel: true),
            ],
            texts);

        var chosen = await dialogs.ShowAsync<object>(
            view,
            new DialogOptions { TitleKey = "deck.export.images.title", PreferredWidth = 480, CanCancelWithEscape = true },
            ct).ConfigureAwait(true);
        return chosen is true;
    }

    private async Task ExportHtmlAsync(CancellationToken ct)
    {
        if (workspace.Active?.Model is not Presentation deck) return;

        var chosen = await files.SaveFileAsync(
            texts["deck.export.html.title"], Name(deck) + ".html",
            [new FileFilter("HTML", ["html"])], ct).ConfigureAwait(true);

        if (chosen is null) return;

        var written = await atomic.WriteAsync(chosen, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1 << 16, leaveOpen: true);
            return publisher.WriteHtml(deck, writer);
        }, _ => true, ct).ConfigureAwait(true);

        status.Report(texts.Format("deck.export.done", written), NotificationSeverity.Success);
    }

    private async Task ExportVideoAsync(CancellationToken ct)
    {
        if (workspace.Active?.Model is not Presentation deck) return;

        var encoder = videoEncoders.Current;
        if (!encoder.IsAvailable)
        {
            status.Report(texts["deck.export.video.needsFfmpeg"], NotificationSeverity.Warning);
            return;
        }

        var chosen = await files.SaveFileAsync(
            texts["deck.export.video.title"], Name(deck) + ".mp4",
            [new FileFilter("MP4", ["mp4"])], ct).ConfigureAwait(true);

        if (chosen is null) return;

        var visible = deck.Slides.Where(s => !s.Hidden).ToList();
        if (visible.Count == 0) return;

        var result = await EncodeVideoAsync(encoder, deck, visible, chosen, ct).ConfigureAwait(true);

        if (result.Succeeded)
            status.Report(texts.Format("deck.export.done", visible.Count), NotificationSeverity.Success);
        else
            status.Report(texts.Format("deck.export.video.failed", Reason(result)), NotificationSeverity.Error);
    }

    private async Task<OperationResult<string>> EncodeVideoAsync(
        IVideoEncoder encoder, Presentation deck, IReadOnlyList<Slide> visible, ChosenFile chosen, CancellationToken ct)
    {
        var renderer = new DispatchedSlideFrameRenderer(publisher.Renderer(), deck, publisher.ExportWidth);

        IVideoFrameSource frames;
        try
        {
            frames = await videoFrames.CreateAsync(visible, videoDurations.Of, renderer, ct).ConfigureAwait(true);
        }
        catch (VideoFrameException ex)
        {
            return OperationResult<string>.Fail(Diagnostic.Error(ex.Code, ex.Message).With(ex.Message));
        }

        try
        {
            return await atomic.WriteThroughPathAsync(chosen,
                (path, token) => encoder.EncodeAsync(frames, VideoFps, path, token),
                encoded => encoded.Succeeded, ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OperationResult<string>.Fail(Diagnostic.Error("VIDEO002", ex.Message).With(ex.Message));
        }
    }

    private string Reason(OperationResult<string> result) =>
        result.Diagnostics.FirstOrDefault(d => d.Severity == Severity.Error) is { } error
            ? diagnostics.Describe(error)
            : texts["deck.export.failed"];

    private string Name(Presentation deck) =>
        workspace.Active is { Model: Presentation active } document && ReferenceEquals(active, deck)
            ? System.IO.Path.GetFileNameWithoutExtension(document.Title)
            : publisher.SuggestedName(deck);
}
