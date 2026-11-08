using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxAlignmentTests
{
    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        return result.Value!;
    }

    [Fact]
    public void GliEffettiDiCarattereSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Prova"));

        var style = CellStyle.Default with
        {
            Font = CellFont.Default with
            {
                DoubleUnderline = true,
                Underline = true,
                Shadow = true,
                Outline = true,
                Script = CellScript.Superscript,
            },
        };

        sheet.SetStyle(new CellAddress(0, 0), workbook.Styles.Intern(style));

        var read = RoundTrip(workbook);
        var font = read.Styles.Get(read.Sheets[0][new CellAddress(0, 0)].StyleId).Font;

        Assert.True(font.DoubleUnderline);
        Assert.True(font.Shadow);
        Assert.True(font.Outline);
        Assert.Equal(CellScript.Superscript, font.Script);
    }

    [Fact]
    public void IlVersoDelFoglioSopravvive()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].RightToLeft = true;

        Assert.True(RoundTrip(workbook).Sheets[0].RightToLeft);
    }

    [Fact]
    public void GliScenariSopravvivono()
    {
        var workbook = Workbook.CreateDefault();

        workbook.Sheets[0].Scenarios.Add(new Scenario
        {
            Name = "Ottimista",
            Comment = "Le vendite crescono",
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(1, 0)),
            Values = new Dictionary<CellAddress, CellValue>
            {
                [new CellAddress(0, 0)] = CellValue.Number(120),
                [new CellAddress(1, 0)] = CellValue.Text("alto"),
            },
        });

        var scenario = Assert.Single(RoundTrip(workbook).Sheets[0].Scenarios);

        Assert.Equal("Ottimista", scenario.Name);
        Assert.Equal(120, scenario.Values[new CellAddress(0, 0)].AsNumber());
        Assert.Equal("alto", scenario.Values[new CellAddress(1, 0)].AsText());
    }

    [Fact]
    public void LeSparklineSopravvivono()
    {
        var workbook = Workbook.CreateDefault();

        workbook.Sheets[0].Sparklines.Add(new SparklineGroup
        {
            Id = "g1",
            Kind = SparklineKind.Column,
            SameScale = true,
            Sparklines =
            [
                new Sparkline(
                    new CellAddress(0, 4),
                    new CellRange(new CellAddress(0, 0), new CellAddress(0, 3))),
            ],
        });

        var read = RoundTrip(workbook);
        var group = Assert.Single(read.Sheets[0].Sparklines.Groups);

        Assert.Equal(SparklineKind.Column, group.Kind);
        Assert.True(group.SameScale);
        Assert.Equal(new CellAddress(0, 4), group.Sparklines[0].Cell);
    }

    [Fact]
    public void IlGruppoDiSparklineEIlSuoStileSopravvivono()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Name = "Dati 2024";
        var style = new SparklineStyle
        {
            ColorHex = "#112233", NegativeHex = "#445566", MarkerHex = "#778899",
            ShowLowPoint = true, ShowFirstPoint = true, ShowNegative = true, LineWidth = 2.25,
        };
        workbook.Sheets[0].Sparklines.Add(new SparklineGroup
        {
            Id = "g1",
            Style = style,
            Sparklines =
            [
                new Sparkline(new CellAddress(0, 4), new CellRange(new CellAddress(0, 0), new CellAddress(0, 3))),
                new Sparkline(new CellAddress(1, 4), new CellRange(new CellAddress(1, 0), new CellAddress(1, 3))),
            ],
        });

        var group = Assert.Single(RoundTrip(workbook).Sheets[0].Sparklines.Groups);

        Assert.Equal(2, group.Sparklines.Count);
        Assert.Equal(style, group.Style);
    }

    [Fact]
    public void IControlliDiModuloSopravvivono()
    {
        var workbook = Workbook.CreateDefault();

        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "c1",
            Anchor = new CellAddress(2, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject
            {
                Id = "c1",
                ControlKind = FormControlKind.CheckBox,
                Properties = new FormControlProperties
                {
                    Name = "Accetto",
                    Label = "Accetto le condizioni",
                    LinkedCell = "D3",
                    Value = new FormControlValue { Checked = CheckState.Checked },
                },
            },
        });

        var read = RoundTrip(workbook);

        var placed = Assert.Single(
            read.Sheets[0].Objects, item => item.Content is FormControlObject);

        var control = (FormControlObject)placed.Content;

        Assert.Equal(FormControlKind.CheckBox, control.ControlKind);
        Assert.Equal("Accetto", control.Properties.Name);
        Assert.Equal("D3", control.Properties.LinkedCell);
        Assert.Equal(CheckState.Checked, control.Properties.Value.Checked);
        Assert.Equal(new CellAddress(2, 1), placed.Anchor);
    }

    [Fact]
    public void IlTracciatoLiberoDiventaGeometria()
    {
        var workbook = Workbook.CreateDefault();

        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "p1",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
            Content = new FreeformObject
            {
                Id = "p1",
                Path = new FreeformPath([
                    new PathFigure([
                        new PathNode(new ShapePoint(0, 0)),
                        new PathNode(new ShapePoint(1, 1)),
                    ]),
                ]),
            },
        });

        var read = RoundTrip(workbook);

        Assert.NotEmpty(read.Sheets[0].Objects);
    }

    [Fact]
    public void LePerditeVengonoDichiarate()
    {
        var workbook = Workbook.CreateDefault();

        workbook.Macros.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "x"));
        workbook.DefaultLanguage = "it-IT";
        workbook.Properties.Language = "en-US";

        var report = new XlsxSpreadsheetWriter().CheckFidelity(workbook);

        Assert.Contains(report.Losses, loss => loss.Code == "XLSX011");
        Assert.Contains(report.Losses, loss => loss.Code == "XLSX013");
    }

    [Theory]
    [InlineData("it-IT")]
    [InlineData("en-US")]
    public void TheWorkbookLanguageCrossesXlsxAsTheDocumentLanguage(string language)
    {
        var workbook = Workbook.CreateDefault();
        workbook.DefaultLanguage = language;

        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX013");
        Assert.Equal(language, RoundTrip(workbook).TextLanguage);
    }
}
