using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round192PrivateSpoolTests
{
    [Fact]
    public void The_spool_file_is_private_readable_and_gone_once_closed()
    {
        var root = Directory.CreateTempSubdirectory("pulse-spool-test-").FullName;
        try
        {
            using var temporary = new TemporaryFiles(root);
            var spool = new PrivateFileSpool(temporary, new PrivateFilesFactory());
            string path;

            using (var stream = (FileStream)spool.Create())
            {
                path = stream.Name;
                stream.Write([7, 8, 9]);
                stream.Position = 0;
                Assert.Equal(7, stream.ReadByte());
                if (!OperatingSystem.IsWindows())
                    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            }

            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
