using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using X14 = DocumentFormat.OpenXml.Office2010.Excel;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxStandardFormControlTests
{
    private static Workbook Sample()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var row = 1;

        void Add(string id, FormControlKind kind, FormControlProperties properties)
        {
            sheet.Objects.Add(new PlacedObject
            {
                Id = id,
                Anchor = new CellAddress(row, 4),
                Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
                ZOrder = row,
                Content = new FormControlObject { Id = id, ControlKind = kind, Properties = properties },
            });
            row += 3;
        }

        Add("Spunta", FormControlKind.CheckBox, new FormControlProperties { Name = "Spunta", Label = "Accetto", LinkedCell = "A1", Value = new FormControlValue { Checked = CheckState.Checked } });
        Add("Opzione", FormControlKind.OptionButton, new FormControlProperties { Name = "Opzione", Label = "Prima scelta", LinkedCell = "A2" });
        Add("Tendina", FormControlKind.ComboBox, new FormControlProperties { Name = "Tendina", LinkedCell = "A3", SourceRange = "C1:C5", DropdownLines = 5, Value = new FormControlValue { SelectedIndices = [2] } });
        Add("Elenco", FormControlKind.ListBox, new FormControlProperties { Name = "Elenco", LinkedCell = "A4", SourceRange = "C1:C5", MultiSelect = true, Value = new FormControlValue { SelectedIndices = [1] } });
        Add("Barra", FormControlKind.ScrollBar, new FormControlProperties { Name = "Barra", LinkedCell = "A5", Range = new NumericRange(0, 100, 2, 10), Orientation = FormOrientation.Horizontal, Value = new FormControlValue { Number = 40 } });
        Add("Contatore", FormControlKind.SpinButton, new FormControlProperties { Name = "Contatore", LinkedCell = "A6", Range = new NumericRange(1, 9, 1, 1), Orientation = FormOrientation.Vertical, Value = new FormControlValue { Number = 3 } });
        Add("Pulsante", FormControlKind.PushButton, new FormControlProperties { Name = "Pulsante", Label = "Calcola" });
        Add("Etichetta", FormControlKind.Label, new FormControlProperties { Name = "Etichetta", Label = "Totale" });
        Add("Gruppo", FormControlKind.GroupBox, new FormControlProperties { Name = "Gruppo", Label = "Scelte" });
        return workbook;
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
                to.Write(Encoding.UTF8.GetBytes(change(reader.ReadToEnd())));
            }
        return output.ToArray();
    }

    private static FormControlObject Control(Workbook workbook, string name) =>
        workbook.Sheets[0].Objects.Select(item => item.Content).OfType<FormControlObject>().Single(control => control.Properties.Name == name);

    private static void AssertSample(Workbook back)
    {
        Assert.Equal(9, back.Sheets[0].Objects.Count(item => item.Content is FormControlObject));

        var check = Control(back, "Spunta");
        Assert.Equal(FormControlKind.CheckBox, check.ControlKind);
        Assert.Equal("Accetto", check.Properties.Label);
        Assert.Equal("A1", check.Properties.LinkedCell);
        Assert.Equal(CheckState.Checked, check.Properties.Value.Checked);

        Assert.Equal(FormControlKind.OptionButton, Control(back, "Opzione").ControlKind);
        Assert.Equal("Prima scelta", Control(back, "Opzione").Properties.Label);

        var combo = Control(back, "Tendina");
        Assert.Equal(FormControlKind.ComboBox, combo.ControlKind);
        Assert.Equal("C1:C5", combo.Properties.SourceRange);
        Assert.Equal("A3", combo.Properties.LinkedCell);
        Assert.Equal(5, combo.Properties.DropdownLines);
        Assert.Equal([2], combo.Properties.Value.SelectedIndices);

        var list = Control(back, "Elenco");
        Assert.Equal(FormControlKind.ListBox, list.ControlKind);
        Assert.True(list.Properties.MultiSelect);
        Assert.Equal([1], list.Properties.Value.SelectedIndices);

        var scroll = Control(back, "Barra");
        Assert.Equal(FormControlKind.ScrollBar, scroll.ControlKind);
        Assert.Equal(new NumericRange(0, 100, 2, 10), scroll.Properties.Range);
        Assert.Equal(40, scroll.Properties.Value.Number);
        Assert.Equal(FormOrientation.Horizontal, scroll.Properties.Orientation);

        var spin = Control(back, "Contatore");
        Assert.Equal(FormControlKind.SpinButton, spin.ControlKind);
        Assert.Equal(3, spin.Properties.Value.Number);
        Assert.Equal(FormOrientation.Vertical, spin.Properties.Orientation);

        Assert.Equal("Calcola", Control(back, "Pulsante").Properties.Label);
        Assert.Equal(FormControlKind.Label, Control(back, "Etichetta").ControlKind);
        Assert.Equal("Scelte", Control(back, "Gruppo").Properties.Label);
        Assert.Equal(FormControlKind.GroupBox, Control(back, "Gruppo").ControlKind);
    }

    [Fact]
    public void INoveControlliTornanoConLeLoroProprieta()
    {
        var workbook = Sample();
        var back = Read(Write(workbook));

        AssertSample(back);
        var placed = back.Sheets[0].Objects.Single(item => item.Id == "Barra");
        var original = workbook.Sheets[0].Objects.Single(item => item.Id == "Barra");
        Assert.Equal(original.Anchor, placed.Anchor);
    }

    [Fact]
    public void IControlliSiScrivonoNellaFormaDiExcel()
    {
        var package = Write(Sample());
        var sheet = Entry(package, "xl/worksheets/sheet1.xml");

        Assert.Contains("Requires=\"x14\"", sheet, StringComparison.Ordinal);
        Assert.Contains("controlPr", sheet, StringComparison.Ordinal);
        Assert.Contains("legacyDrawing", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("pulsecontrol:", sheet, StringComparison.Ordinal);
        Assert.True(sheet.IndexOf("legacyDrawing", StringComparison.Ordinal) < sheet.IndexOf("controls", StringComparison.Ordinal));

        using var zip = new ZipArchive(new MemoryStream(package));
        var vml = zip.Entries.Single(entry => entry.FullName.EndsWith(".vml", StringComparison.Ordinal));
        using var reader = new StreamReader(vml.Open());
        var drawing = reader.ReadToEnd();
        foreach (var type in new[] { "Checkbox", "Radio", "Drop", "List", "Scroll", "Spin", "Button", "Label", "GBox" })
            Assert.Contains($"ObjectType=\"{type}\"", drawing, StringComparison.Ordinal);
        Assert.Contains("<x:FmlaRange>C1:C5</x:FmlaRange>", drawing, StringComparison.Ordinal);
        Assert.Equal(9, zip.Entries.Count(entry => entry.FullName.StartsWith("xl/ctrlProps/", StringComparison.Ordinal)));

        using var document = SpreadsheetDocument.Open(new MemoryStream(package), false);
        var part = document.WorkbookPart!.WorksheetParts.Single();
        var shapeIds = part.Worksheet!.Descendants<Spreadsheet.Control>().Select(control => control.ShapeId!.Value).ToList();
        foreach (var id in shapeIds) Assert.Contains($"_x0000_s{id}\"", drawing, StringComparison.Ordinal);
    }

    [Fact]
    public void SoloIlDisegnoVmlBastaARileggereIControlli()
    {
        var package = Rewrite(Write(Sample()), "xl/worksheets/sheet1.xml", xml =>
            Regex.Replace(xml, "<mc:AlternateContent[^>]*><mc:Choice Requires=\"x14\"><(x:)?controls>.*</(x:)?controls></mc:Choice></mc:AlternateContent>", string.Empty, RegexOptions.Singleline));

        Assert.DoesNotContain("controlPr", Entry(package, "xl/worksheets/sheet1.xml"), StringComparison.Ordinal);
        var back = Read(package);
        var controls = back.Sheets[0].Objects.Select(item => item.Content).OfType<FormControlObject>().ToList();

        Assert.Equal(9, controls.Count);
        var combo = controls.Single(control => control.ControlKind == FormControlKind.ComboBox);
        Assert.Equal("C1:C5", combo.Properties.SourceRange);
        Assert.Equal("A3", combo.Properties.LinkedCell);
        Assert.Equal([2], combo.Properties.Value.SelectedIndices);
        var check = controls.Single(control => control.ControlKind == FormControlKind.CheckBox);
        Assert.Equal("Accetto", check.Properties.Label);
        Assert.Equal(CheckState.Checked, check.Properties.Value.Checked);
    }

    [Fact]
    public void LaFormaVecchiaConIlNomeCodificatoSiRilegge()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook.CreateDefault(), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart!.WorksheetParts.Single();
            var properties = part.AddNewPart<ControlPropertiesPart>();
            properties.FormControlProperties = new X14.FormControlProperties { ObjectType = X14.ObjectTypeValues.CheckBox, FmlaLink = "$B$2", Checked = X14.CheckedValues.Checked };
            part.Worksheet!.AppendChild(new Spreadsheet.Controls(new Spreadsheet.Control
            {
                ShapeId = 1,
                Id = part.GetIdOfPart(properties),
                Name = "pulsecontrol:CheckBox|vecchio|3|2|2268|567|Vecchio|Etichetta%7Cvecchia|0|0",
            }));
            part.Worksheet!.Save();
        }

        var back = Read(stream.ToArray());
        var placed = back.Sheets[0].Objects.Single();
        var control = Assert.IsType<FormControlObject>(placed.Content);

        Assert.Equal("vecchio", placed.Id);
        Assert.Equal(new CellAddress(3, 2), placed.Anchor);
        Assert.Equal("Vecchio", control.Properties.Name);
        Assert.Equal("Etichetta|vecchia", control.Properties.Label);
        Assert.Equal("B2", control.Properties.LinkedCell);
        Assert.Equal(CheckState.Checked, control.Properties.Value.Checked);
    }

    [Fact]
    public void IControlliStandardNonDichiaranoPerdite()
    {
        var report = new XlsxSpreadsheetWriter().CheckFidelity(Sample());
        Assert.DoesNotContain(report.Losses, loss => loss.Code == "XLSX020");
    }

    [Fact]
    public void UnCampoDiTestoOUnColoreSiDichiarano()
    {
        var workbook = Sample();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "colorato",
            Anchor = new CellAddress(30, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject { Id = "colorato", ControlKind = FormControlKind.CheckBox, Properties = new FormControlProperties { Name = "colorato", BackgroundHex = "#FF0000" } },
        });

        var loss = Assert.Single(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, item => item.Code == "XLSX020");
        Assert.Contains("1 form controls", loss.Message, StringComparison.Ordinal);
    }
}
