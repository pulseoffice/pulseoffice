using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Exchange;

namespace PulseOffice.UI.Embedding;

public interface ISheetObjectFactory
{
    PackagedObject Create(string id, CellRangeContent grid, ExchangeRepresentation workbook);
}

public sealed class SheetObjectFactory(ICellGridConversions grids, IContentPainter painter) : ISheetObjectFactory
{
    private const double PixelsPerPoint = 2;
    private const double MaximumPixels = 4096;

    public PackagedObject Create(string id, CellRangeContent grid, ExchangeRepresentation workbook)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(workbook);
        var table = grids.Table(grid, id);
        return new PackagedObject
        {
            Id = id,
            Name = grid.SheetName,
            FileName = grid.SheetName + ".ods",
            MediaType = workbook.MediaType ?? ExchangeMediaTypes.Spreadsheet,
            Data = workbook.Data,
            NaturalSize = table.NaturalSize,
            Replacement = Picture(table),
        };
    }

    private byte[]? Picture(DataTableObject table)
    {
        var width = table.NaturalSize.Width.ToPoints() * PixelsPerPoint;
        var height = table.NaturalSize.Height.ToPoints() * PixelsPerPoint;
        if (width < 1 || height < 1 || !painter.CanPaint(table)) return null;
        var scale = Math.Min(1, MaximumPixels / Math.Max(width, height));
        var pixels = new PixelSize((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));

        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Avalonia.Media.Brushes.White, new Rect(0, 0, pixels.Width, pixels.Height));
            painter.Paint(context, table, new Rect(0, 0, pixels.Width, pixels.Height), PixelsPerPoint * scale);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }
}
