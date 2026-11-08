using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Text.Spelling;
using PulseOffice.Text.Spelling.Hunspell;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public class DictionaryScanTests
{
    private sealed class TemporaryLocations(string root) : IDictionaryLocations
    {
        public IReadOnlyList<string> Folders => [root];

        public IReadOnlyList<string> SystemFolders => [];

        public string UserFolder => root;
    }

    [Fact]
    public void UnaCartellaIllegibileNonFermaLaRicerca()
    {
        if (OperatingSystem.IsWindows()) return;

        var root = Directory.CreateTempSubdirectory("dictionaries").FullName;
        var locked = Directory.CreateDirectory(Path.Combine(root, "a-locked")).FullName;
        File.WriteAllText(Path.Combine(root, "it_IT.aff"), "SET UTF-8\n");
        File.WriteAllText(Path.Combine(root, "it_IT.dic"), "1\ncasa\n");

        try
        {
            File.SetUnixFileMode(locked, UnixFileMode.None);

            var provider = new HunspellDictionaryProvider(new TemporaryLocations(root), NullLogger<HunspellDictionaryProvider>.Instance, new PulseOffice.Text.Spelling.Dictionaries.DictionaryChanges());

            Assert.Contains(provider.Available, tag => tag.Canonical.StartsWith("it", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, recursive: true);
        }
    }
}
