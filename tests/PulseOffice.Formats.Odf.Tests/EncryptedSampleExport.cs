using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests;

public class EncryptedSampleExport
{
    public const string FolderVariable = "PULSEOFFICE_ENCRYPTED_SAMPLES";
    public const string Password = "Prova-PulseOffice-2026";
    public const string SheetMarker = "SegretoFoglioPulseOffice";
    public const string SlideMarker = "SegretoDiapositivaPulseOffice";

    private readonly OdfPackageEncryption _encryption = new();

    [Fact]
    public void ScriveICampioniCifratiPerLaVerificaEsterna()
    {
        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        var target = string.IsNullOrEmpty(folder) ? Path.Combine(Path.GetTempPath(), "pulseoffice-samples-" + Guid.NewGuid().ToString("N")) : folder;
        Directory.CreateDirectory(target);
        try
        {
            Save(Path.Combine(target, "encrypted.ods"), stream => new OdsSpreadsheetWriter().Write(Workbook(), stream));
            Save(Path.Combine(target, "encrypted.odp"), stream => new OdpWriter().Write(Deck(), stream));

            foreach (var name in new[] { "encrypted.ods", "encrypted.odp" })
            {
                using var stream = new MemoryStream(File.ReadAllBytes(Path.Combine(target, name)));
                Assert.Equal(OdfDecryptionStatus.Decrypted, _encryption.Decrypt(stream, new OdfPassword(Password)).Status);
            }
        }
        finally
        {
            if (string.IsNullOrEmpty(folder)) Directory.Delete(target, recursive: true);
        }
    }

    private void Save(string path, Action<Stream> write)
    {
        using var plain = new MemoryStream();
        write(plain);
        plain.Position = 0;
        using var encrypted = _encryption.Encrypt(plain, new PasswordProtection(Password));
        File.WriteAllBytes(path, encrypted.ToArray());
    }

    private Workbook Workbook()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Conti");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text(SheetMarker));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(42));
        return workbook;
    }

    private Presentation Deck()
    {
        var theme = new ThemeCatalog().Find("Slate")!;
        var master = new MasterFactory().CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide);
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.Replace(title with { Text = title.Text.WithPlainText(SlideMarker) });
        deck.AddExisting(slide);
        return deck;
    }
}
