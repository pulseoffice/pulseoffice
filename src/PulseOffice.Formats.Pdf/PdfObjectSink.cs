namespace PulseOffice.Formats.Pdf;

public interface IPdfObjectSink
{
    int Reserve();

    int WriteObject(string body);

    void WriteObject(int id, string body);

    int WriteStream(byte[] payload, bool compress, string? extraDict);
}
