using System.Net;
using System.Net.Sockets;

namespace PulseOffice.Foundation.Network;

public enum OutboundAudience
{
    DocumentContent,
    UserChosen,
}

public interface IOutboundHttpClients
{
    HttpClient Create(OutboundAudience audience, TimeSpan timeout);
}

public sealed class OutboundHttpClients(IAddressClassifier classifier) : IOutboundHttpClients
{
    private const int MaximumRedirections = 5;

    public HttpClient Create(OutboundAudience audience, TimeSpan timeout)
    {
        IDestinationPolicy policy = audience == OutboundAudience.DocumentContent
            ? new PublicInternetPolicy(classifier)
            : new UserChosenDestinationPolicy(classifier);
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            UseCookies = false,
            Credentials = null,
            PreAuthenticate = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = MaximumRedirections,
            ConnectTimeout = timeout,
            ConnectCallback = (context, ct) => ConnectAsync(policy, context.DnsEndPoint, ct),
        };
        return new HttpClient(handler, disposeHandler: true) { Timeout = timeout };
    }

    private async ValueTask<Stream> ConnectAsync(IDestinationPolicy policy, DnsEndPoint endpoint, CancellationToken ct)
    {
        var addresses = IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endpoint.Host, ct).ConfigureAwait(false);
        var permitted = addresses.Where(policy.Permits).ToArray();
        if (permitted.Length == 0) throw new DestinationRefusedException(endpoint.Host);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(permitted, endpoint.Port, ct).ConfigureAwait(false);
            if (socket.RemoteEndPoint is not IPEndPoint connected || !policy.Permits(connected.Address))
                throw new DestinationRefusedException(endpoint.Host);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
