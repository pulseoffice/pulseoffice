using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OdtDocxReferenceParityTests
{
    private const string Folder = "Fixtures/TextParity";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly PulseOffice.Formats.Odf.Tests.Odt.TextDocumentEssence _essence = new();

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Folder, "*.fodt").Order(StringComparer.Ordinal)) data.Add(Path.GetFileNameWithoutExtension(path));
        return data;
    }

    private TextDocument Read(string file, ITextDocumentReader reader)
    {
        using var stream = File.OpenRead(Path.Combine(Folder, file));
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void IlGiroOdtSuiteEsternaDocxNonPerdeNulla(string name)
    {
        var original = Read(name + ".fodt", new OdtReader());
        var back = Read(name + ".via-odt.docx", _composer.Reader(DocxFlavour.Document));
        Assert.Equal(Essence(original), Essence(back));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void IlGiroDocxSuiteEsternaOdtNonPerdeNulla(string name)
    {
        var original = Read(name + ".fodt", new OdtReader());
        var back = Read(name + ".via-docx.odt", new OdtReader());
        Assert.Equal(Essence(original), Essence(back));
    }

    private string Essence(TextDocument document) => _essence.Describe(document);
}
