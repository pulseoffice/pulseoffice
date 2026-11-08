using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;

namespace PulseOffice.Sheets.UI;

public interface ISheetImageInsertion
{
    (int Width, int Height)? PixelSize(byte[] bytes);

    string MediaTypeOf(string fileName);

    bool InsertObject(EmbeddedObject content, DocumentSize size, string historyLabel);

    PlacedObject? Insert(PlacedObject placed, string historyLabel);

    bool InsertRaster(byte[] bytes, string name, string fileName);
}

public sealed class SheetImageInsertion(
    IActiveSheetContext context,
    PulseOffice.UI.Controls.Objects.IRasterImageFiles images,
    ITextCatalog texts,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    PulseOffice.UI.Controls.Services.IStatusBarService status,
    IObjectOrdering ordering,
    PulseOffice.Drawing.Assets.IImageExtents extents) : ISheetImageInsertion
{
    private const int LargestExtent = PulseOffice.Exchange.ExchangeContentGuard.MaximumExtent;

    public (int Width, int Height)? PixelSize(byte[] bytes) => images.PixelSize(bytes);

    public string MediaTypeOf(string fileName) => images.MediaTypeOf(fileName);

    public bool InsertObject(EmbeddedObject content, DocumentSize size, string historyLabel)
    {
        ArgumentNullException.ThrowIfNull(content);

        return Insert(new PlacedObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Content = content,
            Anchor = context.Selection.Start,
            Size = size,
        }, historyLabel) is not null;
    }

    public PlacedObject? Insert(PlacedObject placed, string historyLabel)
    {
        ArgumentNullException.ThrowIfNull(placed);

        if (context.Workbook is not { } workbook) return null;

        var sheet = workbook.Sheets[context.SheetIndex];
        if (Refused(sheet)) return null;

        placed = placed with { ZOrder = ordering.NextOrder(sheet) };
        sheet.Objects.Add(placed);

        context.History?.Push(new DelegateAction(
            historyLabel,
            redo: () => sheet.Objects.Add(placed),
            undo: () => sheet.Objects.Remove(placed)));

        context.SelectObject(placed);
        context.RequestRefresh();

        return placed;
    }

    private bool Refused(Worksheet sheet)
    {
        if (workspace.Active?.IsReadOnly == true)
        {
            status.Report(texts["status.readOnly"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error,
                PulseOffice.UI.Controls.Notifications.NotificationRemedies.EditMode);
            return true;
        }

        if (!sheet.Protection.IsEnabled) return false;
        status.Report(texts["status.protection.objects"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }

    public bool InsertRaster(byte[] bytes, string name, string fileName)
    {
        if (PixelSize(bytes) is not { } size) return false;

        return InsertObject(
            new RasterImageObject
            {
                Id = name,
                Data = bytes,
                MediaType = MediaTypeOf(fileName),
                PixelWidth = size.Width,
                PixelHeight = size.Height,
            },
            extents.Natural(size.Width, size.Height, new PulseOffice.Drawing.Assets.ImageBounds(new DocumentSize(new Twip(LargestExtent), new Twip(LargestExtent)), default)),
            texts["history.image.insert"]);
    }
}
