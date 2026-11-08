using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Canvas;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.UI.Controls.Rulers;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioViewModeTests
{
    private readonly PageDesk _desk = new();

    private static IReadOnlyList<(double, double)> Pages(int count) => [.. Enumerable.Repeat((100.0, 140.0), count)];

    [Fact]
    public void LaVistaLibroMetteLaPrimaPaginaADestraEPoiACoppie()
    {
        var desk = _desk.Arrange(Pages(4), 1, 600, DeskLayout.Book);

        Assert.True(desk.Pages[0].X > 300);
        Assert.Equal(desk.Pages[1].Y, desk.Pages[2].Y);
        Assert.True(desk.Pages[1].X < 300 && desk.Pages[2].X > 300);
        Assert.True(desk.Pages[3].Y > desk.Pages[2].Y);
    }

    [Fact]
    public void PiuPaginePerRigaRiempionoLaLarghezza()
    {
        var desk = _desk.Arrange(Pages(5), 1, 600, DeskLayout.Multiple);

        Assert.Equal(desk.Pages[0].Y, desk.Pages[3].Y);
        Assert.True(desk.Pages[4].Y > desk.Pages[0].Y);
        Assert.Equal(4, desk.Pages.Count(slot => slot.Y == desk.Pages[0].Y));
    }

    [Fact]
    public Task LaVistaWebAdattaLaPaginaAllaFinestra() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([.. Enumerable.Range(0, 120).Select(index => new ParagraphBlock(document.NextId("p"), "Riga " + index))]));
        var layout = scene.Services.GetRequiredService<IFolioViewLayout>();
        var pages = scene.Editor.Layout.Pages.Count;

        layout.Mode = FolioViewMode.Web;
        Dispatcher.UIThread.RunJobs();
        scene.Window.Width = 700;
        Dispatcher.UIThread.RunJobs();

        Assert.True(pages > 1);
        Assert.Single(scene.Editor.Layout.Pages);
        Assert.InRange(scene.Editor.Layout.Pages[0].Width, 300, 700);

        layout.Mode = FolioViewMode.Print;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(pages, scene.Editor.Layout.Pages.Count);
        return Task.CompletedTask;
    });

    [Fact]
    public Task LaVistaWebNascondeIntestazioniEMarginiMaStampaInPagine() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(document =>
        {
            var page = document.Styles.Page.Find(PulseOffice.Documents.Styles.DefaultStyleNames.Standard)!;
            page.Header.Enabled = true;
            page.Header.Default.Replace([new ParagraphBlock(document.NextId("p"), "Intestazione")]);
            page.Footer.Enabled = true;
            page.Footer.Default.Replace([new ParagraphBlock(document.NextId("p"), "Piede")]);
            document.Body.Replace([.. Enumerable.Range(0, 120).Select(index => new ParagraphBlock(document.NextId("p"), "Riga " + index))]);
        });
        var layout = scene.Services.GetRequiredService<IFolioViewLayout>();
        var printed = scene.Editor.Layout;
        Assert.NotNull(printed.Pages[0].Header);

        layout.Mode = FolioViewMode.Web;
        Dispatcher.UIThread.RunJobs();
        scene.Window.Width = 700;
        Dispatcher.UIThread.RunJobs();

        var web = Assert.Single(scene.Editor.Layout.Pages);
        Assert.Null(web.Header);
        Assert.Null(web.Footer);
        Assert.True(web.Content.X < 20);

        var open = new PulseOffice.Shell.OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Documents, "Folio1", scene.View, scene.History) { Model = scene.Document };
        var operations = scene.Services.GetServices<PulseOffice.Shell.IDocumentOperations>().Single(candidate => candidate.Module == PulseOffice.Foundation.Modules.ModuleKind.Documents);
        var printable = operations.BuildPrintable(open)!;

        Assert.Equal(printed.Pages.Count, printable.PageCount);
        Assert.Single(scene.Editor.Layout.Pages);
        Assert.Null(scene.Editor.Layout.Pages[0].Header);
        return Task.CompletedTask;
    });

    [Fact]
    public Task IlRighelloMostraRientriEMarginiESiTrascina() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("testo");
        var rulers = scene.View.Rulers;
        rulers.Update();
        var model = rulers.Horizontal.Model;
        var left = model.Markers.Single(marker => marker.Kind == RulerMarkerKind.Left);
        Assert.Equal(model.ActiveStart, left.Position, 3);
        Assert.Contains(model.Markers, marker => marker.Kind == RulerMarkerKind.MarginStart);

        var from = rulers.Horizontal.TranslatePoint(new Point(model.ToPixels(left.Position), RulerStrip.Thickness - 3), scene.Window)!.Value;
        var to = from + new Point(model.UnitPoints * model.Scale, 0);
        scene.Window.MouseDown(from, MouseButton.Left);
        scene.Window.MouseMove(to);
        scene.Window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(model.UnitPoints, scene.Editor.CurrentParagraph().MarginLeft ?? 0, 1);

        rulers.Update();
        var click = rulers.Horizontal.TranslatePoint(new Point(rulers.Horizontal.Model.ToPixels(model.ActiveStart + model.UnitPoints * 3), 10), scene.Window)!.Value;
        scene.Window.MouseDown(click, MouseButton.Left);
        scene.Window.MouseUp(click, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(scene.Editor.CurrentParagraph().TabStops ?? []);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ISegniDiFormattazioneSiAccendono() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var marks = scene.Services.GetRequiredService<IFolioViewMarks>();

        marks.FormattingMarks = true;

        Assert.True(marks.Current.FormattingMarks);
        Assert.False(PageMarks.None.FormattingMarks);
        Assert.True(PageMarks.None.Images);
        return Task.CompletedTask;
    });

    [Fact]
    public Task IlParagrafoNascostoSparisceEdESiMostraACommando() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(document =>
        {
            var runs = new[]
            {
                new PulseOffice.Text.Rich.TextRun("segreto"),
                new PulseOffice.Text.Rich.TextRun("\u200B") { Field = PulseOffice.Text.Rich.TextFieldKind.HiddenParagraph, FieldArgument = "1 == 1" },
            };
            document.Body.Replace([
                new ParagraphBlock(document.NextId("p"), "visibile"),
                new ParagraphBlock(document.NextId("p"), new PulseOffice.Text.Rich.TextParagraph("segreto\u200B") { Runs = runs }),
            ]);
        });
        scene.Services.GetRequiredService<PulseOffice.Documents.UI.Editing.IFolioFieldRefresh>().UpdateAll(scene.Editor);
        var hidden = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Last().Id;

        Assert.DoesNotContain(scene.Editor.Layout.Pages[0].Lines, line => line.ParagraphId == hidden);

        scene.Services.GetRequiredService<PulseOffice.Documents.Layout.Flow.IHiddenParagraphs>().Show = true;
        scene.Editor.Reflow();

        Assert.Contains(scene.Editor.Layout.Pages[0].Lines, line => line.ParagraphId == hidden);
        scene.Services.GetRequiredService<PulseOffice.Documents.Layout.Flow.IHiddenParagraphs>().Show = false;
        return Task.CompletedTask;
    });

    [Fact]
    public void NascondereLoSpazioTagliaIMarginiFraLePagine()
    {
        var full = _desk.Arrange(Pages(2), 1, 600);
        var cropped = _desk.Arrange(Pages(2), 1, 600, DeskLayout.Single, [(20, 30), (20, 30)]);

        Assert.True(cropped.Height < full.Height - 80);
        Assert.Equal(20, cropped.Pages[0].CropTop);
        Assert.True(cropped.Pages[1].VisibleTop < full.Pages[1].Y);
        Assert.True(cropped.Pages[0].Contains(cropped.Pages[0].X + 1, cropped.Pages[0].VisibleTop + 1));
        Assert.False(cropped.Pages[0].Contains(cropped.Pages[0].X + 1, cropped.Pages[0].Y + 1));
    }

    [Fact]
    public Task LEvidenziazioneColoraStiliEFormattazioneDiretta() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([
            new ParagraphBlock(document.NextId("p"), new PulseOffice.Text.Rich.TextParagraph("ab") { StyleName = "Heading 1", Runs = [new PulseOffice.Text.Rich.TextRun("a") { Bold = true }, new PulseOffice.Text.Rich.TextRun("b")] }),
        ]));
        var spotlight = scene.Services.GetRequiredService<IFolioSpotlight>();
        var id = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single().Id;

        spotlight.ParagraphStyles = true;
        spotlight.DirectFormatting = true;
        var marks = spotlight.Apply(PageMarks.None, scene.Document);

        Assert.NotNull(marks.ParagraphSpotlight!(id));
        Assert.NotNull(marks.RunSpotlight!(id, 0));
        Assert.Null(marks.RunSpotlight!(id, 1));
        return Task.CompletedTask;
    });
}
