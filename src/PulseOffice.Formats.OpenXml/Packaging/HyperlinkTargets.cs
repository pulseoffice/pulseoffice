using DocumentFormat.OpenXml.Packaging;

namespace PulseOffice.Formats.OpenXml;

internal sealed class HyperlinkTargets
{
    private readonly HyperlinkRelationshipIndex _relationships = new();

    public string? Of(OpenXmlPartContainer part, string relationshipId) => _relationships.Of(part, relationshipId)?.Uri.OriginalString;
}
