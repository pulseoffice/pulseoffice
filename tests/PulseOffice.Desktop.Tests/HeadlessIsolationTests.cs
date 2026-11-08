using System.Reflection;
using Xunit;

namespace PulseOffice.Desktop.Tests;

public sealed class HeadlessIsolationTests
{
    [Fact]
    public void Ogni_classe_di_test_gira_nella_sessione_headless_condivisa()
    {
        var outside = typeof(HeadlessIsolationTests).Assembly.GetTypes()
            .Where(type => type != typeof(HeadlessIsolationTests))
            .Where(type => type.GetMethods().Any(method => method.GetCustomAttributes<FactAttribute>().Any()))
            .Where(type => type.GetCustomAttribute<CollectionAttribute>() is null)
            .Select(type => type.Name)
            .ToList();
        Assert.True(outside.Count == 0, "Fuori dalla sessione headless (il dispatcher di Avalonia si lega al primo thread che lo tocca): " + string.Join(", ", outside));
    }
}
