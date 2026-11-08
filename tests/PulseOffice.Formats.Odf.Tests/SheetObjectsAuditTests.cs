using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using Xunit;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf.Tests;

public class SheetObjectsAuditTests
{
    private const string Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><path d='M0 0 L10 0 L5 10 Z' fill='#ff0000'/></svg>";

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static byte[] Write(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        return stream.ToArray();
    }

    private static Workbook Read(byte[] package)
    {
        var result = new OdsReader().Read(new MemoryStream(package));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static XDocument Content(byte[] package)
    {
        using var zip = new ZipArchive(new MemoryStream(package));
        using var stream = zip.GetEntry("content.xml")!.Open();
        return XDocument.Load(stream);
    }

    private static byte[] Rewrite(byte[] package, Action<XDocument> change)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(package)))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal).Open();
                if (entry.FullName != "content.xml") { from.CopyTo(to); continue; }
                var content = XDocument.Load(from);
                change(content);
                content.Save(to, SaveOptions.DisableFormatting);
            }
        return output.ToArray();
    }

    private static PlacedObject Placed(string id, EmbeddedObject content, string anchor, double x, double y, double width, double height, int order = 0) => new()
    {
        Id = id,
        Content = content,
        Anchor = A(anchor),
        OffsetX = Twip.FromCentimeters(x),
        OffsetY = Twip.FromCentimeters(y),
        Size = new DocumentSize(Twip.FromCentimeters(width), Twip.FromCentimeters(height)),
        ZOrder = order,
    };

    [Fact]
    public void UnaFormaRuotataSiScriveComeNelRiferimentoESiRilegge()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(Placed("forma", new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, RotationDegrees = 30 }, "C5", 0.5, 0.2, 3, 1));

        var package = Write(workbook);
        var shape = Content(package).Descendants(Namespaces.Draw + "custom-shape").Single();

        Assert.Null(shape.Attribute(Namespaces.Svg + "x"));
        Assert.StartsWith("rotate (-0.52", (string?)shape.Attribute(Namespaces.Draw + "transform"));
        Assert.Contains("translate (", (string?)shape.Attribute(Namespaces.Draw + "transform"));

        var back = Read(package).Sheets[0].Objects.Single();
        Assert.Equal(30, ((ShapeObject)back.Content).RotationDegrees, 3);
        Assert.Equal(A("C5"), back.Anchor);
        Assert.InRange(back.OffsetX.Value, Twip.FromCentimeters(0.5).Value - 2, Twip.FromCentimeters(0.5).Value + 2);
        Assert.InRange(back.OffsetY.Value, Twip.FromCentimeters(0.2).Value - 2, Twip.FromCentimeters(0.2).Value + 2);
    }

    [Fact]
    public void UnaFormaSpecchiataERuotataUsaIlVersoEsterno()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(Placed("forma", new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle, RotationDegrees = 30, FlipHorizontal = true }, "C5", 0.5, 0.2, 3, 1));

        var package = Write(workbook);
        var shape = Content(package).Descendants(Namespaces.Draw + "custom-shape").Single();

        Assert.StartsWith("rotate (0.52", (string?)shape.Attribute(Namespaces.Draw + "transform"));
        Assert.Equal(30, ((ShapeObject)Read(package).Sheets[0].Objects.Single().Content).RotationDegrees, 3);
    }

    [Fact]
    public void LaTrasformazioneEsternoConAncoraSopraLaFormaSiRiporta()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Layout.DefaultRowHeight = Twip.FromCentimeters(0.529);
        sheet.Objects.Add(Placed("forma", new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle }, "C4", 0, 0, 3, 1));

        var package = Rewrite(Write(workbook), content =>
        {
            var shape = content.Descendants(Namespaces.Draw + "custom-shape").Single();
            shape.SetAttributeValue(Namespaces.Svg + "x", null);
            shape.SetAttributeValue(Namespaces.Svg + "y", null);
            shape.SetAttributeValue(Namespaces.Draw + "transform", "rotate (-0.523598775598299) translate (0.95cm 0.045cm)");
        });

        var back = Read(package).Sheets[0].Objects.Single();
        Assert.Equal(30, ((ShapeObject)back.Content).RotationDegrees, 2);
        Assert.Equal(A("C5"), back.Anchor);
        Assert.InRange(back.OffsetX.ToCentimeters(), 0.48, 0.52);
        Assert.InRange(back.OffsetY.ToCentimeters(), 0.17, 0.23);
    }

    [Fact]
    public void UnVettorialeConservaRotazioneSpecchioEOrdine()
    {
        var workbook = Workbook.CreateDefault();
        var vector = new VectorGraphicObject
        {
            Id = "vett",
            RotationDegrees = 45,
            FlipHorizontal = true,
            Asset = new VectorAsset { Id = "v", Name = "v", SourceKind = VectorSourceKind.InlineSvg, SvgContent = Svg },
        };
        workbook.Sheets[0].Objects.Add(Placed("vett", vector, "E8", 0.3, 0, 2, 2, order: 4));

        var back = Read(Write(workbook)).Sheets[0].Objects.Single();
        var read = Assert.IsType<VectorGraphicObject>(back.Content);

        Assert.Equal(45, read.RotationDegrees, 3);
        Assert.True(read.FlipHorizontal);
        Assert.False(read.FlipVertical);
        Assert.Equal(4, back.ZOrder);
        Assert.Equal(A("E8"), back.Anchor);
    }

    [Fact]
    public void LeImmaginiAncorateAllaPaginaNonSiPerdono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(Placed("forma", new ShapeObject { Id = "forma", Shape = ShapeKind.Ellipse }, "A1", 0, 0, 2, 1));

        var column = workbook.Sheets[0].Layout.DefaultColumnWidth.ToCentimeters();
        var package = Rewrite(Write(workbook), content =>
        {
            var shape = content.Descendants(Namespaces.Draw + "custom-shape").Single();
            var table = shape.Ancestors(Namespaces.Table + "table").Single();
            shape.Remove();
            shape.SetAttributeValue(Namespaces.Table + "end-cell-address", null);
            shape.SetAttributeValue(Namespaces.Svg + "x", $"{(2 * column + 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture)}cm");
            table.Elements(Namespaces.Table + "table-column").Last().AddAfterSelf(new XElement(Namespaces.Table + "shapes", shape));
        });

        var back = Read(package).Sheets[0].Objects.Single();
        Assert.Equal(A("C1"), back.Anchor);
        Assert.InRange(back.OffsetX.ToCentimeters(), 0.48, 0.52);
    }

    [Fact]
    public void UnaFormaPersonalizzataSenzaTipoDiventaUnTracciato()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(Placed("forma", new ShapeObject { Id = "forma", Shape = ShapeKind.Rectangle }, "B2", 0, 0, 2, 2));

        var package = Rewrite(Write(workbook), content =>
        {
            var geometry = content.Descendants(Namespaces.Draw + "enhanced-geometry").Single();
            geometry.SetAttributeValue(Namespaces.Draw + "type", "non-primitive");
            geometry.SetAttributeValue(Namespaces.Svg + "viewBox", "0 0 100 100");
            geometry.SetAttributeValue(Namespaces.Draw + "enhanced-path", "M 0 0 L 100 0 50 100 Z N");
        });

        var freeform = Assert.IsType<FreeformObject>(Read(package).Sheets[0].Objects.Single().Content);
        Assert.Equal(3, freeform.Path.Figures.Single().Nodes.Count);
    }

    [Fact]
    public void LaCellaCollegataSiScriveComeLaLeggonoLeAltreSuite()
    {
        var workbook = Workbook.CreateDefault();
        var id = "3f2a";
        workbook.Sheets[0].Objects.Add(Placed(id, new FormControlObject
        {
            Id = id,
            ControlKind = FormControlKind.ListBox,
            Properties = new FormControlProperties { Name = "Elenco", LinkedCell = "J1", SourceRange = "E1:E3" },
        }, "H3", 0.2, 0, 4, 2));

        var package = Write(workbook);
        var control = Content(package).Descendants(Namespaces.Form + "listbox").Single();

        Assert.Equal("Sheet1.J1", (string?)control.Attribute(Namespaces.Form + "linked-cell"));
        Assert.Equal("Sheet1.E1:Sheet1.E3", (string?)control.Attribute(Namespaces.Form + "source-cell-range"));
        Assert.Equal("selection-indexes", (string?)control.Attribute(Namespaces.Form + "list-linkage-type"));
        Assert.True(System.Xml.XmlConvert.VerifyNCName((string)control.Attribute(XNamespace.Xml + "id")!) is not null);

        var stripped = Rewrite(package, content =>
        {
            foreach (var property in content.Descendants(Namespaces.Form + "property").ToList())
                if ((string?)property.Attribute(Namespaces.Form + "property-name") is "LinkedCell" or "SourceCellRange") property.Remove();
            content.Descendants(Namespaces.Form + "listbox").Single().SetAttributeValue(Namespaces.Form + "linked-cell", "$Sheet1.$J$1");
        });

        var back = Read(stripped).Sheets[0].Objects.Single();
        var read = Assert.IsType<FormControlObject>(back.Content);
        Assert.Equal(id, back.Id);
        Assert.Equal("J1", read.Properties.LinkedCell);
        Assert.Equal("E1:E3", read.Properties.SourceRange);
        Assert.InRange(back.OffsetX.Value, Twip.FromCentimeters(0.2).Value - 2, Twip.FromCentimeters(0.2).Value + 2);
    }

    [Fact]
    public void UnaRegolaSuPiuAreeSiScriveUnaVoltaSola()
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
        Assert.Single(Content(package).Descendants(Namespaces.Calcext + "conditional-format"));

        var rule = Assert.Single(Read(package).Sheets[0].ConditionalRules);
        Assert.Equal(new CellRange(A("A1"), A("A3")), rule.Range);
        Assert.Equal([new CellRange(A("D1"), A("D3"))], rule.AdditionalRanges);
    }

    [Fact]
    public void UnaFormulaConCellaBaseDiversaSiRiportaAllAncora()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("B2"), A("B4")),
            Formula = "B2>0",
            Style = new CellStyle { BackgroundHex = "#FF0000" },
        });

        var package = Rewrite(Write(workbook), content =>
        {
            var condition = content.Descendants(Namespaces.Calcext + "condition").Single();
            condition.SetAttributeValue(Namespaces.Calcext + "value", "formula-is([.A1]>0)");
            condition.SetAttributeValue(Namespaces.Calcext + "base-cell-address", "Sheet1.A1");
        });

        var rule = Assert.Single(Read(package).Sheets[0].ConditionalRules);
        Assert.Contains("B2", rule.Formula);
    }

    [Fact]
    public void NomiLocaliUgualiSuDueFogliSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Nord");
        workbook.DefineName(new DefinedName("Tasso", "$A$1", "Sheet1"));
        workbook.DefineName(new DefinedName("Tasso", "$B$2", "Nord"));
        workbook.DefineName(new DefinedName("Tasso", "0.5"));

        var back = Read(Write(workbook));

        Assert.Equal(3, back.DefinedNames.Count);
        Assert.Contains("A$1", back.FindName("Tasso", "Sheet1")!.Expression);
        Assert.Contains("B$2", back.FindName("Tasso", "Nord")!.Expression);
        Assert.Equal("0.5", back.FindName("Tasso")!.Expression);
    }
}
