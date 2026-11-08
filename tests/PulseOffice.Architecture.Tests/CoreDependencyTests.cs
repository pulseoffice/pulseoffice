using System.Reflection;
using Xunit;

namespace PulseOffice.Architecture.Tests;

public sealed class CoreDependencyTests
{
    public static TheoryData<string> CoreAssemblies() =>
    [
        "PulseOffice.Foundation",
        "PulseOffice.Document",
        "PulseOffice.Drawing",
        "PulseOffice.Text",
        "PulseOffice.Text.Rich",
        "PulseOffice.Presentations",
        "PulseOffice.Documents",
        "PulseOffice.Documents.Layout",
        "PulseOffice.Formats",
        "PulseOffice.Formats.Odf",
        "PulseOffice.Formats.OpenXml",
        "PulseOffice.Formats.Rasterizing",
    ];

    [Theory]
    [InlineData("PulseOffice.Formats")]
    [InlineData("PulseOffice.Formats.Odf")]
    [InlineData("PulseOffice.Formats.OpenXml")]
    public void Gli_scrittori_dei_formati_rasterizzano_solo_attraverso_l_interfaccia(string name)
    {
        var closure = Closure(Assembly.Load(name));

        Assert.DoesNotContain(closure, reference => reference.StartsWith("SkiaSharp", StringComparison.Ordinal) || reference.StartsWith("Svg", StringComparison.Ordinal));
        Assert.DoesNotContain("PulseOffice.Formats.Rasterizing", closure);
    }

    [Theory]
    [MemberData(nameof(CoreAssemblies))]
    public void IlNucleoNonDipendeDaAvalonia(string name)
    {
        var offending = Closure(Assembly.Load(name))
            .Where(reference => reference.StartsWith("Avalonia", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offending);
    }

    [Fact]
    public void IlTestoRiccoNonDipendeDaDeck()
    {
        var closure = Closure(Assembly.Load("PulseOffice.Text.Rich"));

        Assert.DoesNotContain("PulseOffice.Presentations", closure);
    }

    [Fact]
    public void FolioNonDipendeDaDeck()
    {
        var closure = Closure(Assembly.Load("PulseOffice.Documents"));

        Assert.DoesNotContain("PulseOffice.Presentations", closure);
        Assert.DoesNotContain("PulseOffice.Sheets", closure);
    }

    [Fact]
    public void LImpaginazioneDiFolioNonDipendeDaDeckNeDaiFogli()
    {
        var closure = Closure(Assembly.Load("PulseOffice.Documents.Layout"));

        Assert.DoesNotContain("PulseOffice.Presentations", closure);
        Assert.DoesNotContain("PulseOffice.Sheets", closure);
        Assert.DoesNotContain("PulseOffice.UI.Controls", closure);
    }

    private IReadOnlySet<string> Closure(Assembly root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<AssemblyName>(root.GetReferencedAssemblies());
        while (pending.Count > 0)
        {
            var reference = pending.Pop();
            if (reference.Name is not { } name || !seen.Add(name)) continue;
            if (!name.StartsWith("PulseOffice", StringComparison.Ordinal)) continue;
            foreach (var next in Assembly.Load(reference).GetReferencedAssemblies()) pending.Push(next);
        }
        return seen;
    }
}
