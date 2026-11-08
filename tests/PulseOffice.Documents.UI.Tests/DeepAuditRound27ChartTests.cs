using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Charts;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class DeepAuditRound27ChartTests
{
    private sealed class LineEditor : IChartEditor
    {
        public ChartEditSource? Source { get; private set; }

        public Task<ChartDefinition?> EditAsync(ChartDefinition chart, ChartEditSource source, CancellationToken ct = default)
        {
            Source = source;
            return Task.FromResult<ChartDefinition?>(chart with { Type = ChartType.Line, ChartTitle = "Vendite" });
        }
    }

    private sealed class Samples : IChartSampleData
    {
        public ChartData For(ChartFamily family) => new(["A", "B"], [new ChartSeries("S", [1, 2])]);
    }

    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.Chart, DocumentState.DocumentOpen);

    [Fact]
    public Task UnGraficoDiFolioSiModificaConLEditorCondivisoESiAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var objects = scene.Services.GetRequiredService<IFolioObjectEditing>();
        var chart = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty };
        Assert.True(objects.Insert(scene.Editor, new ObjectInsertion(chart, new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(5)), ObjectAnchorKind.Paragraph, WrapMode.None)));
        Dispatcher.UIThread.RunJobs();
        var id = scene.Editor.SelectedObjectId!;
        var editor = new LineEditor();
        var command = new FolioChartCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), objects, editor,
            new InlineChartData(new Samples(), new ChartFamilyCatalog())).GetCommands().Single(item => item.Id.Value == "folio.chart.edit");

        Assert.True(command.IsEnabled(Context));
        command.Execute(Context, CancellationToken.None).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        var edited = (ChartDefinition)((EmbeddedContent)scene.Document.Objects.Find(id)!.Content).Value;
        Assert.Equal(ChartType.Line, edited.Type);
        Assert.Equal("Vendite", edited.ChartTitle);
        Assert.NotNull(edited.InlineData);
        Assert.False(editor.Source!.DataLayoutEditable);

        scene.History.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ChartType.Column, ((ChartDefinition)((EmbeddedContent)scene.Document.Objects.Find(id)!.Content).Value).Type);
    }, CancellationToken.None);
}
