using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class TemporaryFilesTests
{
    [Fact]
    public void AllUscitaLaCartellaDellaSessioneSparisce()
    {
        var root = Directory.CreateTempSubdirectory("qo-temp-test-").FullName;

        try
        {
            string folder;
            using (var files = new TemporaryFiles(root))
            {
                folder = files.NewFolder("print-");
                File.WriteAllText(Path.Combine(folder, "a.pdf"), "x");
            }

            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LeSessioniVecchieSiCancellanoAllAvvio()
    {
        var root = Directory.CreateTempSubdirectory("qo-temp-test-").FullName;

        try
        {
            var old = Directory.CreateDirectory(Path.Combine(root, "session-2147483647-vecchia")).FullName;
            Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));
            var recent = Directory.CreateDirectory(Path.Combine(root, "session-2-recente")).FullName;

            using var files = new TemporaryFiles(root);

            Assert.False(Directory.Exists(old));
            Assert.True(Directory.Exists(recent));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LaSessioneVecchiaDiUnIstanzaAncoraVivaNonSiCancella()
    {
        var root = Directory.CreateTempSubdirectory("qo-temp-test-").FullName;

        try
        {
            var alive = Directory.CreateDirectory(Path.Combine(root, $"session-{Environment.ProcessId}-lunga")).FullName;
            File.WriteAllText(Path.Combine(alive, "aperto.pdf"), "x");
            Directory.SetLastWriteTimeUtc(alive, DateTime.UtcNow.AddDays(-3));

            using var files = new TemporaryFiles(root);

            Assert.True(File.Exists(Path.Combine(alive, "aperto.pdf")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnaRadiceCheEUnCollegamentoNonOspitaIFileDellaSessione()
    {
        if (OperatingSystem.IsWindows()) return;
        var target = Directory.CreateTempSubdirectory("qo-temp-target-").FullName;
        var link = target + "-link";
        Directory.CreateSymbolicLink(link, target);

        try
        {
            using var files = new TemporaryFiles(link);
            var folder = files.NewFolder("print-");

            Assert.False(folder.StartsWith(link + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            Assert.Empty(Directory.EnumerateFileSystemEntries(target));
            Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(folder)!)!, recursive: true);
        }
        finally
        {
            Directory.Delete(link);
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void UnaRadiceApertaAGliAltriUtentiNonOspitaIFileDellaSessione()
    {
        if (OperatingSystem.IsWindows()) return;
        var shared = Directory.CreateTempSubdirectory("qo-temp-shared-").FullName;
        File.SetUnixFileMode(shared, (UnixFileMode)0b111_111_111);

        try
        {
            using var files = new TemporaryFiles(shared);
            var folder = files.NewFolder("print-");

            Assert.False(folder.StartsWith(shared + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(folder));
            Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(folder)!)!, recursive: true);
        }
        finally
        {
            Directory.Delete(shared, recursive: true);
        }
    }

    [Fact]
    public void UnaRadiceIlleggibileNonFaCadereLAvvio()
    {
        if (OperatingSystem.IsWindows()) return;
        var locked = Directory.CreateTempSubdirectory("qo-temp-locked-").FullName;
        File.SetUnixFileMode(locked, UnixFileMode.None);

        try
        {
            using var files = new TemporaryFiles(locked);
            var folder = files.NewFolder("print-");

            Assert.True(Directory.Exists(folder));
            Assert.False(folder.StartsWith(locked + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(locked, recursive: true);
            foreach (var fresh in Directory.EnumerateDirectories(Path.GetDirectoryName(locked)!, Path.GetFileName(locked) + "-*"))
                Directory.Delete(fresh, recursive: true);
        }
    }
}
