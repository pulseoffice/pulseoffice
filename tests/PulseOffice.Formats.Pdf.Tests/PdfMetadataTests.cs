using System.Text;
using System.Xml.Linq;
using Xunit;

namespace PulseOffice.Formats.Pdf.Tests;

public sealed class PdfMetadataTests
{
    [Fact]
    public void I_caratteri_di_controllo_del_titolo_non_rompono_i_metadati_XMP()
    {
        using var stream = new MemoryStream();
        var file = new PdfFile(stream);
        var start = stream.Length;

        new PdfArchiveParts(new SrgbProfile()).Metadata(file, new PdfExportOptions("Bilancio\u000B2026\u0001", "Rossi\u0000", "Nota"), DateTimeOffset.UnixEpoch);

        var text = Encoding.UTF8.GetString(stream.ToArray(), (int)start, (int)(stream.Length - start));
        var from = text.IndexOf("<x:xmpmeta", StringComparison.Ordinal);
        var to = text.IndexOf("</x:xmpmeta>", StringComparison.Ordinal) + "</x:xmpmeta>".Length;
        var xmp = XDocument.Parse(text[from..to]);

        Assert.Contains("Bilancio 2026", xmp.ToString(), StringComparison.Ordinal);
    }
}
