using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsSignatureLineRoundTripTests
{
    [Fact]
    public void LaRigaDellaFirmaAttraversaLOds()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Contratto").Objects.Add(new PlacedObject
        {
            Id = "firma",
            Content = new SignatureLineObject { Id = "firma", SignerName = "Ada", SignerTitle = "Direttrice", Instructions = "Firmare", ShowDate = true, AllowComments = false },
            Anchor = new CellAddress(4, 2),
            Size = SignatureLineObject.StandardSize,
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        var placed = Assert.Single(result.Value!.Sheets[0].Objects);
        var line = Assert.IsType<SignatureLineObject>(placed.Content);
        Assert.Equal("Ada", line.SignerName);
        Assert.Equal("Direttrice", line.SignerTitle);
        Assert.Equal("Firmare", line.Instructions);
        Assert.True(line.ShowDate);
        Assert.False(line.AllowComments);
        Assert.Equal(new CellAddress(4, 2), placed.Anchor);
        Assert.Equal(7, placed.Size.Width.ToCentimeters(), 1);
    }
}
