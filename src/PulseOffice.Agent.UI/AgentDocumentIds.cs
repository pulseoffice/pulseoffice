using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using PulseOffice.Shell;

namespace PulseOffice.Agent.UI;

public interface IAgentDocumentIds
{
    string IdOf(OpenDocument document);
}

public sealed class AgentDocumentIds : IAgentDocumentIds
{
    private readonly ConditionalWeakTable<OpenDocument, string> _ids = [];

    public string IdOf(OpenDocument document) =>
        _ids.GetValue(document, _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant());
}
