using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfRootAuditTests
{
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;
    private readonly XNamespace _office = OdfPackage.Namespaces.Office;

    [Fact(Timeout = 20_000)]
    public async Task Una_cella_dello_scenario_ripetuta_senza_fine_non_blocca_l_apertura()
    {
        var workbook = await Task.Run(() => Reopened(WithScenario(), content =>
            ScenarioCell(content).SetAttributeValue(_table + "number-columns-repeated", int.MaxValue.ToString())));

        var scenario = workbook.Sheets[0].Scenarios[0];
        Assert.Equal(10, scenario.Values[CellAddress.First].AsNumber());
        Assert.Equal(10, scenario.Values[new CellAddress(0, 1)].AsNumber());
    }

    [Fact]
    public void Le_righe_ripetute_dello_scenario_si_leggono_tutte()
    {
        var workbook = Reopened(WithScenario(), content =>
            ScenarioCell(content).Parent!.SetAttributeValue(_table + "number-rows-repeated", "2"));

        var scenario = workbook.Sheets[0].Scenarios[0];
        Assert.Equal(10, scenario.Values[new CellAddress(1, 0)].AsNumber());
    }

    [Fact]
    public void Un_colore_della_linguetta_non_valido_si_ignora()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].TabColorHex = "#FF0000";
        var workbook = Reopened(source, content =>
        {
            foreach (var attribute in content.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "tab-color")) attribute.Value = "rosso";
        });

        Assert.Null(workbook.Sheets[0].TabColorHex);
    }

    [Fact]
    public void Una_versione_con_un_nome_che_esce_dalla_cartella_non_si_legge_ne_si_riscrive()
    {
        using var source = new MemoryStream();
        using (var package = OdfPackage.Create(source, OdfPackage.MimeTypes.Text))
        {
            package.WriteBytes("Versions/../evil", [1, 2, 3]);
            package.WriteXml("Versions/VersionList.xml", new XDocument(new XElement(OdfPackage.Namespaces.VersionList + "version-list",
                new XElement(OdfPackage.Namespaces.VersionList + "version-entry", new XAttribute(OdfPackage.Namespaces.VersionList + "title", "../evil")))));
        }
        source.Position = 0;

        var versions = new PulseOffice.Document.Versions.DocumentVersionCollection();
        using (var package = OdfPackage.OpenRead(source)) new OdfVersionMapper().Read(package, versions);

        Assert.Equal(0, versions.Count);
    }

    [Fact]
    public void Un_numero_di_righe_negativo_della_query_si_legge_come_zero()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].QueryBindings.AddOrUpdate(new QueryBinding { Id = "q1", DisplayName = "q", ConnectionId = "c", Query = new PulseOffice.Data.QueryDefinition { Sql = "select 1" }, Anchor = CellAddress.First, Range = new CellRange(CellAddress.First, CellAddress.First), LastRowCount = 3 });
        var workbook = Reopened(source, content =>
        {
            foreach (var attribute in content.Descendants().Attributes().Where(attribute => attribute.Name == XName.Get("rows", "urn:pulseoffice:query"))) attribute.Value = "-7";
        });

        Assert.Equal(0, workbook.Sheets[0].QueryBindings.All.Single().LastRowCount);
    }

    [Theory]
    [InlineData("Infinitypt", "#00FF00")]
    [InlineData("1e300pt", "#00FF00")]
    [InlineData("2pt", "verde")]
    public void Lo_stile_delle_sparkline_letto_resta_disegnabile(string width, string color)
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].Sparklines.Add(new SparklineGroup
        {
            Id = "g",
            Sparklines = [new Sparkline(CellAddress.First, new CellRange(new CellAddress(0, 1), new CellAddress(0, 3)))],
        });
        var workbook = Reopened(source, content =>
        {
            var group = content.Descendants(OdfPackage.Namespaces.Calcext + "sparkline-group").Single();
            group.SetAttributeValue(OdfPackage.Namespaces.Calcext + "line-width", width);
            group.SetAttributeValue(OdfPackage.Namespaces.Calcext + "color-series", color);
        });

        var style = workbook.Sheets[0].Sparklines.Groups.Single().Style;
        Assert.InRange(style.LineWidth, 0.01, 100);
        Assert.NotNull(PulseOffice.Foundation.Colors.ArgbColor.FromHashedRgbHex(style.ColorHex));
    }

    [Fact]
    public void La_data_di_creazione_mantiene_l_istante_in_ogni_fuso()
    {
        var created = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.FromHours(-11));
        var mapper = new OdfMetaMapper();
        var document = mapper.Build(new PulseOffice.Document.Metadata.DocumentProperties { Created = created });
        var properties = new PulseOffice.Document.Metadata.DocumentProperties();

        mapper.Read(document, properties);

        Assert.Equal(created.UtcDateTime, properties.Created!.Value.UtcDateTime);
    }

    [Fact]
    public void Due_validazioni_con_lo_stesso_nome_non_impediscono_l_apertura()
    {
        var workbook = Reopened(WithValidation(), content =>
        {
            var declared = content.Descendants(_table + "content-validation").First();
            declared.AddAfterSelf(new XElement(declared));
        });

        Assert.Single(workbook.Sheets[0].Validations);
    }

    [Fact]
    public void La_validazione_copre_le_righe_ripetute_e_resta_nel_foglio()
    {
        var workbook = Reopened(WithValidation(), content =>
        {
            var cell = content.Descendants(_table + "table-cell").First(cell => cell.Attribute(_table + "content-validation-name") is not null);
            cell.SetAttributeValue(_table + "number-columns-repeated", int.MaxValue.ToString());
            cell.Parent!.SetAttributeValue(_table + "number-rows-repeated", "3");
        });

        var range = workbook.Sheets[0].Validations.Single().Range;
        Assert.Equal(2, range.End.Row);
        Assert.True(range.End.IsValid);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Un_volume_non_finito_resta_pieno(string decibels)
    {
        var plugin = new XElement(OdfPackage.Namespaces.Draw + "plugin",
            new XElement(OdfPackage.Namespaces.Draw + "param", new XAttribute(OdfPackage.Namespaces.Draw + "name", "VolumeDB"), new XAttribute(OdfPackage.Namespaces.Draw + "value", decibels)));

        Assert.Equal(1, new OdfMediaPlayback().Read(plugin).Volume);
    }

    private Workbook WithValidation()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));
        workbook.Sheets[0].Validations.Add(new DataValidation { Range = new CellRange(CellAddress.First, CellAddress.First), Kind = ValidationKind.WholeNumber, Operator = ValidationOperator.Between, FirstFormula = "1", SecondFormula = "9" });
        return workbook;
    }

    [Fact]
    public void Gruppi_di_animazione_annidati_senza_fine_non_fanno_traboccare_la_pila()
    {
        var anim = OdfPackage.Namespaces.Anim;
        var nested = new XElement(anim + "par");
        for (var depth = 0; depth < 100_000; depth++) nested = new XElement(anim + "par", nested);

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { new OdfAnimationBehaviourCodec().Read(new XElement(anim + "par", nested)); }
            catch (Exception ex) { failure = ex; }
        }, 256 * 1024);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void Un_area_della_mappa_immagine_con_misure_infinite_si_scarta()
    {
        var draw = OdfPackage.Namespaces.Draw;
        var svg = OdfPackage.Namespaces.Svg;
        var frame = new XElement(draw + "frame", new XElement(draw + "image-map",
            new XElement(draw + "area-rectangle", new XAttribute(svg + "x", "1e400cm"), new XAttribute(svg + "y", "0cm"), new XAttribute(svg + "width", "1cm"), new XAttribute(svg + "height", "1cm"))));

        var map = new OdfImageMapCodec().Read(frame, new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(5), PulseOffice.Foundation.Units.Twip.FromCentimeters(5)));

        Assert.All(map.Areas.SelectMany(area => area.Points), point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("-Infinity")]
    [InlineData("-3")]
    public void Un_filtro_dei_primi_elementi_con_un_conteggio_non_valido_ne_usa_dieci(string count)
    {
        var workbook = Reopened(WithFilter(new ColumnFilter(1) { Top = new TopItemsCriterion(false, false, 5) }), content =>
        {
            foreach (var condition in content.Descendants(_table + "filter-condition")) condition.SetAttributeValue(_table + "value", count);
        });

        Assert.Equal(10, workbook.Sheets[0].AutoFilter.Filters.Single().Top!.Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("99")]
    public void Un_filtro_su_una_colonna_inesistente_si_scarta(string field)
    {
        var workbook = Reopened(WithFilter(new ColumnFilter(1) { Criteria = ">3" }), content =>
        {
            foreach (var condition in content.Descendants(_table + "filter-condition")) condition.SetAttributeValue(_table + "field-number", field);
        });

        Assert.Empty(workbook.Sheets[0].AutoFilter.Filters);
    }

    private Workbook WithFilter(ColumnFilter filter)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Text("a"));
        workbook.Sheets[0].AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(5, 2)));
        workbook.Sheets[0].AutoFilter.Set(filter);
        return workbook;
    }

    [Theory]
    [InlineData("Infinity")]
    [InlineData("1e400")]
    [InlineData("NaN")]
    public void Un_intervallo_di_raggruppamento_non_finito_della_pivot_si_ignora(string step)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Text("Valore"));
        var name = workbook.Sheets[0].Name;
        var content = new XElement(_table + "data-pilot-tables",
            new XElement(_table + "data-pilot-table",
                new XAttribute(_table + "target-range-address", name + ".E1:F5"),
                new XElement(_table + "source-cell-range", new XAttribute(_table + "cell-range-address", name + ".A1:A5")),
                new XElement(_table + "data-pilot-field", new XAttribute(_table + "source-field-name", "Valore"), new XAttribute(_table + "orientation", "row"),
                    new XElement(_table + "data-pilot-groups", new XAttribute(_table + "step", step)))));

        new OdfPivotMapper().Read(content, workbook);

        Assert.Null(workbook.Pivots.Single().Fields.Single().NumericGroupInterval);
    }

    [Fact]
    public void Una_formula_con_let_annidati_senza_fine_si_esporta_senza_traboccare()
    {
        var formula = "=" + string.Concat(Enumerable.Repeat("LET(x;1;", 20_000)) + "x" + new string(')', 20_000);

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { new OdfFormulaSyntax().ToOdf(formula); }
            catch (Exception ex) { failure = ex; }
        }, 256 * 1024);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void Una_transizione_assente_si_traduce_in_smil_senza_eccezioni()
    {
        var smil = new SmilTransitionMapper().ToSmil(new PulseOffice.Presentations.SlideTransition());

        Assert.Equal("fade", smil.Type);
    }

    [Fact]
    public void L_immagine_della_riga_della_firma_resta_entro_il_lato_massimo()
    {
        var rasterizer = new MeasuringRasterizer();
        var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(50_000), PulseOffice.Foundation.Units.Twip.FromCentimeters(20_000));

        new SignatureLineImage(rasterizer).UnsignedRaster(new PulseOffice.Document.Forms.SignatureLineObject { Id = "s" }, size);

        Assert.InRange(Math.Max(rasterizer.Width, rasterizer.Height), 1, 2048);
    }

    [Fact]
    public void Un_nome_di_funzione_con_prefissi_ripetuti_si_importa_senza_traboccare()
    {
        var formula = "of:=" + string.Concat(Enumerable.Repeat("COM.MICROSOFT.", 50_000)) + "XLOOKUP(1;2;3)";

        Exception? failure = null;
        string? imported = null;
        var thread = new Thread(() =>
        {
            try { imported = new OdfFormulaSyntax().ToInternal(formula); }
            catch (Exception ex) { failure = ex; }
        }, 256 * 1024);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.Equal("=XLOOKUP(1;2;3)", imported);
    }

    private sealed class MeasuringRasterizer : PulseOffice.Drawing.Assets.IVectorRasterizer
    {
        public int Width { get; private set; }

        public int Height { get; private set; }

        public byte[]? Png(byte[] svg, int pixelWidth, int pixelHeight)
        {
            Width = pixelWidth;
            Height = pixelHeight;
            return null;
        }
    }

    private XElement ScenarioCell(XDocument content) =>
        content.Descendants(_table + "table").Single(table => table.Element(_table + "scenario") is not null)
            .Descendants(_table + "table-cell").First(cell => (string?)cell.Attribute(_office + "value") == "10");

    private Workbook WithScenario()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.Scenarios.Add(new Scenario
        {
            Name = "Prova",
            Range = new CellRange(CellAddress.First, new CellAddress(1, 1)),
            Values = new Dictionary<CellAddress, CellValue> { [CellAddress.First] = CellValue.Number(10) },
        });
        return workbook;
    }

    private Workbook Reopened(Workbook workbook, Action<XDocument> change)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            change(content);
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            content.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }
}
