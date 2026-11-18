namespace PulseOffice.Formats.Odf;

public interface IContentSpool
{
    Stream Create();
}

public sealed class MemoryContentSpool : IContentSpool
{
    public Stream Create() => new MemoryStream();
}
