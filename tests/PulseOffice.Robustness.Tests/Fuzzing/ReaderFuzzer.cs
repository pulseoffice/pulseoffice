using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;

namespace PulseOffice.Robustness.Tests.Fuzzing;

public sealed record FuzzFailure(string Seed, string Mutation, int Iteration, string Problem);

public sealed class ReaderFuzzer(DocumentReaders readers, ITextCatalog texts, TimeSpan timeout)
{
    public IReadOnlyList<FuzzFailure> Run(IReadOnlyList<Seed> seeds, IReadOnlyList<IFileMutation> mutations, int iterations, int baseSeed)
    {
        var failures = new List<FuzzFailure>();
        foreach (var seed in seeds)
        foreach (var mutation in mutations)
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var mutated = Mutate(seed, mutation, iteration, baseSeed);
            if (Check(seed, mutated) is { } problem)
                failures.Add(new FuzzFailure(seed.Name, mutation.Name, iteration, problem));
        }

        return failures;
    }

    public byte[] Mutate(Seed seed, IFileMutation mutation, int iteration, int baseSeed) =>
        mutation.Apply(seed.Content, new Random(unchecked(((baseSeed * 31 + StableHash(seed.Name)) * 31 + StableHash(mutation.Name)) * 31 + iteration)));

    public string? Check(Seed seed, byte[] content)
    {
        IReadOnlyList<Diagnostic>? diagnostics = null;
        Exception? escaped = null;
        var worker = new Thread(() =>
        {
            try
            {
                using var stream = new MemoryStream(content, writable: false);
                diagnostics = readers.Read(seed.Family, "fuzzed" + Path.GetExtension(seed.Name), stream);
            }
            catch (Exception exception)
            {
                escaped = exception;
            }
        }, 1024 * 1024) { IsBackground = true };
        worker.Start();

        if (!worker.Join(timeout)) return $"blocked for more than {timeout.TotalSeconds:0} s";
        if (escaped is not null) return $"unhandled {escaped.GetType().Name}: {escaped.Message} @ {escaped.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}";

        if (diagnostics!.FirstOrDefault(diagnostic => diagnostic.Code == "FMT010") is { } leaked) return $"reader leaked an exception: {leaked.Message}";

        var untranslated = diagnostics!.Where(diagnostic => diagnostic.Severity == Severity.Error && !texts.Contains(diagnostic.MessageKey)).ToList();
        return untranslated.Count > 0 ? $"untranslated error {untranslated[0].Code}: {untranslated[0].Message}" : null;
    }

    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var character in text) hash = unchecked(hash * 31 + character);
        return hash;
    }
}
