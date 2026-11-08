using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Forms;
using PulseOffice.Formats.Packaging;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfFormsAuditTests
{
    private readonly XNamespace _form = OdfPackage.Namespaces.Form;
    private readonly XNamespace _office = OdfPackage.Namespaces.Office;

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-1e300")]
    [InlineData("1e300")]
    public void Le_proprieta_numeriche_del_controllo_restano_nei_limiti(string value)
    {
        var properties = Reopened(FormControlKind.TextBox, new FormControlProperties { Name = "t" }, bag =>
        {
            foreach (var name in new[] { "FontHeight", "MaxTextLen", "DecimalAccuracy", "LineCount", "RepeatDelay", "Align", "Border", "ButtonType", "Orientation", "ImageScaleMode", "EchoChar" })
                Set(bag, name, value);
        });

        Assert.InRange(properties.Text.Size, 1, 4000);
        Assert.InRange(properties.MaxTextLength, 0, int.MaxValue);
        Assert.InRange(properties.DecimalPlaces, 0, 20);
        Assert.InRange(properties.DropdownLines, 1, 1000);
        Assert.InRange(properties.RepeatDelayMilliseconds, 0, 60_000);
        Assert.True(Enum.IsDefined(properties.Text.Alignment));
        Assert.True(Enum.IsDefined(properties.Border));
        Assert.True(Enum.IsDefined(properties.ButtonAction));
        Assert.True(Enum.IsDefined(properties.Orientation));
        Assert.True(Enum.IsDefined(properties.ImageScale));
        Assert.Null(properties.PasswordCharacter);
    }

    [Fact]
    public void Un_intervallo_rovesciato_non_fa_lanciare_il_controllo()
    {
        var properties = Reopened(FormControlKind.ScrollBar,
            new FormControlProperties { Name = "s", Range = new NumericRange(0, 10, 1, 1), Value = new FormControlValue { Number = 5 } },
            bag =>
            {
                Set(bag, "ScrollValueMin", "10");
                Set(bag, "ScrollValueMax", "0");
                Set(bag, "LineIncrement", "-3");
            });

        var range = properties.Range!;
        Assert.True(range.Minimum <= range.Maximum);
        Assert.True(range.Step > 0);
        Assert.Equal(5, range.Snap(5));
    }

    private void Set(XElement bag, string name, string value)
    {
        var property = bag.Elements(_form + "property").FirstOrDefault(element => (string?)element.Attribute(_form + "property-name") == name);
        if (property is null)
        {
            property = new XElement(_form + "property", new XAttribute(_form + "property-name", name), new XAttribute(_office + "value-type", "float"));
            bag.Add(property);
        }
        property.SetAttributeValue(_office + "value", value);
    }

    private FormControlProperties Reopened(FormControlKind kind, FormControlProperties properties, Action<XElement> change)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "c1",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(1)),
            Content = new FormControlObject { Id = "c1", ControlKind = kind, Properties = properties },
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            change(content.Descendants(_form + "properties").Single());
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            content.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        var control = Assert.IsType<FormControlObject>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
        return control.Properties;
    }
}
