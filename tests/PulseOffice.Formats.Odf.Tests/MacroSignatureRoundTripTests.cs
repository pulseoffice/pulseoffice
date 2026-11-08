using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PulseOffice.Document.Forms;
using PulseOffice.Presentations;
using PulseOffice.Security;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class MacroSignatureRoundTripTests
{
    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Prova Macro", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
    }

    [Fact]
    public void LaFirmaDelleMacroSopravviveInOdpEVerificaAncora()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));
        deck.Macros.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "app.Log('ciao')"));
        var signer = new MacroSigner();
        deck.MacroSignature = signer.Sign(deck.SignableMacros, Certificate());

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var back = new OdpReader(themes, masters).Read(stream).Value!;

        Assert.Equal("Prova Macro", back.MacroSignature?.Signer.Name);
        Assert.Equal(SignatureStatus.UntrustedCertificate, signer.Verify(back.SignableMacros, back.MacroSignature));

        back.Macros.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "app.Log('altro')"));
        Assert.Equal(SignatureStatus.Broken, signer.Verify(back.SignableMacros, back.MacroSignature));
    }

    [Fact]
    public void LaFirmaDelleMacroSopravviveInOds()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Macros.Store(new DocumentMacro("Somma", MacroLanguage.Python, "print(1)"));
        var signer = new MacroSigner();
        workbook.MacroSignature = signer.Sign(workbook.SignableMacros, Certificate());

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var back = new OdsReader().Read(stream).Value!;

        Assert.Equal(SignatureStatus.UntrustedCertificate, signer.Verify(back.SignableMacros, back.MacroSignature));
    }
}

public sealed class FormSettingsRoundTripTests
{
    [Fact]
    public void LeImpostazioniDeiModuliSopravvivonoInOdp()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, FormSettings = new FormDocumentSettings(true, true) };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        var catalog = new FormControlCatalog();
        slide.Add(new ObjectElement
        {
            Id = "c1",
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(new(100), new(100), new(2000), new(400)),
            Content = new FormControlObject { Id = "c1", ControlKind = FormControlKind.CheckBox, Properties = catalog.Create(FormControlKind.CheckBox, "Spunta") },
        });
        deck.AddExisting(slide);

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(new FormDocumentSettings(true, true), new OdpReader(themes, masters).Read(stream).Value!.FormSettings);
    }
}
