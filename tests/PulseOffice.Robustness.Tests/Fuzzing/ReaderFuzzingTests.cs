using PulseOffice.Localization;
using Xunit;

namespace PulseOffice.Robustness.Tests.Fuzzing;

public sealed class ReaderFuzzingTests : IDisposable
{
    private const string RoundsVariable = "PULSEOFFICE_FUZZ_ROUNDS";
    private const string SeedVariable = "PULSEOFFICE_FUZZ_SEED";
    private const int BaseSeed = 20261016;

    private readonly DocumentReaders _readers = new();
    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.Italian);

    public void Dispose() => _readers.Dispose();

    public static TheoryData<string> Mutations => [.. AllMutations().Select(mutation => mutation.Name)];

    [Theory]
    [MemberData(nameof(Mutations))]
    public void Mutated_files_open_or_fail_cleanly_without_crash_or_hang(string mutationName)
    {
        var rounds = int.TryParse(Environment.GetEnvironmentVariable(RoundsVariable), out var configured) && configured > 0 ? configured : 1;
        var mutation = AllMutations().Single(candidate => candidate.Name == mutationName);
        var seeds = new SeedCorpus().Load(perExtension: rounds > 1 ? 50 : 3);

        var baseSeed = int.TryParse(Environment.GetEnvironmentVariable(SeedVariable), out var chosen) ? chosen : BaseSeed;
        var failures = new ReaderFuzzer(_readers, _texts, TimeSpan.FromSeconds(30)).Run(seeds, [mutation], iterations: 2 * rounds, baseSeed: baseSeed);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(40).Select(failure => $"{failure.Seed} [{failure.Mutation} #{failure.Iteration}]: {failure.Problem}")));
    }

    [Theory]
    [InlineData("LargeAnchorOffset.ods", "out-of-range-numbers", 21, BaseSeed)]
    [InlineData("placeholder-priority.pptx", "byte-flip", 37, 7)]
    [InlineData("generated.docm", "out-of-range-numbers", 4, 99)]
    public void Cases_found_by_long_runs_stay_fixed(string seedName, string mutationName, int iteration, int baseSeed)
    {
        var seed = new SeedCorpus().Load(perExtension: 50).Single(candidate => candidate.Name.EndsWith(seedName, StringComparison.Ordinal));
        var fuzzer = new ReaderFuzzer(_readers, _texts, TimeSpan.FromSeconds(30));
        var mutated = fuzzer.Mutate(seed, AllMutations().Single(candidate => candidate.Name == mutationName), iteration, baseSeed);

        Assert.Null(fuzzer.Check(seed, mutated));
    }

    private static IReadOnlyList<IFileMutation> AllMutations() =>
    [
        new ByteFlip(), new Truncation(), new TailCorruption(), new EntryRemoval(), new MalformedXml(),
        new OutOfRangeNumbers(), new DuplicateIdentifiers(), new CircularReferences(), new DeepNesting(20_000), new DeepNesting(240),
    ];
}
