using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using System.Text;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxSheetObjectsAuditTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFklEQVR4nGP4z8DAwMDAxMDAwMDAAAANHQEDasKb6QAAAABJRU5ErkJggg==");

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static byte[] Write(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        return stream.ToArray();
    }

    private static Workbook Read(byte[] package)
    {
        var result = new XlsxReader().Read(new MemoryStream(package));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static string Entry(byte[] package, string name)
    {
        using var zip = new ZipArchive(new MemoryStream(package));
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    private static byte[] Rewrite(byte[] package, string name, Func<string, string> change)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(package)))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName).Open();
                if (entry.FullName != name) { from.CopyTo(to); continue; }
                using var reader = new StreamReader(from);
                var bytes = Encoding.UTF8.GetBytes(change(reader.ReadToEnd()));
                to.Write(bytes);
            }
        return output.ToArray();
    }

    private static Workbook WithObject(EmbeddedObject content, string anchor, double width, double height)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = content.Id,
            Content = content,
            Anchor = A(anchor),
            Size = new DocumentSize(Twip.FromCentimeters(width), Twip.FromCentimeters(height)),
        });
        return workbook;
    }

    private static string TwoCell(string drawing, string from, string to)
    {
        var fromStart = drawing.IndexOf("<xdr:from>", StringComparison.Ordinal);
        var toEnd = drawing.IndexOf("</xdr:to>", fromStart, StringComparison.Ordinal) + "</xdr:to>".Length;
        return drawing[..fromStart] + from + to + drawing[toEnd..];
    }

    private static string Marker(string name, int column, long columnOffset, int row, long rowOffset) =>
        $"<xdr:{name}><xdr:col>{column}</xdr:col><xdr:colOff>{columnOffset}</xdr:colOff><xdr:row>{row}</xdr:row><xdr:rowOff>{rowOffset}</xdr:rowOff></xdr:{name}>";

    [Fact]
    public void UnImmagineAncorataADueCelleComeInExcelNonSiPerde()
    {
        var workbook = WithObject(new RasterImageObject { Id = "img", Data = Png, MediaType = "image/png" }, "B3", 2, 1);
        var layout = workbook.Sheets[0].Layout;
        var package = Write(workbook);
        var column = Emu.FromTwips(layout.DefaultColumnWidth).Value;

        package = Rewrite(package, "xl/drawings/drawing1.xml", xml => TwoCell(xml,
            Marker("from", 1, 0, 2, 0),
            Marker("to", 3, column / 2, 4, 0)));

        var back = Read(package).Sheets[0].Objects.Single();
        var image = Assert.IsType<RasterImageObject>(back.Content);

        Assert.Equal(A("B3"), back.Anchor);
        Assert.Equal(2, image.PixelWidth);
        Assert.InRange(back.Size.Width.Value, layout.DefaultColumnWidth.Value * 5 / 2 - 5, layout.DefaultColumnWidth.Value * 5 / 2 + 5);
        Assert.InRange(back.Size.Height.Value, layout.DefaultRowHeight.Value * 2 - 5, layout.DefaultRowHeight.Value * 2 + 5);
    }

    [Fact]
    public void UnaFormaRuotataPrendeLaMisuraDallaTrasformazione()
    {
        var workbook = WithObject(new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, RotationDegrees = 90 }, "C5", 3, 1);
        var sheet = workbook.Sheets[0];
        var package = Write(workbook);

        var centreX = sheet.Layout.ColumnOffset(2).Value + Twip.FromCentimeters(1.5).Value;
        var centreY = sheet.Layout.RowOffset(4).Value + Twip.FromCentimeters(0.5).Value;
        var placement = new SheetPlacement();
        var origin = new CellAddress(0, 0);
        var seed = new PlacedObject { Id = "x", Anchor = origin, Content = new ShapeObject { Id = "x", Shape = ShapeKind.Rectangle } };
        var rotatedTop = placement.Place(sheet.Layout, origin, seed, new PlacementBox(
            new Twip(centreX - Twip.FromCentimeters(0.5).Value), new Twip(centreY - Twip.FromCentimeters(1.5).Value),
            Twip.FromCentimeters(1), Twip.FromCentimeters(3)));
        var rotatedEnd = placement.Place(sheet.Layout, origin, seed, new PlacementBox(
            new Twip(centreX + Twip.FromCentimeters(0.5).Value), new Twip(centreY + Twip.FromCentimeters(1.5).Value),
            Twip.FromCentimeters(1), Twip.FromCentimeters(3)));

        package = Rewrite(package, "xl/drawings/drawing1.xml", xml => TwoCell(xml,
            Marker("from", rotatedTop.Anchor.Column, Emu.FromTwips(rotatedTop.OffsetX).Value, rotatedTop.Anchor.Row, Emu.FromTwips(rotatedTop.OffsetY).Value),
            Marker("to", rotatedEnd.Anchor.Column, Emu.FromTwips(rotatedEnd.OffsetX).Value, rotatedEnd.Anchor.Row, Emu.FromTwips(rotatedEnd.OffsetY).Value)));

        var back = Read(package).Sheets[0].Objects.Single();

        Assert.Equal(90, ((ShapeObject)back.Content).RotationDegrees, 3);
        Assert.Equal(A("C5"), back.Anchor);
        Assert.InRange(back.Size.Width.ToCentimeters(), 2.98, 3.02);
        Assert.InRange(back.Size.Height.ToCentimeters(), 0.98, 1.02);
        Assert.InRange(back.OffsetX.Value, -3, 3);
    }

    [Fact]
    public void UnSvgEsternoTornaVettoriale()
    {
        var workbook = WithObject(new RasterImageObject { Id = "img", Data = Png, MediaType = "image/png" }, "B3", 2, 2);
        var package = Write(workbook);
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><path d=\"M0 0 L10 10\"/></svg>";

        using (var buffer = new MemoryStream())
        {
            buffer.Write(package);
            using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(buffer, true))
            {
                var drawings = document.WorkbookPart!.WorksheetParts.First().DrawingsPart!;
                var part = drawings.AddImagePart(ImagePartType.Svg);
                using (var stream = part.GetStream()) stream.Write(Encoding.UTF8.GetBytes(svg));
                var id = drawings.GetIdOfPart(part);
                var blip = drawings.WorksheetDrawing!.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Single();
                blip.AppendChild(new DocumentFormat.OpenXml.Drawing.BlipExtensionList(new DocumentFormat.OpenXml.Drawing.BlipExtension(
                    new DocumentFormat.OpenXml.Office2019.Drawing.SVG.SVGBlip { Embed = id }) { Uri = "{96DAC541-7B7A-43D3-8B79-37D633B846F1}" }));
                drawings.WorksheetDrawing.Save();
            }
            package = buffer.ToArray();
        }

        var vector = Assert.IsType<VectorGraphicObject>(Read(package).Sheets[0].Objects.Single().Content);
        Assert.Contains("<svg", vector.Asset.SvgContent);
    }

    [Fact]
    public void IControlliDiExcelSiLeggonoConLaLoroAncora()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "c1",
            Anchor = A("H3"),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject { Id = "c1", ControlKind = FormControlKind.CheckBox, Properties = new FormControlProperties { Name = "Spunta", LinkedCell = "J1", Value = new FormControlValue { Checked = CheckState.Checked } } },
        });
        var package = Write(workbook);

        package = Rewrite(package, "xl/worksheets/sheet1.xml", xml =>
            System.Text.RegularExpressions.Regex.Replace(xml, "<(x:)?legacyDrawing [^>]*/>", string.Empty));

        var back = Read(package).Sheets[0];
        var placed = back.Objects.Single();
        var control = Assert.IsType<FormControlObject>(placed.Content);

        Assert.Equal(FormControlKind.CheckBox, control.ControlKind);
        Assert.Equal(A("H3"), placed.Anchor);
        Assert.InRange(placed.Size.Width.Value, Twip.FromCentimeters(4).Value - 2, Twip.FromCentimeters(4).Value + 2);
        Assert.Equal("J1", control.Properties.LinkedCell);
        Assert.Equal(CheckState.Checked, control.Properties.Value.Checked);
    }

    [Fact]
    public void UnControlloConservaScostamentoEUnNomeConLaBarra()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "c|1",
            Anchor = A("H3"),
            OffsetX = Twip.FromCentimeters(0.3),
            OffsetY = Twip.FromCentimeters(0.1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject { Id = "c|1", ControlKind = FormControlKind.CheckBox, Properties = new FormControlProperties { Name = "Sì|No", Label = "100% vero", Value = new FormControlValue { Checked = CheckState.Indeterminate } } },
        });

        var placed = Read(Write(workbook)).Sheets[0].Objects.Single();
        var control = Assert.IsType<FormControlObject>(placed.Content);

        Assert.Equal("Sì|No", placed.Id);
        Assert.Equal("Sì|No", control.Properties.Name);
        Assert.Equal("100% vero", control.Properties.Label);
        Assert.Equal(Twip.FromCentimeters(0.3), placed.OffsetX);
        Assert.Equal(Twip.FromCentimeters(0.1), placed.OffsetY);
        Assert.Equal(CheckState.Indeterminate, control.Properties.Value.Checked);
    }

    [Fact]
    public void UnaRegolaSuPiuAreeSopravviveInXlsx()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            AdditionalRanges = [new CellRange(A("D1"), A("D3"))],
            Formula = "A1>0",
            Style = new CellStyle { BackgroundHex = "#FF0000" },
        });

        var package = Write(workbook);
        Assert.Contains("sqref=\"A1:A3 D1:D3\"", Entry(package, "xl/worksheets/sheet1.xml"));

        var rule = Assert.Single(Read(package).Sheets[0].ConditionalRules);
        Assert.Equal([new CellRange(A("A1"), A("A3")), new CellRange(A("D1"), A("D3"))], rule.Ranges);
    }

    [Fact]
    public void UnaRegolaSuFormulaConTestoESfondoSopravviveInXlsxAccantoAUnaScalaDiColori()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule { Range = new CellRange(A("C14"), A("N14")), Visual = ConditionalVisual.ColorScale });
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("C4"), A("N11")),
            Formula = "=$B4=\"Uscita\"",
            Priority = 1,
            Style = new CellStyle { BackgroundHex = "#FCE4D6", Font = CellFont.Default with { ColorHex = "#14532D" } },
        });

        var rules = Read(Write(workbook)).Sheets[0].ConditionalRules;
        var rule = Assert.Single(rules, candidate => candidate.Visual == ConditionalVisual.Style);
        Assert.Equal("=$B4=\"Uscita\"", rule.Formula);
        Assert.Equal("#FCE4D6", rule.Style.BackgroundHex);
    }

    [Fact]
    public void LeMisurePredefiniteDelFoglioSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Layout.DefaultRowHeight = Twip.FromPoints(18);

        var layout = Read(Write(workbook)).Sheets[0].Layout;

        Assert.Equal(Twip.FromPoints(18), layout.DefaultRowHeight);
        Assert.InRange(layout.DefaultColumnWidth.Value, workbook.Sheets[0].Layout.DefaultColumnWidth.Value - 2, workbook.Sheets[0].Layout.DefaultColumnWidth.Value + 2);
    }

    [Fact]
    public void NomiLocaliUgualiSuDueFogliSopravvivonoInXlsx()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Nord");
        workbook.DefineName(new DefinedName("Tasso", "$A$1", "Sheet1"));
        workbook.DefineName(new DefinedName("Tasso", "$B$2", "nord"));
        workbook.DefineName(new DefinedName("Tasso", "0.5"));

        var back = Read(Write(workbook));

        Assert.Equal(3, back.DefinedNames.Count);
        Assert.Contains("$B$2", back.FindName("Tasso", "Nord")!.Expression);
        Assert.Equal("0.5", back.FindName("Tasso")!.Expression);
    }
}
