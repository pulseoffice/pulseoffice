using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.OpenXml.Tests.PropertyAudit;

public sealed record CodecAuditFinding(string Format, string Path, object? Expected, object? Actual);

public sealed class CodecPropertyAudit<TModel>(
    ModelPropertyProbe probe,
    Func<object, TModel> place,
    Func<TModel, object?> recover,
    Func<string, object, object>? prepare = null)
{
    public IReadOnlyList<CodecAuditFinding> Silent(
        object baseline,
        string format,
        Func<TModel, FidelityReport> fidelity,
        Func<TModel, TModel> roundTrip)
    {
        var known = Codes(fidelity(place(baseline)));
        var findings = new List<CodecAuditFinding>();

        foreach (var variant in probe.Variants(baseline))
        {
            var instance = prepare?.Invoke(variant.Path, variant.Instance) ?? variant.Instance;
            var model = place(instance);
            var expected = probe.Read(instance, variant.Path);
            var back = recover(roundTrip(model));
            var actual = back is null ? null : probe.Read(back, variant.Path);
            if (probe.Same(expected, actual)) continue;
            if (!Codes(fidelity(model)).IsSubsetOf(known)) continue;

            findings.Add(new CodecAuditFinding(format, variant.Path, expected, actual));
        }

        return findings;
    }

    private HashSet<string> Codes(FidelityReport report) => [.. report.Losses.Select(loss => loss.Code)];
}
