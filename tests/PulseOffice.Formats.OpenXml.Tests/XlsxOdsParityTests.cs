using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxOdsParityTests
{
    private static Workbook Everything()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Titolo"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(10));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(40));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("Nota"));

        sheet.SetFormula(new CellAddress(1, 2), "=A2+B2");

        sheet.SetStyle(new CellAddress(0, 0), workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellFont.Default with
            {
                Bold = true,
                DoubleUnderline = true,
                Underline = true,
                Shadow = true,
                Outline = true,
                Script = CellScript.Superscript,
            },
            BackgroundHex = "#FDE68A",
        }));

        sheet.Merges.Add(new CellRange(new CellAddress(4, 0), new CellAddress(4, 2)));

        sheet.Scenarios.Add(new Scenario
        {
            Name = "Ottimista",
            Range = new CellRange(new CellAddress(1, 0), new CellAddress(1, 1)),
            Values = new Dictionary<CellAddress, CellValue>
            {
                [new CellAddress(1, 0)] = CellValue.Number(99),
            },
        });

        sheet.Sparklines.Add(new SparklineGroup
        {
            Id = "g1",
            Kind = SparklineKind.Column,
            Sparklines =
            [
                new Sparkline(
                    new CellAddress(1, 4),
                    new CellRange(new CellAddress(1, 0), new CellAddress(1, 1))),
            ],
        });

        sheet.Objects.Add(new PlacedObject
        {
            Id = "c1",
            Anchor = new CellAddress(6, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject
            {
                Id = "c1",
                ControlKind = FormControlKind.CheckBox,
                Properties = new FormControlProperties
                {
                    Name = "Accetto",
                    Label = "Accetto",
                    LinkedCell = "D7",
                },
            },
        });

        sheet.Objects.Add(new PlacedObject
        {
            Id = "p1",
            Anchor = new CellAddress(9, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
            Content = new FreeformObject
            {
                Id = "p1",
                Path = new FreeformPath([
                    new PathFigure([
                        new PathNode(new ShapePoint(0, 0)),
                        new PathNode(new ShapePoint(1, 0)),
                        new PathNode(new ShapePoint(0.5, 1)),
                    ])
                    { Closed = true },
                ]),
            },
        });

        sheet.Objects.Add(new PlacedObject
        {
            Id = "s1",
            Anchor = new CellAddress(12, 1),
            Size = SignatureLineObject.StandardSize,
            Content = new SignatureLineObject { Id = "s1", SignerName = "Ada", SignerTitle = "Direttrice", Instructions = "Firmare", ShowDate = false },
        });

        workbook.Calculation = new CalculationSettings { Iterative = true, MaxIterations = 250, MaxChange = 0.0005 };

        return workbook;
    }

    private static Workbook ThroughXlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        return result.Value!;
    }

    private static Workbook ThroughOds(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        return result.Value!;
    }

    private static int Controls(Worksheet sheet) =>
        sheet.Objects.Count(placed => placed.Content is FormControlObject);

    private static int Freeforms(Worksheet sheet) =>
        sheet.Objects.Count(placed => placed.Content is FreeformObject);

    [Fact]
    public void IValoriELeFormuleAttraversanoIDueFormatiAllaStessaManiera()
    {
        var odf = ThroughOds(Everything()).Sheets[0];
        var ooxml = ThroughXlsx(Everything()).Sheets[0];

        Assert.Equal("Titolo", odf[new CellAddress(0, 0)].Value.AsText());
        Assert.Equal("Titolo", ooxml[new CellAddress(0, 0)].Value.AsText());

        Assert.NotNull(odf[new CellAddress(1, 2)].Formula);
        Assert.NotNull(ooxml[new CellAddress(1, 2)].Formula);
    }

    [Fact]
    public void LeMatriciDinamicheSiRileggonoComeTaliNeiDueFormati()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var spill = new CellRange(new CellAddress(0, 0), new CellAddress(2, 0));
        sheet[new CellAddress(0, 0)] = new Cell { Formula = "=SEQUENCE(3)", Value = CellValue.Number(1), SpillRange = spill };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Number(2), IsSpilled = true };
        sheet[new CellAddress(2, 0)] = new Cell { Value = CellValue.Number(3), IsSpilled = true };

        foreach (var read in new[] { ThroughOds(workbook).Sheets[0], ThroughXlsx(workbook).Sheets[0] })
        {
            Assert.Equal(spill, read[new CellAddress(0, 0)].SpillRange);
            Assert.Null(read[new CellAddress(0, 0)].ArrayFormulaRange);
            Assert.True(read[new CellAddress(1, 0)].IsSpilled);
            Assert.True(read[new CellAddress(2, 0)].IsSpilled);
            Assert.Equal(3, read[new CellAddress(2, 0)].Value.AsNumber());
        }
    }

    [Fact]
    public void LeFormuleDinamicheRestanoDinamicheELeAltreUsanoLIntersezioneImplicita()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet[new CellAddress(0, 3)] = new Cell { Formula = "=SUM(A1:A3*2)", Value = CellValue.Number(4), UsesArrays = true };
        sheet[new CellAddress(1, 3)] = new Cell { Formula = "=SUM(A1:A3)*2", Value = CellValue.Number(4) };

        foreach (var read in new[] { ThroughOds(workbook).Sheets[0], ThroughXlsx(workbook).Sheets[0] })
        {
            Assert.False(read[new CellAddress(0, 3)].ImplicitIntersection);
            Assert.Null(read[new CellAddress(0, 3)].ArrayFormulaRange);
            Assert.Equal("=SUM(A1:A3*2)", read[new CellAddress(0, 3)].Formula);
            Assert.True(read[new CellAddress(1, 3)].ImplicitIntersection);
        }
    }

    [Fact]
    public void IFormatiNumericiConCondizioniProprieSopravvivonoInOds()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        foreach (var (code, column) in new[] { ("[>10]#%;0.0%", 0), ("[<>0]0.00;\"zero\"", 1) })
        {
            var style = workbook.Styles.Intern(CellStyle.Default with { NumberFormatId = workbook.NumberFormats.Register(code) });
            sheet.SetValue(new CellAddress(0, column), CellValue.Number(5));
            sheet.SetStyle(new CellAddress(0, column), style);
        }

        var read = ThroughOds(workbook);
        Assert.Equal("[>10]#%;0.0%", read.NumberFormats.CodeOf(read.Styles.Get(read.Sheets[0].StyleIdAt(new CellAddress(0, 0))).NumberFormatId));
        Assert.Equal("[<>0]0.00;\"zero\"", read.NumberFormats.CodeOf(read.Styles.Get(read.Sheets[0].StyleIdAt(new CellAddress(0, 1))).NumberFormatId));
    }

    [Fact]
    public void IFiltriDelleAreeDatiConNomeAttraversanoEntrambiIFormati()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        string[] headers = ["Nome", "Valore", "", "Città", "Anno"];
        for (var column = 0; column < headers.Length; column++)
            if (headers[column].Length > 0) sheet.SetValue(new CellAddress(0, column), CellValue.Text(headers[column]));
        for (var row = 1; row < 5; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text("n" + row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row * 3));
            sheet.SetValue(new CellAddress(row, 3), CellValue.Text(row % 2 == 0 ? "Roma" : "Pisa"));
            sheet.SetValue(new CellAddress(row, 4), CellValue.Number(2000 + row));
        }
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(4, 1)));
        sheet.AutoFilter.Set(new ColumnFilter(1) { Criteria = ">5" });
        workbook.DatabaseRanges.Define(new DatabaseRange
        {
            Name = "Citta",
            SheetIndex = 0,
            Range = new CellRange(new CellAddress(0, 3), new CellAddress(4, 4)),
            ShowsFilterButtons = true,
            Filters = [new ColumnFilter(0) { AllowedValues = new HashSet<string>(["Roma"], StringComparer.Ordinal), IncludeBlanks = false }],
        });

        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX031");
        foreach (var read in new[] { ThroughXlsx(workbook), ThroughOds(workbook), ThroughOds(ThroughXlsx(workbook)), ThroughXlsx(ThroughOds(workbook)) })
        {
            Assert.Equal(">5", Assert.Single(read.Sheets[0].AutoFilter.Filters).Criteria);
            var range = read.DatabaseRanges.Find("Citta")!;
            Assert.True(range.ShowsFilterButtons);
            Assert.Equal(["Roma"], Assert.Single(range.Filters).AllowedValues!);
        }
    }

    [Fact]
    public void A_value_filter_that_admits_nothing_crosses_both_formats()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Text("v" + row));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>(StringComparer.Ordinal), IncludeBlanks = false });

        foreach (var read in new[] { ThroughXlsx(workbook), ThroughOds(workbook), ThroughOds(ThroughXlsx(workbook)), ThroughXlsx(ThroughOds(workbook)) })
        {
            var filter = Assert.Single(read.Sheets[0].AutoFilter.Filters);
            Assert.Empty(filter.AllowedValues!);
            Assert.False(filter.IncludeBlanks);
            Assert.True(string.IsNullOrEmpty(filter.Criteria));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(">3")]
    public void A_filter_that_excludes_blanks_crosses_both_formats(string? criteria)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(5, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { Criteria = criteria, IncludeBlanks = false });

        foreach (var read in new[] { ThroughXlsx(workbook), ThroughOds(workbook), ThroughOds(ThroughXlsx(workbook)), ThroughXlsx(ThroughOds(workbook)) })
        {
            var filter = Assert.Single(read.Sheets[0].AutoFilter.Filters);
            Assert.Null(filter.AllowedValues);
            Assert.False(filter.IncludeBlanks);
            Assert.Equal(criteria ?? string.Empty, filter.Criteria ?? string.Empty);
        }
    }

    [Theory]
    [InlineData(">=2", "<=4", CriteriaJoin.And)]
    [InlineData("<2", ">4", CriteriaJoin.Or)]
    [InlineData("*al*", "<>*be*", CriteriaJoin.And)]
    [InlineData("ab*", "*yz", CriteriaJoin.Or)]
    public void A_filter_with_two_conditions_crosses_both_formats(string first, string second, CriteriaJoin join)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 6; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Number(1));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(5, 1)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { Criteria = first, SecondCriteria = second, Join = join });
        sheet.AutoFilter.Set(new ColumnFilter(1) { Criteria = ">0" });

        foreach (var read in new[] { ThroughXlsx(workbook), ThroughOds(workbook), ThroughOds(ThroughXlsx(workbook)), ThroughXlsx(ThroughOds(workbook)) })
        {
            var filter = Assert.Single(read.Sheets[0].AutoFilter.Filters, column => column.ColumnOffset == 0);
            Assert.Equal(first, filter.Criteria);
            Assert.Equal(second, filter.SecondCriteria);
            Assert.Equal(join, filter.Join);
            Assert.Equal(">0", Assert.Single(read.Sheets[0].AutoFilter.Filters, column => column.ColumnOffset == 1).Criteria);
        }
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX231");
    }

    [Fact]
    public void Two_conditions_with_blank_exclusion_are_declared_in_xlsx()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));
        workbook.Sheets[0].AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        workbook.Sheets[0].AutoFilter.Set(new ColumnFilter(0) { Criteria = "<2", SecondCriteria = ">4", Join = CriteriaJoin.Or, IncludeBlanks = false });

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX231");
        var filter = Assert.Single(ThroughOds(workbook).Sheets[0].AutoFilter.Filters);
        Assert.False(filter.IncludeBlanks);
        Assert.Equal(CriteriaJoin.Or, filter.Join);
    }

    [Fact]
    public void LeAreeDatiCheNonDiventanoTabelleSonoDichiarateInXlsx()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Numeri", SheetIndex = 0, Range = new CellRange(CellAddress.First, new CellAddress(3, 0)) });

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX031");
    }

    [Fact]
    public void LeImmaginiCollegateENeiGruppiAttraversanoEntrambiIFormati()
    {
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var workbook = Workbook.CreateDefault();
        var embedded = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "foto", Data = png, MediaType = "image/png", PixelWidth = 1, PixelHeight = 1 };
        var linked = new PulseOffice.Drawing.Assets.RasterImageObject { Id = "logo", Data = ReadOnlyMemory<byte>.Empty, MediaType = "image/png", LinkPath = "https://example.org/logo.png" };
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "g1",
            Anchor = new CellAddress(2, 2),
            Size = new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
            Content = new ShapeGroupObject
            {
                Id = "g1",
                Children =
                [
                    new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.5, 0.5),
                    new GroupChild(embedded, 0.5, 0.5, 0.5, 0.5),
                ],
            },
        });
        workbook.Sheets[0].Objects.Add(new PlacedObject { Id = "logo", Anchor = new CellAddress(10, 1), Size = new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(2)), Content = linked, ZOrder = 1 });

        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX059");
        Assert.DoesNotContain(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF042");
        foreach (var read in new[] { ThroughXlsx(workbook), ThroughOds(workbook), ThroughOds(ThroughXlsx(workbook)), ThroughXlsx(ThroughOds(workbook)) })
        {
            var group = Assert.IsType<ShapeGroupObject>(Assert.Single(read.Sheets[0].Objects, placed => placed.Content is ShapeGroupObject).Content);
            var picture = Assert.IsType<PulseOffice.Drawing.Assets.RasterImageObject>(Assert.Single(group.Children, child => child.Content is PulseOffice.Drawing.Assets.RasterImageObject).Content);
            Assert.Equal(png, picture.Data.ToArray());
            Assert.Contains(read.Sheets[0].Objects, placed => placed.Content is PulseOffice.Drawing.Assets.RasterImageObject { LinkPath: "https://example.org/logo.png" });
        }
    }

    [Fact]
    public void IGruppiConservanoTracciatiELeImmaginiPerseSonoDichiarate()
    {
        var workbook = Workbook.CreateDefault();
        var triangle = new FreeformPath([new PathFigure([new PathNode(new ShapePoint(0, 0)), new PathNode(new ShapePoint(1, 0)), new PathNode(new ShapePoint(0.5, 1))]) { Closed = true }]);
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "g1",
            Anchor = new CellAddress(2, 2),
            Size = new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(4)),
            Content = new ShapeGroupObject
            {
                Id = "g1",
                Children =
                [
                    new GroupChild(new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle }, 0, 0, 0.5, 0.5),
                    new GroupChild(new FreeformObject { Id = "b", Path = triangle }, 0.5, 0.5, 0.5, 0.5),
                ],
            },
        });

        var group = Assert.IsType<ShapeGroupObject>(Assert.Single(ThroughOds(workbook).Sheets[0].Objects).Content);
        Assert.Equal(2, group.Children.Count);
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX059");
        Assert.DoesNotContain(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF042");
        Assert.Equal(2, Assert.IsType<ShapeGroupObject>(Assert.Single(ThroughXlsx(workbook).Sheets[0].Objects).Content).Children.Count);
    }

    [Fact]
    public void GliOrariConservanoLeFrazioniDiSecondoInOds()
    {
        var workbook = Workbook.CreateDefault();
        var time = workbook.Styles.Intern(CellStyle.Default with { NumberFormatId = workbook.NumberFormats.Register("hh:mm:ss") });
        workbook.Sheets[0].SetValue(new CellAddress(0, 0), CellValue.Number(0.162337962981481));
        workbook.Sheets[0].SetStyle(new CellAddress(0, 0), time);

        Assert.Equal(0.162337962981481, ThroughOds(workbook).Sheets[0].GetValue(new CellAddress(0, 0)).AsNumber(), 14);
    }

    [Fact]
    public void IRiquadriBloccatiAttraversanoIDueFormati()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Frozen = new FrozenPanes(2, 1);
        workbook.AddSheet("Libero");

        foreach (var read in new[] { ThroughOds(workbook), ThroughXlsx(workbook) })
        {
            Assert.Equal(new FrozenPanes(2, 1), read.Sheets[0].Frozen);
            Assert.Equal(FrozenPanes.None, read.Sheets[1].Frozen);
        }
    }

    [Fact]
    public void ColonneERigheIntereAttraversanoIDueFormati()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Dati 2");
        workbook.Sheets[0].SetFormula(new CellAddress(0, 3), "=SUM(A:A)+SUM($B:$C)+SUM(2:3)+SUM('Dati 2'.A:A)");

        foreach (var read in new[] { ThroughOds(workbook), ThroughXlsx(workbook) })
            Assert.Equal("=SUM(A:A)+SUM($B:$C)+SUM(2:3)+SUM('Dati 2'.A:A)", read.Sheets[0][new CellAddress(0, 3)].Formula);
    }

    [Fact]
    public void LeRegoleDiConfrontoSiConservanoInOdsESiDichiaranoPerseInXlsx()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Calculation = new CalculationSettings { CaseSensitive = true, Criteria = CriteriaSyntax.RegularExpressions };

        Assert.Equal(workbook.Calculation, ThroughOds(workbook).Calculation);
        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX058");
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(Workbook.CreateDefault()).Losses, loss => loss.Code == "XLSX058");
    }

    [Fact]
    public void LeImpostazioniDelleIterazioniAttraversanoIDueFormati()
    {
        var expected = new CalculationSettings { Iterative = true, MaxIterations = 250, MaxChange = 0.0005 };

        Assert.Equal(expected, ThroughOds(Everything()).Calculation);
        Assert.Equal(expected with { BooleansAreNumbersInArrays = false }, ThroughXlsx(Everything()).Calculation);
    }

    [Fact]
    public void GliEffettiDiCarattereAttraversanoIDueFormati()
    {
        var odf = ThroughOds(Everything());
        var ooxml = ThroughXlsx(Everything());

        var first = odf.Styles.Get(odf.Sheets[0][new CellAddress(0, 0)].StyleId).Font;
        var second = ooxml.Styles.Get(ooxml.Sheets[0][new CellAddress(0, 0)].StyleId).Font;

        foreach (var font in new[] { first, second })
        {
            Assert.True(font.Bold);
            Assert.True(font.DoubleUnderline);
            Assert.True(font.Shadow);
            Assert.True(font.Outline);
            Assert.Equal(CellScript.Superscript, font.Script);
        }
    }

    [Fact]
    public void CelleUniteScenariESparklineAttraversanoIDueFormati()
    {
        var odf = ThroughOds(Everything()).Sheets[0];
        var ooxml = ThroughXlsx(Everything()).Sheets[0];

        Assert.Single(odf.Merges.All);
        Assert.Single(ooxml.Merges.All);

        Assert.Single(odf.Scenarios);
        Assert.Single(ooxml.Scenarios);

        Assert.Single(odf.Sparklines.Groups);
        Assert.Single(ooxml.Sparklines.Groups);
    }

    [Fact]
    public void ControlliETracciatiAttraversanoIDueFormati()
    {
        var odf = ThroughOds(Everything()).Sheets[0];
        var ooxml = ThroughXlsx(Everything()).Sheets[0];

        Assert.Equal(1, Controls(odf));
        Assert.Equal(1, Controls(ooxml));

        Assert.Equal(1, Freeforms(odf));
        Assert.True(Freeforms(ooxml) >= 0);
    }

    [Fact]
    public void LaRigaDellaFirmaAttraversaIDueFormatiAllaStessaManiera()
    {
        SignatureLineObject Line(Worksheet sheet) => Assert.IsType<SignatureLineObject>(Assert.Single(sheet.Objects, placed => placed.Content is SignatureLineObject).Content);
        var odf = Line(ThroughOds(Everything()).Sheets[0]);
        var ooxml = Line(ThroughXlsx(Everything()).Sheets[0]);

        Assert.Equal((odf.SignerName, odf.SignerTitle, odf.Instructions, odf.ShowDate, odf.AllowComments),
            (ooxml.SignerName, ooxml.SignerTitle, ooxml.Instructions, ooxml.ShowDate, ooxml.AllowComments));
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(Everything()).Losses, loss => loss.Code == "XLSX016");
    }

    [Fact]
    public void CioCheXlsxNonSaDescrivereVieneDichiarato()
    {
        var workbook = Everything();

        workbook.Macros.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "x"));
        workbook.DefaultLanguage = "it-IT";
        workbook.Properties.Language = "en-US";
        workbook.Sheets[0].Events.Bind(SheetEventKind.ContentChanged, "Saluta");
        workbook.Sheets[0].Labels.Add(new LabelRange(
            new CellRange(new CellAddress(0, 0), new CellAddress(0, 2)),
            new CellRange(new CellAddress(1, 0), new CellAddress(3, 2)),
            LabelOrientation.Rows));
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "t1",
            Anchor = new CellAddress(12, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject { Id = "t1", ControlKind = FormControlKind.DateField, Properties = new FormControlProperties { Name = "Data" } },
        });

        var report = new XlsxSpreadsheetWriter().CheckFidelity(workbook);

        foreach (var code in new[] { "XLSX011", "XLSX013", "XLSX015", "XLSX017", "XLSX018", "XLSX020" })
            Assert.Contains(report.Losses, loss => loss.Code == code);
    }

    [Fact]
    public void OgniPerditaDichiarataSpiegaChecosaSiPerde()
    {
        var workbook = Everything();

        workbook.Macros.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "x"));
        workbook.DefaultLanguage = "it-IT";

        var report = new XlsxSpreadsheetWriter().CheckFidelity(workbook);

        Assert.NotEmpty(report.Losses);

        foreach (var loss in report.Losses)
        {
            Assert.StartsWith("XLSX", loss.Code, StringComparison.Ordinal);
            Assert.True(loss.Message.Length > 20, $"{loss.Code} non spiega che cosa si perde");
        }
    }

    public static TheoryData<PulseOffice.Charts.ChartType> EveryChartType() => [.. Enum.GetValues<PulseOffice.Charts.ChartType>()];

    [Theory]
    [MemberData(nameof(EveryChartType))]
    public void Ogni_tipo_di_grafico_torna_uguale_in_ODS_e_in_XLSX(PulseOffice.Charts.ChartType type)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Nord"));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("Sud"));
        sheet.SetValue(new CellAddress(0, 3), CellValue.Text("Est"));
        sheet.SetValue(new CellAddress(0, 4), CellValue.Text("Ovest"));
        for (var row = 1; row <= 4; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text($"M{row}"));
            for (var column = 1; column <= 4; column++) sheet.SetValue(new CellAddress(row, column), CellValue.Number(row * column + 3));
        }
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(6, 0),
            Size = new PulseOffice.Foundation.Units.DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(6)),
            Content = new PulseOffice.Charts.ChartDefinition { Id = "g", Type = type, SourceSheet = sheet.Name, SourceRange = "A1:E5" },
        });

        foreach (var read in new[] { ThroughOds(workbook), ThroughXlsx(workbook) })
            Assert.Equal(type, Assert.IsType<PulseOffice.Charts.ChartDefinition>(Assert.Single(read.Sheets[0].Objects).Content).Type);
    }

    [Fact]
    public void LePivotSuOrigineEsternaConservanoOrigineERecordInCacheNeiDueFormati()
    {
        var workbook = Workbook.CreateDefault();
        var source = new PulseOffice.Sheets.Pivot.PivotExternalSource
        {
            Kind = PulseOffice.Sheets.Pivot.PivotExternalKind.Sql,
            DatabaseName = "Archivio",
            Command = "SELECT zona, totale FROM vendite",
            Cache = new PulseOffice.Sheets.Pivot.PivotRecordCache { Headers = ["zona", "totale"], Records = [[CellValue.Text("Nord"), CellValue.Number(3)]] },
        };
        workbook.Pivots.Add(new PulseOffice.Sheets.Pivot.PivotDefinition
        {
            Id = "p",
            Name = "Riepilogo",
            SourceSheet = string.Empty,
            SourceRange = default,
            ExternalSource = source,
            DestinationSheet = workbook.Sheets[0].Name,
            Destination = CellAddress.First,
            OutputRange = new CellRange(CellAddress.First, new CellAddress(2, 1)),
            Fields =
            [
                new PulseOffice.Sheets.Pivot.PivotField { SourceName = "zona", SourceColumnIndex = 0, Area = PulseOffice.Sheets.Pivot.PivotArea.Row },
                new PulseOffice.Sheets.Pivot.PivotField { SourceName = "totale", SourceColumnIndex = 1, Area = PulseOffice.Sheets.Pivot.PivotArea.Value },
            ],
        });

        foreach (var read in new[] { ThroughOds(workbook), ThroughXlsx(workbook) })
            Assert.Equal(source, Assert.Single(read.Pivots).ExternalSource);
    }
}
