using System.Net;
using System.Net.Sockets;

namespace PulseOffice.Foundation.Network;

public enum AddressScope
{
    Public,
    Loopback,
    Private,
    LinkLocal,
    Unspecified,
    Multicast,
    Reserved,
}

public interface IAddressClassifier
{
    AddressScope Classify(IPAddress address);
}

public sealed class AddressClassifier : IAddressClassifier
{
    public AddressScope Classify(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) return ClassifyVersion4(address.MapToIPv4().GetAddressBytes());
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => ClassifyVersion4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => ClassifyVersion6(address.GetAddressBytes()),
            _ => AddressScope.Reserved,
        };
    }

    private AddressScope ClassifyVersion4(byte[] bytes)
    {
        var (a, b, c) = (bytes[0], bytes[1], bytes[2]);
        if (a == 0) return bytes.All(part => part == 0) ? AddressScope.Unspecified : AddressScope.Reserved;
        if (a == 127) return AddressScope.Loopback;
        if (a == 10 || (a == 172 && b is >= 16 and <= 31) || (a == 192 && b == 168) || (a == 100 && b is >= 64 and <= 127))
            return AddressScope.Private;
        if (a == 169 && b == 254) return AddressScope.LinkLocal;
        if (a is >= 224 and <= 239) return AddressScope.Multicast;
        if (a >= 240) return AddressScope.Reserved;
        if (a == 192 && b == 0 && c is 0 or 2) return AddressScope.Reserved;
        if (a == 198 && b is 18 or 19) return AddressScope.Reserved;
        if ((a == 198 && b == 51 && c == 100) || (a == 203 && b == 0 && c == 113)) return AddressScope.Reserved;
        return AddressScope.Public;
    }

    private AddressScope ClassifyVersion6(byte[] bytes)
    {
        if (bytes.Take(15).All(part => part == 0))
            return bytes[15] switch { 0 => AddressScope.Unspecified, 1 => AddressScope.Loopback, _ => AddressScope.Reserved };
        if (bytes.Take(12).All(part => part == 0)) return AddressScope.Reserved;
        if (bytes[0] == 0xff) return AddressScope.Multicast;
        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return AddressScope.LinkLocal;
        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0) return AddressScope.Private;
        if ((bytes[0] & 0xfe) == 0xfc) return AddressScope.Private;
        if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b) return ClassifyVersion4(bytes[12..16]);
        if (bytes[0] == 0x20 && bytes[1] == 0x02) return ClassifyVersion4(bytes[2..6]);
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x00) return AddressScope.Reserved;
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) return AddressScope.Reserved;
        return AddressScope.Public;
    }
}
