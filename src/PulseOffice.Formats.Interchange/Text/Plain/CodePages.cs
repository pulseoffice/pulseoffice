using System.Text;

namespace PulseOffice.Formats.Interchange.Text.Plain;

public interface ICodePages
{
    Encoding Get(int codePage);

    Encoding? Named(string name);
}

public sealed class CodePages : ICodePages
{
    private readonly EncodingProvider _provider = CodePagesEncodingProvider.Instance;

    public Encoding Get(int codePage)
    {
        if (codePage == 65001) return new UTF8Encoding(false);
        try
        {
            return _provider.GetEncoding(codePage) ?? Encoding.GetEncoding(codePage);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return _provider.GetEncoding(1252)!;
        }
    }

    public Encoding? Named(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            return _provider.GetEncoding(name.Trim()) ?? Encoding.GetEncoding(name.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
