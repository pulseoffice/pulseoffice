using PulseOffice.Foundation.Network;

namespace PulseOffice.Sheets.UI.Tests;

internal sealed class HandlerClients(HttpMessageHandler handler) : IOutboundHttpClients
{
    public HttpClient Create(OutboundAudience audience, TimeSpan timeout) => new(handler) { Timeout = timeout };
}
