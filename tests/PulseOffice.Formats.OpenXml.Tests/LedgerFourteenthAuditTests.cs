using PulseOffice.Document.Forms;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class LedgerFourteenthAuditTests
{
    private static Workbook Book()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        workbook.AddSheet("Altro foglio");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "forma",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "forma", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse },
            Hyperlink = "https://example.org/",
        });
        sheet.Objects.Add(new PlacedObject
        {
            Id = "casella",
            Anchor = new CellAddress(5, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(1)),
            Content = new FormControlObject
            {
                Id = "casella",
                ControlKind = FormControlKind.CheckBox,
                Properties = new FormControlProperties { LinkedCell = "'Altro foglio'!B2", SourceRange = "A1:A3" },
            },
        });
        return workbook;
    }

    private static Workbook ThroughXlsx(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    private static Workbook ThroughOds(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!;
    }

    public static TheoryData<string> Formats => ["xlsx", "ods"];

    private static Workbook Through(string format, Workbook workbook) => format == "xlsx" ? ThroughXlsx(workbook) : ThroughOds(workbook);

    [Theory]
    [MemberData(nameof(Formats))]
    public void Il_collegamento_di_una_forma_fa_il_giro(string format)
    {
        var back = Through(format, Book());
        Assert.Equal("https://example.org/", back.Sheets[0].Objects.Single(placed => placed.Id == "forma").Hyperlink);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void La_cella_collegata_di_un_altro_foglio_conserva_il_foglio(string format)
    {
        var back = Through(format, Book());
        var control = back.Sheets[0].Objects.Select(placed => placed.Content).OfType<FormControlObject>().Single();
        Assert.Equal("'Altro foglio'!B2", control.Properties.LinkedCell);
        Assert.Equal("A1:A3", control.Properties.SourceRange);
    }

    [Fact]
    public void Il_controllo_scrive_nella_cella_dell_altro_foglio()
    {
        var workbook = Book();
        var control = new FormControlProperties { LinkedCell = "'Altro foglio'!B2", Value = new FormControlValue { Checked = CheckState.Checked } };
        var write = new FormControlBinding(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant()).Publish(FormControlKind.CheckBox, control, workbook);
        Assert.Same(workbook.FindSheet("Altro foglio"), write!.Sheet);
        Assert.Equal(new CellAddress(1, 1), write.Address);
        Assert.Null(new FormControlBinding(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant()).Publish(FormControlKind.CheckBox, control));
    }

    [Fact]
    public void I_riferimenti_si_dividono_e_si_ricompongono()
    {
        var references = new SheetReferenceText();
        Assert.Equal(new SheetQualifiedReference("It's", "A1"), references.Split("'It''s'!$A$1"));
        Assert.Equal("'It''s'!A1", references.Join("It's", "A1"));
        Assert.Equal("A1", references.Qualify("Dati", "dati", "A1"));
        Assert.Equal("Altro!A1", references.Qualify("Altro", "Dati", "A1"));
    }
}
