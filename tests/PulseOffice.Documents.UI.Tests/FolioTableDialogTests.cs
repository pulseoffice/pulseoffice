using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Sorting;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioTableDialogTests
{
    private sealed class CapturingSortLevels : ISortLevelsDialog
    {
        public SortLevelsRequest? Request { get; private set; }

        public Task<SortLevelsChoice?> AskAsync(SortLevelsRequest request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult<SortLevelsChoice?>(null);
        }
    }

    private sealed class InitialAnswers : PulseOffice.UI.Controls.ITabbedForms
    {
        public Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, IReadOnlyList<PulseOffice.UI.Controls.FormTab> tabs, CancellationToken ct, int initialTab = 0, double width = 620) =>
            Task.FromResult<IReadOnlyDictionary<string, object>?>(tabs.SelectMany(tab => tab.Fields).ToDictionary(field => field.Key, field => field switch
            {
                PulseOffice.UI.Controls.CheckField check => (object)check.Initial,
                PulseOffice.UI.Controls.NumberField number => number.Initial,
                PulseOffice.UI.Controls.ChoiceField choice => choice.Initial,
                PulseOffice.UI.Controls.Choices.IconChoiceField icon => icon.Initial,
                PulseOffice.UI.Controls.Fields.SizeLockField size => size.Initial,
                PulseOffice.UI.Controls.TextField text => text.Initial,
                _ => string.Empty,
            }));
    }

    [Fact]
    public async Task LeProprietaDellOggettoConfermateSenzaModificheNonPerdonoSopraESotto()
    {
        var item = new PulseOffice.Documents.Objects.AnchoredObject("o1", new PulseOffice.Documents.Objects.ParagraphAnchor("p"),
            new PulseOffice.Documents.Objects.TextBoxContent(new BlockContainer()), new PulseOffice.Foundation.Units.DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)))
        {
            Wrap = PulseOffice.Documents.Objects.WrapMode.TopAndBottom,
        };
        var dialogs = new FolioObjectDialogs(null!, new InitialAnswers(), new TextCatalog(UiLanguage.Italian), null!, null!);

        var choice = await dialogs.PropertiesAsync(item, PulseOffice.Documents.UI.Editing.ObjectAnchorKind.Paragraph, ObjectDialogPage.Position, CancellationToken.None);

        Assert.NotNull(choice);
        Assert.Equal(PulseOffice.Documents.Objects.WrapMode.TopAndBottom, choice.Wrap);
    }

    [Fact]
    public async Task LeColonneOltreLaZetaHannoLettereDistinte()
    {
        const int columns = 30;
        var cells = Enumerable.Range(0, columns).Select(_ => new TableCell(new BlockContainer([new ParagraphBlock("p", string.Empty)]))).ToList();
        var table = new TableBlock("t", [new TableRow(cells)], [.. Enumerable.Repeat(Twip.FromCentimeters(1), columns)]);
        var sort = new CapturingSortLevels();
        var dialogs = new FolioTableDialogs(null!, null!, new TextCatalog(UiLanguage.Italian), null!, null!, null!, null!, null!, sort);

        await dialogs.SortAsync(table, new TableCellRange(0, 0, 0, columns - 1), CancellationToken.None);

        Assert.NotNull(sort.Request);
        Assert.Equal(columns, sort.Request.Letters.Distinct().Count());
        Assert.Equal(["Z", "AA", "AB"], sort.Request.Letters.Skip(25).Take(3));
    }
}
