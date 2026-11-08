using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats;

public sealed record FidelityRule<TDocument>(Func<TDocument, bool> Applies, Diagnostic Loss);

public sealed class FidelityRuleSet<TDocument>(IEnumerable<FidelityRule<TDocument>> rules)
{
    private readonly IReadOnlyList<FidelityRule<TDocument>> _rules = [.. rules];

    public FidelityReport Check(TDocument document) =>
        new([.. _rules.Where(rule => rule.Applies(document)).Select(rule => rule.Loss)]);
}
