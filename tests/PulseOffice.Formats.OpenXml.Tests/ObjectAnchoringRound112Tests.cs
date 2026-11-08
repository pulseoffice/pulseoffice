using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml.Tests;

public class ObjectAnchoringRound112Tests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    private Workbook Objects()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), Sheets.Values.CellValue.Number(1));
        var size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2));
        Add(sheet, "both", new CellAddress(1, 1), ObjectAnchoring.MoveAndSize, new ShapeObject { Id = "both", Shape = ShapeKind.Rectangle }, size);
        Add(sheet, "move", new CellAddress(5, 2), ObjectAnchoring.Move, new ShapeObject { Id = "move", Shape = ShapeKind.Ellipse }, size);
        Add(sheet, "fixed", new CellAddress(9, 3), ObjectAnchoring.Fixed,
            new RasterImageObject { Id = "fixed", Data = Png, MediaType = "image/png", NaturalSize = size }, size);
        return workbook;
    }

    private void Add(Worksheet sheet, string id, CellAddress anchor, ObjectAnchoring anchoring, PulseOffice.Document.Embedding.EmbeddedObject content, DocumentSize size) =>
        sheet.Objects.Add(new PlacedObject
        {
            Id = id,
            Content = content,
            Anchor = anchor,
            OffsetX = Twip.FromMillimeters(2),
            OffsetY = Twip.FromMillimeters(1),
            Size = size,
            Anchoring = anchoring,
        });

    private void AssertModes(Workbook read)
    {
        var objects = read.Sheets[0].Objects;
        Assert.Equal(3, objects.Count);
        Assert.Equal(ObjectAnchoring.MoveAndSize, objects.Single(placed => placed.Content is ShapeObject { Shape: ShapeKind.Rectangle }).Anchoring);
        Assert.Equal(ObjectAnchoring.Move, objects.Single(placed => placed.Content is ShapeObject { Shape: ShapeKind.Ellipse }).Anchoring);
        var image = objects.Single(placed => placed.Content is RasterImageObject);
        Assert.Equal(ObjectAnchoring.Fixed, image.Anchoring);
        Assert.Equal(new CellAddress(9, 3), image.Anchor);
        Assert.InRange(Math.Abs(image.OffsetX.Value - Twip.FromMillimeters(2).Value), 0, 30);
        Assert.InRange(Math.Abs(image.Size.Width.Value - Twip.FromCentimeters(3).Value), 0, 30);
    }

    [Fact]
    public void Xlsx_writes_edit_as_by_mode_and_reads_it_back()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Objects(), stream);

        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var drawing = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!;
            var modes = drawing.Elements<Xdr.TwoCellAnchor>().Select(anchor => anchor.EditAs?.Value).ToList();
            Assert.Contains(Xdr.EditAsValues.TwoCell, modes);
            Assert.Contains(Xdr.EditAsValues.OneCell, modes);
            Assert.Contains(Xdr.EditAsValues.Absolute, modes);
        }

        stream.Position = 0;
        AssertModes(new XlsxReader().Read(stream).Value!);
    }

    [Fact]
    public void Ods_uses_end_cell_for_resizing_and_table_shapes_for_page_anchoring()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Objects(), stream);

        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var entry = zip.GetEntry("content.xml")!.Open();
            var content = XDocument.Load(entry);
            var named = content.Descendants().Where(element => element.Attribute(_draw + "name") is not null).ToList();
            var both = named.Single(element => (string?)element.Attribute(_draw + "name") == "both");
            var move = named.Single(element => (string?)element.Attribute(_draw + "name") == "move");
            var fixedFrame = named.Single(element => (string?)element.Attribute(_draw + "name") == "fixed");
            Assert.NotNull(both.Attribute(_table + "end-cell-address"));
            Assert.Null(move.Attribute(_table + "end-cell-address"));
            Assert.Equal(_table + "shapes", fixedFrame.Parent!.Name);
            Assert.Null(fixedFrame.Attribute(_table + "end-cell-address"));
        }

        stream.Position = 0;
        AssertModes(new OdsReader().Read(stream).Value!);
    }

    [Fact]
    public void Modes_cross_from_xlsx_to_ods()
    {
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(Objects(), xlsx);
        xlsx.Position = 0;
        var fromXlsx = new XlsxReader().Read(xlsx).Value!;

        using var ods = new MemoryStream();
        new OdsWriter().Write(fromXlsx, ods);
        ods.Position = 0;

        AssertModes(new OdsReader().Read(ods).Value!);
    }

    [Fact]
    public void Two_cell_anchors_without_edit_as_read_as_move_and_size()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Objects(), stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var drawing = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!;
            foreach (var anchor in drawing.Elements<Xdr.TwoCellAnchor>()) anchor.EditAs = null;
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;

        Assert.All(read.Sheets[0].Objects, placed => Assert.Equal(ObjectAnchoring.MoveAndSize, placed.Anchoring));
    }
}
