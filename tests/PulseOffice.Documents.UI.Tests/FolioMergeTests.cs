using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Formats.Pdf;
using PulseOffice.Formats.Pdf.Fonts;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioMergeTests
{
    private static readonly MergeData Data = new(["Nome", "Città"], [["Anna", "Roma"], ["Bruno", "Torino"], ["Carla/Bis", "Bari"]]);

    private static void Letter(TextDocument document)
    {
        var runs = new[]
        {
            new TextRun("Cara "),
            new TextRun("«Nome»") { Field = TextFieldKind.Database, FieldArgument = "Nome" },
            new TextRun(" di "),
            new TextRun("«Città»") { Field = TextFieldKind.Database, FieldArgument = "Città" },
        };
        document.Body.Replace([new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs })]);
    }

    private static FolioMergeOutput Output(FolioScene scene) => new(
        scene.Services.GetRequiredService<IMailMergeEngine>(),
        scene.Services.GetRequiredService<PulseOffice.Formats.ITextDocumentFormatRegistry>(),
        scene.Services.GetRequiredService<IFolioPrintables>(),
        scene.Services.GetRequiredService<PulseOffice.Documents.Layout.Flow.IPrintLayouts>(),
        new PdfExporter(new StandardPdfFonts(new PdfFontMetrics())),
        new MergeFileNames(),
        new PulseOffice.Platform.AtomicFileWriter());

    [Fact]
    public void File_names_from_a_column_are_portable_and_bounded()
    {
        var data = new MergeData(["Name"], [[new string('x', 400)], ["a?b*c<d>e|f\"g"], ["CON. "]]);
        var names = new MergeFileNames();

        var longName = names.For(data, 0, "Name", "letter", "odt");
        var symbols = names.For(data, 1, "Name", "letter", "odt");
        var trailing = names.For(data, 2, "Name", "letter", "odt");

        Assert.True(longName.Length <= 120);
        Assert.EndsWith(".odt", longName);
        Assert.Equal("letter-a_b_c_d_e_f_g.odt", symbols);
        Assert.Equal("letter-CON.odt", trailing);
    }

    [Fact]
    public Task IFileSeparatiPrendonoIlNomeDallaColonna() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(Letter);
        var folder = Directory.CreateTempSubdirectory("pulse-merge-").FullName;

        var result = Output(scene).Files(scene.Document, Data, [0, 1, 2], MergeTarget.OdtFiles, folder, "lettera", "Nome");

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value);
        Assert.Equal(["lettera-Anna.odt", "lettera-Bruno.odt", "lettera-Carla_Bis.odt"], Directory.GetFiles(folder).Select(Path.GetFileName).Order());
        Directory.Delete(folder, recursive: true);
        return Task.CompletedTask;
    });

    [Fact]
    public Task IlPdfUnicoHaUnaPaginaPerRecord() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(Letter);
        using var stream = new MemoryStream();

        var result = Output(scene).Pdf(scene.Document, Data, [0, 1, 2], stream, "Lettere");

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value);
        return Task.CompletedTask;
    });
}
