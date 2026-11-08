using Microsoft.Extensions.Logging;
using PulseOffice.UI.Controls.Notifications;
using System.Globalization;
using PulseOffice.Formats.Pdf;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellPrintCommands(
    IDocumentWorkspace workspace,
    IDocumentOperationsCatalog operations,
    IPrintPreferences preferences,
    IPdfExporter exporter,
    IPrintServiceProvider printing,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Platform.ITemporaryFiles temporary,
    IPrintImposition imposition,
    IPageRangeParser ranges,
    PulseOffice.UI.Controls.Printing.IPdfExportPrompt pdfPrompt,
    PulseOffice.UI.Controls.Printing.IPageCanvasFactory canvases,
    IFileDialogService files,
    IAtomicFileWriter atomic,
    IDiagnosticTexts diagnostics,
    ILogger<ShellPrintCommands> logger)
{

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.file.print"),
            MenuPath = "file",
            DefaultGesture = "Ctrl+P",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => operations.For(workspace.Active) is not null,
            Execute = (_, ct) => PrintAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.printPreview"),
            MenuPath = "file",
            DefaultGesture = "Ctrl+Shift+O",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => operations.For(workspace.Active) is not null,
            Execute = (_, ct) => PreviewAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.exportPdf"),
            MenuPath = "file/export",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => operations.For(workspace.Active) is not null,
            Execute = (_, ct) => ExportPdfAsync(direct: false, ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.exportPdfDirect"),
            MenuPath = "file/export",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => operations.For(workspace.Active) is not null,
            Execute = (_, ct) => ExportPdfAsync(direct: true, ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("shell.file.printerSetup"),
            MenuPath = "file",
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => PrinterSetupAsync(ct),
        },
    ];

    private (IDocumentOperations Operations, OpenDocument Document)? Active() =>
        workspace.Active is { } document && operations.For(document) is { } found ? (found, Committed(document)) : null;

    private static OpenDocument Committed(OpenDocument document)
    {
        document.CommitPendingEdits();
        return document;
    }

    private async Task PreviewAsync(CancellationToken ct)
    {
        if (Active() is not { } active) return;

        if (active.Operations.BuildPrintable(active.Document) is not { } printable)
        {
            status.Report(texts["status.print.empty"], NotificationSeverity.Warning);
            return;
        }

        var print = false;
        await dialogs.ShowAsync(
            new PulseOffice.UI.Controls.Printing.PrintPreviewView(printable, texts, canvases, print: () => print = true),
            new DialogOptions
            {
                TitleKey = "command.shell.file.printPreview.title",
                PreferredWidth = 1240,
                PreferredHeight = 900,
            },
            ct).ConfigureAwait(true);

        if (print) await PrintAsync(ct).ConfigureAwait(true);
    }

    private async Task ExportPdfAsync(bool direct, CancellationToken ct)
    {
        if (Active() is not { } active) return;
        var name = active.Operations.SuggestedName(active.Document);
        var properties = active.Operations.Properties(active.Document);
        var comments = active.Operations.SupportsPrintComments(active.Document);
        var profile = active.Operations.PdfExport(active.Document);
        if (active.Operations.BuildExportable(active.Document, false) is not { } printable)
        {
            status.Report(texts["status.print.empty"], NotificationSeverity.Warning);
            return;
        }

        var options = new PdfExportOptions(properties?.Title ?? name, properties?.Author ?? string.Empty, properties?.Subject ?? string.Empty)
        {
            Keywords = string.Join(", ", properties?.Keywords ?? []),
            Language = properties?.Language,
            Bookmarks = profile.HasHeadings,
        };
        if (!direct)
        {
            var exportable = printable;
            var choice = await pdfPrompt.AskAsync(new PulseOffice.UI.Controls.Printing.PdfExportDefaults(options.Title, options.Author, options.Subject, options.Keywords, printable.PageCount)
            {
                HasComments = comments,
                HasHeadings = profile.HasHeadings,
                Language = options.Language,
                Preview = () => exportable,
                LayoutLabel = profile.LayoutLabel,
                Layouts = [.. profile.Layouts.Select(layout => new PulseOffice.UI.Controls.Printing.PdfPreviewLayout(layout.Label, layout.Build))],
            }, ct).ConfigureAwait(true);
            if (choice is null) return;
            options = choice.Options;
            if (profile.Layouts.Count > 0 && choice.Layout is int index)
                printable = profile.Layouts[Math.Clamp(index, 0, profile.Layouts.Count - 1)].Build();
        }

        var chosen = await files.SaveFileAsync(texts["dialog.exportPdf.title"], name + ".pdf", [new FileFilter("PDF", ["pdf"])], ct).ConfigureAwait(true);
        if (chosen is null) return;

        Report(await atomic.WriteAsync(chosen, stream => exporter.Write(printable, stream, options), written => written.Succeeded, ct).ConfigureAwait(true));
    }

    private void Report(PulseOffice.Foundation.Diagnostics.OperationResult<int> result)
    {
        if (!result.Succeeded)
        {
            foreach (var diagnostic in result.Diagnostics)
                logger.LogWarning("PDF export failed: {Code} {Message}", diagnostic.Code, diagnostic.Message);
            status.Report(result.Diagnostics.FirstOrDefault() is { } reason
                ? texts.Format("status.export.failedBecause", diagnostics.Describe(reason))
                : texts["status.export.failed"], NotificationSeverity.Error);
            return;
        }
        var warning = result.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Severity == PulseOffice.Foundation.Diagnostics.Severity.Warning);
        status.Report(warning is null
            ? texts.Format("status.export.done", result.Value)
            : texts.Format("status.export.doneWithWarning", result.Value, texts.Format(warning.MessageKey, [.. warning.Arguments])),
            warning is null ? NotificationSeverity.Success : NotificationSeverity.Warning);
    }

    private async Task PrintAsync(CancellationToken ct)
    {
        if (Active() is not { } active) return;

        var name = active.Operations.SuggestedName(active.Document);

        if (active.Operations.BuildPrintable(active.Document) is null)
        {
            status.Report(texts["status.print.empty"], NotificationSeverity.Warning);
            return;
        }

        var service = printing.Current;
        var capabilities = service.Capabilities;
        var targets = capabilities.ChoosePrinter ? await TargetsAsync(service, ct).ConfigureAwait(true) : [];
        var source = new Dictionary<bool, IPrintableDocument>();
        IPrintableDocument Build(bool comments)
        {
            if (!source.TryGetValue(comments, out var built))
                source[comments] = built = active.Operations.BuildPrintable(active.Document, comments) ?? active.Operations.BuildPrintable(active.Document)!;
            return built;
        }

        var choice = await dialogs.ShowAsync<PrintChoice>(
            new PrintPromptView(new PrintPromptRequest(targets, capabilities, Build, imposition, ranges)
            {
                InitialCopies = preferences.Copies,
                InitialDuplex = preferences.Duplex,
                SupportsComments = active.Operations.SupportsPrintComments(active.Document),
                InitialComments = active.Operations.PrintCommentsByDefault,
            }, texts, canvases),
            new DialogOptions { TitleKey = "dialog.print.title", PreferredWidth = 860 }, ct)
            .ConfigureAwait(true);

        if (choice is null) return;

        var toFile = choice.ToFile;
        var imposed = imposition.Impose(Build(choice.Options.Comments), choice.Options, nativeCopies: !toFile && capabilities.NativeCopies);

        if (toFile)
        {
            var chosen = await files.SaveFileAsync(texts["print.toPdf"], name + ".pdf", [new FileFilter("PDF", ["pdf"])], ct).ConfigureAwait(true);
            if (chosen is null) return;
            Report(await atomic.WriteAsync(chosen, output => exporter.Write(imposed, output, new PdfExportOptions(Title: name) { Bookmarks = false, Links = false, Notes = false }), written => written.Succeeded, ct).ConfigureAwait(true));
            return;
        }

        var file = Path.Combine(temporary.NewFolder("print-"), "document.pdf");

        await using (var stream = File.Create(file))
        {
            if (!exporter.Write(imposed, stream, new PdfExportOptions(Title: name) { Bookmarks = false, Links = false, Notes = false }).Succeeded)
            {
                status.Report(texts["status.export.failed"], NotificationSeverity.Error);
                return;
            }
        }

        var printed = await service.PrintAsync(new PrintRequest
        {
            FilePath = file,
            PrinterId = choice.PrinterId,
            Copies = capabilities.NativeCopies ? choice.Options.Copies : 1,
            Collate = choice.Options.Collate,
            Sides = choice.Options.Duplex switch
            {
                DuplexMode.LongEdge => PrintSides.LongEdge,
                DuplexMode.ShortEdge => PrintSides.ShortEdge,
                _ => PrintSides.OneSided,
            },
            JobName = name,
        }, ct).ConfigureAwait(true);

        status.Report(printed.Succeeded
            ? texts.Format("status.print.sent", choice.Options.Copies)
            : texts["status.print.failed"],
            printed.Succeeded ? NotificationSeverity.Success : NotificationSeverity.Error);
    }

    private async Task<IReadOnlyList<PrintTarget>> TargetsAsync(IPrintService service, CancellationToken ct)
    {
        var printers = await service.PrintersAsync(ct).ConfigureAwait(true);
        var preferred = printers.Any(printer => printer.Id == preferences.PrinterId) ? preferences.PrinterId : null;

        return [.. printers.Select(printer => new PrintTarget(
            printer.Id,
            printer.DisplayName,
            preferred is null ? printer.IsDefault : printer.Id == preferred))];
    }

    private async Task PrinterSetupAsync(CancellationToken ct)
    {
        var service = printing.Current;
        var available = await service.PrintersAsync(ct).ConfigureAwait(true);

        if (available.Count == 0)
        {
            status.Report(texts["printer.none"], NotificationSeverity.Warning);
            return;
        }

        var current = Math.Max(0, available.ToList().FindIndex(printer =>
            preferences.PrinterId is { } id ? printer.Id == id : printer.IsDefault));

        var fields = new List<FormField>
        {
            new ChoiceField("printer", texts["printer.name"], [.. available.Select(printer => printer.DisplayName)], current),
            new NumberField("copies", texts["printer.copies"], preferences.Copies, 1, 99),
        };

        if (service.Capabilities.Duplex)
            fields.Add(new CheckField("duplex", texts["printer.duplex"], preferences.Duplex));

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(texts["printer.explanation"], fields, texts),
            new DialogOptions { TitleKey = "command.shell.file.printerSetup.title" }, ct).ConfigureAwait(true);

        if (values is null) return;

        var chosen = available[(int)Convert.ToInt64(values["printer"], CultureInfo.InvariantCulture)];

        preferences.PrinterId = chosen.Id;
        preferences.Copies = (int)Math.Round(Convert.ToDouble(values["copies"], CultureInfo.InvariantCulture));
        preferences.Duplex = values.TryGetValue("duplex", out var duplex) && duplex is true;

        status.Report(texts.Format("printer.chosen", chosen.DisplayName));
    }
}
