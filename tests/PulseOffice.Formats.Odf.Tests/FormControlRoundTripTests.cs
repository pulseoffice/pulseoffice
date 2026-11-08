using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class FormControlRoundTripTests
{
    private static Workbook WithControls()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.Objects.Add(Placed("c1", FormControlKind.CheckBox, new FormControlProperties
        {
            Name = "Accetto",
            Label = "Accetto le condizioni",
            LinkedCell = "C2",
            Value = new FormControlValue { Checked = CheckState.Checked },
        }, new CellAddress(1, 1)));

        sheet.Objects.Add(Placed("c2", FormControlKind.ListBox, new FormControlProperties
        {
            Name = "Regione",
            SourceRange = "E1:E3",
            Items = ["Nord", "Sud"],
            TabIndex = 3,
        }, new CellAddress(4, 0)));

        sheet.Objects.Add(Placed("c3", FormControlKind.ScrollBar, new FormControlProperties
        {
            Name = "Quantita",
            Range = new NumericRange(0, 250, 5, 25),
            Value = new FormControlValue { Number = 125 },
            Enabled = false,
        }, new CellAddress(8, 2)));

        return workbook;
    }

    private static PlacedObject Placed(
        string id, FormControlKind kind, FormControlProperties properties, CellAddress anchor) => new()
    {
        Id = id,
        Anchor = anchor,
        Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
        Content = new FormControlObject { Id = id, ControlKind = kind, Properties = properties },
    };

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        return result.Value!;
    }

    private static FormControlObject Find(Workbook workbook, string name) =>
        workbook.Sheets[0].Objects
            .Select(placed => placed.Content)
            .OfType<FormControlObject>()
            .Single(control => control.Properties.Name == name);

    [Fact]
    public void IControlliSopravvivonoAlGiro()
    {
        var read = RoundTrip(WithControls());

        Assert.Equal(3, read.Sheets[0].Objects.Count(
            placed => placed.Content is FormControlObject));
    }

    [Fact]
    public void LaSpuntaConservaStatoCellaCollegataEEtichetta()
    {
        var control = Find(RoundTrip(WithControls()), "Accetto");

        Assert.Equal(FormControlKind.CheckBox, control.ControlKind);
        Assert.Equal("Accetto le condizioni", control.Properties.Label);
        Assert.Equal("C2", control.Properties.LinkedCell);
        Assert.Equal(CheckState.Checked, control.Properties.Value.Checked);
    }

    [Fact]
    public void LElencoConservaVociIntervalloEOrdineDiTabulazione()
    {
        var control = Find(RoundTrip(WithControls()), "Regione");

        Assert.Equal(FormControlKind.ListBox, control.ControlKind);
        Assert.Equal(["Nord", "Sud"], control.Properties.Items);
        Assert.Equal("E1:E3", control.Properties.SourceRange);
        Assert.Equal(3, control.Properties.TabIndex);
    }

    [Fact]
    public void LaBarraConservaIntervalloValoreEStatoSpento()
    {
        var control = Find(RoundTrip(WithControls()), "Quantita");

        Assert.Equal(FormControlKind.ScrollBar, control.ControlKind);
        Assert.Equal(250, control.Properties.Range!.Maximum);
        Assert.Equal(5, control.Properties.Range.Step);
        Assert.Equal(125, control.Properties.Value.Number);
        Assert.False(control.Properties.Enabled);
    }

    [Fact]
    public void LAncoraggioELaMisuraTornanoDovErano()
    {
        var read = RoundTrip(WithControls());

        var placed = read.Sheets[0].Objects.Single(
            item => item.Content is FormControlObject { Properties.Name: "Quantita" });

        Assert.Equal(new CellAddress(8, 2), placed.Anchor);
        Assert.Equal(4, placed.Size.Width.ToCentimeters(), 2);
    }
}

