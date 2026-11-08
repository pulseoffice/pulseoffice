using PulseOffice.Typography.OpenType;
using PulseOffice.Typography.OpenType.Metrics;
using Xunit;

namespace PulseOffice.Typography.Tests;

public sealed class FontWidthChoiceTests
{
    [Fact]
    public void FraUnaVersioneStrettaEUnaNormaleSiSceglieLaNormale()
    {
        var directory = Directory.CreateTempSubdirectory("pulse-fonts-");
        try
        {
            var builder = new KerningFont();
            var tables = new List<(string, byte[])> { ("hhea", builder.Hhea()), ("hmtx", builder.Hmtx()), ("cmap", builder.Cmap()) };
            File.WriteAllBytes(Path.Combine(directory.FullName, "a-condensed.ttf"), new SyntheticFont { Family = "Prova Larga", WidthClass = 3, ExtraTables = tables }.Build());
            File.WriteAllBytes(Path.Combine(directory.FullName, "b-normal.ttf"), new SyntheticFont { Family = "Prova Larga", ExtraTables = tables }.Build());

            var indexed = new IndexedFontLocator([directory.FullName], new OpenTypeReader(), new FontMetricsReader()).Find("Prova Larga", false, false);
            var scanned = new DirectoryFontLocator([directory.FullName]).Find("Prova Larga", false, false);

            Assert.Equal(5, indexed!.Face.Width);
            Assert.Equal(5, scanned!.Face.Width);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void UnaSottocartellaIlleggibileNonNascondeICaratteriDelleAltre()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = Directory.CreateTempSubdirectory("pulse-fonts-");
        var locked = Directory.CreateDirectory(Path.Combine(directory.FullName, "chiusa"));
        try
        {
            var builder = new KerningFont();
            var tables = new List<(string, byte[])> { ("hhea", builder.Hhea()), ("hmtx", builder.Hmtx()), ("cmap", builder.Cmap()) };
            File.WriteAllBytes(Path.Combine(directory.FullName, "font.ttf"), new SyntheticFont { Family = "Prova Aperta", ExtraTables = tables }.Build());
            File.SetUnixFileMode(locked.FullName, UnixFileMode.None);

            var indexed = new IndexedFontLocator([directory.FullName], new OpenTypeReader(), new FontMetricsReader()).Find("Prova Aperta", false, false);
            var scanned = new DirectoryFontLocator([directory.FullName]).Find("Prova Aperta", false, false);

            Assert.NotNull(indexed);
            Assert.NotNull(scanned);
        }
        finally
        {
            File.SetUnixFileMode(locked.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            directory.Delete(true);
        }
    }
}
