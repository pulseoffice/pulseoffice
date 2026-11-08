using System.Net;

namespace PulseOffice.Foundation.Network;

public interface IDestinationPolicy
{
    bool Permits(IPAddress address);
}

public sealed class PublicInternetPolicy(IAddressClassifier classifier) : IDestinationPolicy
{
    public bool Permits(IPAddress address) => classifier.Classify(address) == AddressScope.Public;
}

public sealed class UserChosenDestinationPolicy(IAddressClassifier classifier) : IDestinationPolicy
{
    public bool Permits(IPAddress address) =>
        classifier.Classify(address) is AddressScope.Public or AddressScope.Private or AddressScope.Loopback;
}

public sealed class DestinationRefusedException(string host)
    : IOException($"The destination '{host}' is not allowed for this request.")
{
    public string Host { get; } = host;
}
