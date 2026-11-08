using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class MediaTypesTests
{
    private readonly MediaTypes _types = new();

    [Theory]
    [InlineData("audio/unknown", "media/mediadatabin", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45 }, "audio/wav")]
    [InlineData("application/vnd.sun.star.media", "Media/a.mp3", new byte[] { 0 }, "audio/mpeg")]
    [InlineData("video/mp4", "x.bin", new byte[] { 0 }, "video/mp4")]
    [InlineData(null, "x", new byte[] { 0, 0, 0, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20 }, "audio/mp4")]
    public void TheRealTypeIsRecoveredFromGenericDeclarations(string? declared, string path, byte[] data, string expected) =>
        Assert.Equal(expected, _types.Resolve(declared, path, data));
}
