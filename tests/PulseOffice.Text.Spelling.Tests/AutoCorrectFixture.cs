using PulseOffice.Text.Spelling;

namespace PulseOffice.Text.Spelling.Tests;

internal sealed class AutoCorrectFixture
{
    public AutoCorrectFixture()
    {
        var reader = new AutoCorrectJsonReader(new AutoCorrectEntryNormalizer());
        Defaults = new AutoCorrectDefaults(
        [
            new BundledAutoCorrectSource(Path.Combine(AppContext.BaseDirectory, "autocorrect"), reader, EntryOrigin.Bundled, 0),
            new BundledAutoCorrectSource(Path.Combine(AppContext.BaseDirectory, "autocorrect-pulseoffice"), reader, EntryOrigin.BuiltIn, 10),
        ]);
        Table = new AutoCorrectTable(Defaults);
    }

    public AutoCorrectDefaults Defaults { get; }

    public AutoCorrectTable Table { get; }
}
