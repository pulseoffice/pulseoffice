using Xunit;

namespace PulseOffice.Documents.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DocumentTimingCollection
{
    public const string Name = "Document timing";
}
