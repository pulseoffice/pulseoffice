using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Packaging;

namespace PulseOffice.Formats.OpenXml;

internal abstract class PartRelationshipIndex<TRelationship>
    where TRelationship : ReferenceRelationship
{
    private readonly ConditionalWeakTable<OpenXmlPartContainer, Dictionary<string, TRelationship>> _byPart = [];

    protected abstract IEnumerable<TRelationship> Relationships(OpenXmlPartContainer part);

    public TRelationship? Of(OpenXmlPartContainer part, string relationshipId)
    {
        if (_byPart.TryGetValue(part, out var known) && known.TryGetValue(relationshipId, out var cached)) return cached;

        var byId = new Dictionary<string, TRelationship>(StringComparer.Ordinal);
        foreach (var relationship in Relationships(part)) byId.TryAdd(relationship.Id, relationship);
        _byPart.AddOrUpdate(part, byId);
        return byId.GetValueOrDefault(relationshipId);
    }
}

internal sealed class HyperlinkRelationshipIndex : PartRelationshipIndex<HyperlinkRelationship>
{
    protected override IEnumerable<HyperlinkRelationship> Relationships(OpenXmlPartContainer part) => part.HyperlinkRelationships;
}

internal sealed class ExternalRelationshipIndex : PartRelationshipIndex<ExternalRelationship>
{
    protected override IEnumerable<ExternalRelationship> Relationships(OpenXmlPartContainer part) => part.ExternalRelationships;
}

internal sealed class DataReferenceRelationshipIndex : PartRelationshipIndex<DataPartReferenceRelationship>
{
    protected override IEnumerable<DataPartReferenceRelationship> Relationships(OpenXmlPartContainer part) => part.DataPartReferenceRelationships;
}
