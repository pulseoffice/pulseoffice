using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsMetadataRoundTripTests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();

        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);

        return result.Value!;
    }

    private static CellRange Range(int firstRow, int firstColumn, int lastRow, int lastColumn) =>
        new(new CellAddress(firstRow, firstColumn), new CellAddress(lastRow, lastColumn));

    [Fact]
    public void UnCommentoSopravviveAlSalvataggio()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Text("Ricavi"));
        sheet.Comments[CellAddress.First] = new CellComment { Text = "Da confermare", Author = "Ada" };

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.True(reread.Comments.ContainsKey(CellAddress.First));
        Assert.Equal("Da confermare", reread.Comments[CellAddress.First].Text);
        Assert.Equal("Ada", reread.Comments[CellAddress.First].Author);
    }

    [Fact]
    public void LAreaDiStampaSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.PrintRanges.Add(Range(0, 0, 4, 2));

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.Single(reread.PrintRanges);
        Assert.Equal(Range(0, 0, 4, 2), reread.PrintRanges[0]);
    }

    [Fact]
    public void LeRigheDaRipetereInStampaSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Text("Titolo"));
        sheet.SetValue(new CellAddress(5, 0), CellValue.Number(42));
        sheet.RepeatedPrintRows = 2;

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.Equal(2, reread.RepeatedPrintRows);
        Assert.Equal("Titolo", reread.GetValue(CellAddress.First).AsText());
        Assert.Equal(42, reread.GetValue(new CellAddress(5, 0)).AsNumber());
    }

    [Fact]
    public void IlTipoDiAvvisoDellaConvalidaSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(5));
        sheet.Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 0, 0),
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Between,
            FirstFormula = "1",
            SecondFormula = "10",
            Alert = ValidationAlert.Warning,
            ErrorMessage = "Fuori intervallo",
        });

        var validation = Assert.Single(RoundTrip(workbook).Sheets[0].Validations);

        Assert.Equal(ValidationAlert.Warning, validation.Alert);
        Assert.Equal(ValidationOperator.Between, validation.Operator);
        Assert.Equal("10", validation.SecondFormula);
    }

    [Fact]
    public void UnaConvalidaAElencoSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Text("Nord"));
        sheet.Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 0, 0),
            Kind = ValidationKind.List,
            ListValues = ["Nord", "Sud"],
            ErrorMessage = "Solo Nord o Sud",
        });

        var reread = RoundTrip(workbook).Sheets[0];

        var validation = Assert.Single(reread.Validations);

        Assert.Equal(ValidationKind.List, validation.Kind);
        Assert.Equal(["Nord", "Sud"], validation.ListValues);
        Assert.Equal("Solo Nord o Sud", validation.ErrorMessage);
    }

    [Fact]
    public void UnaConvalidaNumericaSopravviveConIDueEstremi()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(5));
        sheet.Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 0, 0),
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Between,
            FirstFormula = "1",
            SecondFormula = "10",
            AllowBlank = false,
        });

        var validation = Assert.Single(RoundTrip(workbook).Sheets[0].Validations);

        Assert.Equal(ValidationKind.WholeNumber, validation.Kind);
        Assert.Equal(ValidationOperator.Between, validation.Operator);
        Assert.Equal("1", validation.FirstFormula);
        Assert.Equal("10", validation.SecondFormula);
        Assert.False(validation.AllowBlank);
    }

    [Fact]
    public void LeMisureDiRigheEColonneSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.Layout.SetColumnWidth(0, PulseOffice.Foundation.Units.Twip.FromCentimeters(4));
        sheet.Layout.SetRowHeight(0, PulseOffice.Foundation.Units.Twip.FromCentimeters(1));

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.Equal(4, reread.Layout.ColumnWidth(0).ToCentimeters(), 2);
        Assert.Equal(1, reread.Layout.RowHeight(0).ToCentimeters(), 2);
    }

    [Fact]
    public void UnRaggruppamentoDiRigheSopravviveConIDati()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        for (var row = 0; row < 5; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));

        sheet.RowGroups.Add(new OutlineGroup { First = 1, Last = 3, Level = 1 });

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.Equal(5, Enumerable.Range(0, 5)
            .Count(row => !reread.GetValue(new CellAddress(row, 0)).IsEmpty));

        var group = Assert.Single(reread.RowGroups);

        Assert.Equal(1, group.First);
        Assert.Equal(3, group.Last);
    }

    [Fact]
    public void UnGruppoChiusoTornaChiuso()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        for (var row = 0; row < 4; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));

        sheet.RowGroups.Add(new OutlineGroup { First = 1, Last = 2, Level = 1, IsCollapsed = true });

        Assert.True(Assert.Single(RoundTrip(workbook).Sheets[0].RowGroups).IsCollapsed);
    }

    [Fact]
    public void LeOpzioniDeiCampiPivotSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var source = workbook.Sheets[0];
        source.Name = "Dati vendite";
        source.SetValue(CellAddress.First, CellValue.Text("Regione"));
        source.SetValue(new CellAddress(0, 1), CellValue.Text("Importo"));
        source.SetValue(new CellAddress(0, 2), CellValue.Text("Mese"));
        workbook.AddSheet("Pivot 1");

        workbook.Pivots.Add(new PulseOffice.Sheets.Pivot.PivotDefinition
        {
            Id = "p1",
            Name = "Riepilogo",
            SourceSheet = source.Name,
            SourceRange = Range(0, 0, 5, 2),
            DestinationSheet = "Pivot 1",
            Destination = CellAddress.First,
            Layout = PulseOffice.Sheets.Pivot.PivotLayout.Tabular,
            ShowGrandTotalRows = false,
            Fields =
            [
                new PulseOffice.Sheets.Pivot.PivotField
                {
                    SourceName = "Mese", SourceColumnIndex = 2, Area = PulseOffice.Sheets.Pivot.PivotArea.Row, AreaOrder = 1,
                    DateGrouping = PulseOffice.Sheets.Pivot.DateGrouping.Months,
                },
                new PulseOffice.Sheets.Pivot.PivotField
                {
                    SourceName = "Regione", SourceColumnIndex = 0, Area = PulseOffice.Sheets.Pivot.PivotArea.Row, AreaOrder = 0,
                    ShowSubtotals = false, SortDescending = true, ExcludedValues = ["Sud"], DisplayName = "Zona",
                },
                new PulseOffice.Sheets.Pivot.PivotField
                {
                    SourceName = "Importo", SourceColumnIndex = 1, Area = PulseOffice.Sheets.Pivot.PivotArea.Value,
                    Aggregation = PulseOffice.Sheets.Pivot.PivotAggregation.Median,
                    DisplayMode = PulseOffice.Sheets.Pivot.ValueDisplayMode.PercentOfTotal,
                },
            ],
        });

        var pivot = Assert.Single(RoundTrip(workbook).Pivots);
        var rows = pivot.In(PulseOffice.Sheets.Pivot.PivotArea.Row).ToList();

        Assert.Equal("Dati vendite", pivot.SourceSheet);
        Assert.Equal("Pivot 1", pivot.DestinationSheet);
        Assert.Equal(PulseOffice.Sheets.Pivot.PivotLayout.Tabular, pivot.Layout);
        Assert.False(pivot.ShowGrandTotalRows);
        Assert.True(pivot.ShowGrandTotalColumns);
        Assert.Equal(["Regione", "Mese"], rows.Select(field => field.SourceName));
        Assert.False(rows[0].ShowSubtotals);
        Assert.True(rows[0].SortDescending);
        Assert.Equal("Zona", rows[0].DisplayName);
        Assert.Equal(["Sud"], rows[0].ExcludedValues);
        Assert.Equal(PulseOffice.Sheets.Pivot.DateGrouping.Months, rows[1].DateGrouping);
        var value = Assert.Single(pivot.In(PulseOffice.Sheets.Pivot.PivotArea.Value));
        Assert.Equal(PulseOffice.Sheets.Pivot.PivotAggregation.Median, value.Aggregation);
        Assert.Equal(PulseOffice.Sheets.Pivot.ValueDisplayMode.PercentOfTotal, value.DisplayMode);
    }

    [Fact]
    public void UnaTabellaPivotSopravviveConICampi()
    {
        var workbook = Workbook.CreateDefault();
        var source = workbook.Sheets[0];

        source.SetValue(CellAddress.First, CellValue.Text("Regione"));
        source.SetValue(new CellAddress(0, 1), CellValue.Text("Importo"));
        source.SetValue(new CellAddress(1, 0), CellValue.Text("Nord"));
        source.SetValue(new CellAddress(1, 1), CellValue.Number(10));

        workbook.AddSheet("Pivot1");

        workbook.Pivots.Add(new PulseOffice.Sheets.Pivot.PivotDefinition
        {
            Id = "p1",
            Name = "Riepilogo",
            SourceSheet = source.Name,
            SourceRange = Range(0, 0, 1, 1),
            DestinationSheet = "Pivot1",
            Destination = CellAddress.First,
            OutputRange = Range(0, 0, 2, 1),
            Fields =
            [
                new PulseOffice.Sheets.Pivot.PivotField
                {
                    SourceName = "Regione",
                    SourceColumnIndex = 0,
                    Area = PulseOffice.Sheets.Pivot.PivotArea.Row,
                },
                new PulseOffice.Sheets.Pivot.PivotField
                {
                    SourceName = "Importo",
                    SourceColumnIndex = 1,
                    Area = PulseOffice.Sheets.Pivot.PivotArea.Value,
                    Aggregation = PulseOffice.Sheets.Pivot.PivotAggregation.Average,
                },
            ],
        });

        var reread = RoundTrip(workbook);
        var pivot = Assert.Single(reread.Pivots);

        Assert.Equal("Riepilogo", pivot.Name);
        Assert.Equal("Pivot1", pivot.DestinationSheet);
        Assert.Equal(2, pivot.Fields.Count);

        var value = pivot.Fields.First(field => field.Area == PulseOffice.Sheets.Pivot.PivotArea.Value);

        Assert.Equal("Importo", value.SourceName);
        Assert.Equal(1, value.SourceColumnIndex);
        Assert.Equal(PulseOffice.Sheets.Pivot.PivotAggregation.Average, value.Aggregation);
    }

    [Fact]
    public void UnoStileNominatoSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));

        workbook.NamedStyles.Define("Evidenza", PulseOffice.Sheets.Styling.CellStyle.Default with
        {
            BackgroundHex = "#FFE08A",
        });

        var reread = RoundTrip(workbook);
        var style = reread.NamedStyles.Find("Evidenza");

        Assert.NotNull(style);
        Assert.Equal("#FFE08A", reread.Styles.Get(style.StyleId).BackgroundHex);
    }

    [Fact]
    public void GliStiliDiBaseNonSiDuplicano()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));

        var reread = RoundTrip(workbook);

        Assert.Equal(
            workbook.NamedStyles.All.Count,
            reread.NamedStyles.All.Select(style => style.Name).Distinct().Count());
    }

    [Fact]
    public void LoStileDiColonnaSiLeggeDaiFileAltrui()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(1));

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        var document = System.Xml.Linq.XDocument.Load(
            new System.IO.Compression.ZipArchive(new MemoryStream(stream.ToArray()))
                .GetEntry("content.xml")!.Open());

        var table = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:tc:opendocument:xmlns:table:1.0");

        var style = System.Xml.Linq.XNamespace.Get(
            "urn:oasis:names:tc:opendocument:xmlns:style:1.0");

        var fo = System.Xml.Linq.XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0");

        document.Descendants(table + "table-column").First().Add(
            new System.Xml.Linq.XAttribute(table + "default-cell-style-name", "coDefault"));

        document.Descendants(System.Xml.Linq.XName.Get("automatic-styles",
            "urn:oasis:names:tc:opendocument:xmlns:office:1.0")).First().Add(
            new System.Xml.Linq.XElement(style + "style",
                new System.Xml.Linq.XAttribute(style + "name", "coDefault"),
                new System.Xml.Linq.XAttribute(style + "family", "table-cell"),
                new System.Xml.Linq.XElement(style + "table-cell-properties",
                    new System.Xml.Linq.XAttribute(fo + "background-color", "#123456"))));

        using var rebuilt = new MemoryStream();

        using (var archive = new System.IO.Compression.ZipArchive(
            rebuilt, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        using (var source = new System.IO.Compression.ZipArchive(new MemoryStream(stream.ToArray())))
        {
            foreach (var entry in source.Entries)
            {
                var copy = archive.CreateEntry(entry.FullName);
                using var target = copy.Open();

                if (entry.FullName == "content.xml") document.Save(target);
                else entry.Open().CopyTo(target);
            }
        }

        rebuilt.Position = 0;

        var reread = new OdsReader().Read(rebuilt);
        Assert.True(reread.Succeeded);

        Assert.Equal(
            "#123456",
            reread.Value!.Styles.Get(reread.Value.Sheets[0][CellAddress.First].StyleId).BackgroundHex);
    }

    [Fact]
    public void UnFoglioSenzaAggiunteRestaPulito()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(1));

        var reread = RoundTrip(workbook).Sheets[0];

        Assert.Empty(reread.Comments);
        Assert.Empty(reread.Validations);
        Assert.Empty(reread.PrintRanges);
    }

    [Fact]
    public void TutteLeFormeDiConvalidaSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Name = "Dati 2024";
        CellRange Cells(int row) => new(new CellAddress(row, 0), new CellAddress(row, 1));
        DataValidation[] rules =
        [
            new() { Range = Cells(0), Kind = ValidationKind.Time, Operator = ValidationOperator.Greater, FirstFormula = "0.5" },
            new() { Range = Cells(1), Kind = ValidationKind.TextLength, Operator = ValidationOperator.Between, FirstFormula = "2", SecondFormula = "8" },
            new() { Range = Cells(2), Kind = ValidationKind.TextLength, Operator = ValidationOperator.LessOrEqual, FirstFormula = "5" },
            new() { Range = Cells(3), Kind = ValidationKind.List, FirstFormula = "$D$1:$D$4", ShowDropdown = false },
            new() { Range = Cells(4), Kind = ValidationKind.Custom, FirstFormula = "C5>SUM(D1;D2)" },
            new() { Range = Cells(5), Kind = ValidationKind.List, ListValues = ["Nord", "Sud \"est\""] },
            new()
            {
                Range = Cells(6), Kind = ValidationKind.WholeNumber, Operator = ValidationOperator.Between,
                FirstFormula = "D1", SecondFormula = "MAX(D1;D2)", Alert = ValidationAlert.Warning,
                ErrorTitle = "Attenzione", InputTitle = "Suggerimento", InputMessage = "Scrivi un intero",
            },
        ];
        foreach (var rule in rules) sheet.Validations.Add(rule);

        var read = RoundTrip(workbook).Sheets[0].Validations.OrderBy(rule => rule.Range.Start.Row).ToList();

        Assert.Equal(rules.Length, read.Count);
        for (var index = 0; index < rules.Length; index++)
        {
            Assert.Equal(rules[index].Kind, read[index].Kind);
            Assert.Equal(rules[index].Range, read[index].Range);
            Assert.Equal(rules[index].ListValues, read[index].ListValues);
            Assert.Equal(rules[index].ShowDropdown, read[index].ShowDropdown);
            Assert.Equal(rules[index].Alert, read[index].Alert);
            Assert.Equal(rules[index].ErrorTitle, read[index].ErrorTitle);
            Assert.Equal(rules[index].InputTitle, read[index].InputTitle);
            Assert.Equal(rules[index].InputMessage, read[index].InputMessage);
            Assert.Equal(rules[index].FirstFormula?.Replace("$", ""), read[index].FirstFormula?.Replace("$", ""));
            Assert.Equal(rules[index].SecondFormula, read[index].SecondFormula);
            if (rules[index].Kind != ValidationKind.List && rules[index].Kind != ValidationKind.Custom)
                Assert.Equal(rules[index].Operator, read[index].Operator);
        }
    }

    [Fact]
    public void INomiLocaliEICostantiDecimaliSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Name = "Dati 2024";
        workbook.AddSheet("Altro");
        workbook.DefineName(new DefinedName("Aliquota", "0.22"));
        workbook.DefineName(new DefinedName("Zona", "$A$1:$B$3", "Dati 2024"));
        workbook.DefineName(new DefinedName("Punto", "$C$1", "Altro"));
        workbook.DefineName(new DefinedName("Totale", "SUM($A$1:$A$3)*1.5"));

        var read = RoundTrip(workbook);
        var names = read.DefinedNames.ToDictionary(name => (name.Name, name.Scope));

        Assert.Equal("0.22", names[("Aliquota", null)].Expression);
        Assert.Contains("A$1:$B$3", names[("Zona", "Dati 2024")].Expression);
        Assert.Contains("$C$1", names[("Punto", "Altro")].Expression);
        Assert.Contains("SUM(", names[("Totale", null)].Expression);
    }

    [Fact]
    public void IlCommentoSempreVisibileRestaVisibileAncheSulSecondoFoglio()
    {
        var workbook = Workbook.CreateDefault();
        var second = workbook.AddSheet("Secondo");
        workbook.Sheets[0].Comments[new CellAddress(0, 0)] = new CellComment { Text = "Primo", Author = "Ada" };
        second.Comments[new CellAddress(2, 1)] = new CellComment { Text = "Fisso", Author = "Ada", IsAlwaysVisible = true };
        second.Comments[new CellAddress(3, 1)] = new CellComment { Text = "Nascosto" };

        var read = RoundTrip(workbook);

        Assert.False(read.Sheets[0].Comments[new CellAddress(0, 0)].IsAlwaysVisible);
        Assert.True(read.Sheets[1].Comments[new CellAddress(2, 1)].IsAlwaysVisible);
        Assert.False(read.Sheets[1].Comments[new CellAddress(3, 1)].IsAlwaysVisible);
    }

    [Fact]
    public void MessaggiSpentiSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(0, 0, 2, 0),
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Greater,
            FirstFormula = "0",
            InputTitle = "Quantità",
            InputMessage = "Un intero positivo",
            ShowInputMessage = false,
            ShowErrorAlert = false,
        });
        workbook.Sheets[0].Validations.Add(new DataValidation
        {
            Range = Range(4, 0, 4, 0),
            Kind = ValidationKind.Decimal,
            Operator = ValidationOperator.Less,
            FirstFormula = "10",
        });

        var read = RoundTrip(workbook).Sheets[0].Validations.OrderBy(rule => rule.Range.Start.Row).ToList();

        Assert.False(read[0].ShowInputMessage);
        Assert.False(read[0].ShowErrorAlert);
        Assert.Equal("Un intero positivo", read[0].InputMessage);
        Assert.True(read[1].ShowInputMessage);
        Assert.True(read[1].ShowErrorAlert);
    }
}