public sealed class FormControlRichRoundTripTests
{
    private static Workbook WithControl(FormControlKind kind, FormControlProperties properties)
    {
        var workbook = Workbook.CreateDefault();

        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "c1",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject { Id = "c1", ControlKind = kind, Properties = properties },
        });

        return workbook;
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        return result.Value!;
    }

    private static FormControlProperties Read(Workbook workbook) =>
        workbook.Sheets[0].Objects
            .Select(placed => placed.Content)
            .OfType<FormControlObject>()
            .Single()
            .Properties;

    [Fact]
    public void LAspettoDelTestoSopravvive()
    {
        var read = Read(RoundTrip(WithControl(FormControlKind.TextBox, new FormControlProperties
        {
            Name = "Nota",
            Text = new FormTextStyle
            {
                Family = "Georgia",
                Size = 14,
                Bold = true,
                Italic = true,
                Underline = true,
                ColorHex = "#B91C1C",
                Alignment = FormTextAlignment.Right,
            },
        })));

        Assert.Equal("Georgia", read.Text.Family);
        Assert.Equal(14, read.Text.Size);
        Assert.True(read.Text.Bold);
        Assert.True(read.Text.Italic);
        Assert.True(read.Text.Underline);
        Assert.Equal("#B91C1C", read.Text.ColorHex);
        Assert.Equal(FormTextAlignment.Right, read.Text.Alignment);
    }

    [Fact]
    public void LeOpzioniDiUnCampoDiTestoSopravvivono()
    {
        var read = Read(RoundTrip(WithControl(FormControlKind.TextBox, new FormControlProperties
        {
            Name = "Password",
            MaxTextLength = 32,
            MultiLine = true,
            PasswordCharacter = '*',
            BackgroundHex = "#F1F5F9",
            Border = FormBorderKind.ThreeDimensional,
        })));

        Assert.Equal(32, read.MaxTextLength);
        Assert.True(read.MultiLine);
        Assert.Equal('*', read.PasswordCharacter);
        Assert.Equal("#F1F5F9", read.BackgroundHex);
        Assert.Equal(FormBorderKind.ThreeDimensional, read.Border);
    }

    [Fact]
    public void LeOpzioniDiUnPulsanteSopravvivono()
    {
        var read = Read(RoundTrip(WithControl(FormControlKind.PushButton, new FormControlProperties
        {
            Name = "Vai",
            ButtonAction = FormButtonAction.OpenUrl,
            TargetUrl = "https://example.org",
            Toggle = true,
        })));

        Assert.Equal(FormButtonAction.OpenUrl, read.ButtonAction);
        Assert.Equal("https://example.org", read.TargetUrl);
        Assert.True(read.Toggle);
    }

    [Fact]
    public void GliEventiRestanoAgganciatiAllaLoroMacro()
    {
        var properties = new FormControlProperties { Name = "Invia" }
            .Bind(FormControlEvent.Clicked, "Saluta")
            .Bind(FormControlEvent.FocusLost, "Controlla");

        var read = Read(RoundTrip(WithControl(FormControlKind.PushButton, properties)));

        Assert.Equal("Saluta", read.MacroFor(FormControlEvent.Clicked));
        Assert.Equal("Controlla", read.MacroFor(FormControlEvent.FocusLost));
    }

    [Fact]
    public void LeMacroDelDocumentoSopravvivono()
    {
        var workbook = WithControl(FormControlKind.PushButton, new FormControlProperties { Name = "Invia" });

        workbook.Macros.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "foglio.Write('A1', 'ciao')"));
        workbook.Macros.Store(new DocumentMacro("Conta", MacroLanguage.Python, "foglio.Write('A2', 42)"));

        var read = RoundTrip(workbook);

        Assert.Equal(2, read.Macros.All.Count);
        Assert.Equal("foglio.Write('A1', 'ciao')", read.Macros.Find("Saluta")!.Source);
        Assert.Equal(MacroLanguage.Python, read.Macros.Find("Conta")!.Language);
    }
}
