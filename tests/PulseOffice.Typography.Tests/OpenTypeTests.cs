using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;
using Xunit;

namespace PulseOffice.Typography.Tests;

public class OpenTypeTests
{
    private readonly OpenTypeReader _reader = new();

    [Theory]
    [InlineData(7, 700)]
    [InlineData(4, 400)]
    [InlineData(300, 300)]
    public void IPesiNellaScalaVecchiaSiPortanoAiCentinaia(int written, int expected)
    {
        var face = _reader.Read(new SyntheticFont { Weight = (ushort)written }.Build()).Single();

        Assert.Equal(expected, face.Weight);
    }

    [Fact]
    public void LeggeNomiPesoLicenzaEFsType()
    {
        var face = _reader.Read(new SyntheticFont { Weight = 700, FsType = 0x0008 }.Build()).Single();

        Assert.Equal("Prova Sans", face.Family);
        Assert.Equal("Regular", face.Subfamily);
        Assert.Equal(700, face.Weight);
        Assert.Equal((ushort)0x0008, face.FsType);
        Assert.Contains("Open Font License", face.LicenseDescription);
        Assert.Equal("https://openfontlicense.org", face.LicenseUrl);
        Assert.Equal(0x12345678u, face.CheckSumAdjustment);
        Assert.Equal(EmbeddingLevel.Editable, face.Embedding.Level);
    }

    [Fact]
    public void LeggeIlCorsivoDaFsSelection()
    {
        var face = _reader.Read(new SyntheticFont { Italic = true }.Build()).Single();
        Assert.True(face.Italic);
    }

    [Fact]
    public void LeggeOgniFacciaDiUnaCollezione()
    {
        var collection = new SyntheticFont { Family = "Uno" }.BuildCollection(new SyntheticFont { Family = "Due", Weight = 700 });
        var faces = _reader.Read(collection);

        Assert.Equal(["Uno", "Due"], faces.Select(face => face.Family));
        Assert.Equal(1, faces[1].CollectionIndex);
        Assert.Equal(700, faces[1].Weight);
    }

    [Fact]
    public void EstraeUnaFacciaDallaCollezioneInUnFileAutonomo()
    {
        var collection = new SyntheticFont { Family = "Uno" }.BuildCollection(new SyntheticFont { Family = "Due", Weight = 700 });
        var single = new FontFileExtractor().Extract(collection, 1);

        var face = _reader.Read(single).Single();
        Assert.Equal("Due", face.Family);
        Assert.Equal(700, face.Weight);
        Assert.True(_reader.LooksLikeFont(single));
    }

    [Fact]
    public void UnFileNonFontVieneRifiutato()
    {
        Assert.Throws<InvalidDataException>(() => _reader.Read("non è un carattere"u8.ToArray()));
        Assert.False(_reader.LooksLikeFont("PK\u0003\u0004"u8));
    }

    [Fact]
    public void UnaTabellaTroncataNonFaCadere()
    {
        var font = new SyntheticFont().Build();
        Assert.ThrowsAny<Exception>(() => _reader.Read(font[..40]));
    }

    [Theory]
    [InlineData(0x0000, EmbeddingLevel.Installable, true)]
    [InlineData(0x0002, EmbeddingLevel.Restricted, false)]
    [InlineData(0x0004, EmbeddingLevel.PreviewAndPrint, true)]
    [InlineData(0x0008, EmbeddingLevel.Editable, true)]
    [InlineData(0x000C, EmbeddingLevel.Editable, true)]
    [InlineData(0x0006, EmbeddingLevel.PreviewAndPrint, true)]
    public void FsTypeDiventaUnPermesso(int fsType, EmbeddingLevel level, bool allowed)
    {
        var permission = EmbeddingPermission.FromFsType((ushort)fsType);
        Assert.Equal(level, permission.Level);
        Assert.Equal(allowed, permission.AllowsEmbedding);
    }

    [Fact]
    public void SoloBitmapVietaLIncorporamento()
    {
        var permission = EmbeddingPermission.FromFsType(0x0200);
        Assert.True(permission.BitmapOnly);
        Assert.False(permission.AllowsEmbedding);
        Assert.True(EmbeddingPermission.FromFsType(0x0100).NoSubsetting);
    }

    [Fact]
    public void EmbeddedOpenTypeFaIlGiroCompleto()
    {
        var font = new SyntheticFont().Build();
        var face = _reader.Read(font).Single();
        var codec = new EmbeddedOpenTypeCodec();

        var eot = codec.Encode(font, face);

        Assert.Equal((uint)eot.Length, BitConverter.ToUInt32(eot, 0));
        Assert.Equal((uint)font.Length, BitConverter.ToUInt32(eot, 4));
        Assert.Equal(0x504C, BitConverter.ToUInt16(eot, 34));
        Assert.Equal(font, codec.Decode(eot));
    }

    [Fact]
    public void EmbeddedOpenTypeCifratoConXorSiDecifra()
    {
        var font = new SyntheticFont().Build();
        var codec = new EmbeddedOpenTypeCodec();
        var eot = codec.Encode(font, _reader.Read(font).Single());
        BitConverter.GetBytes(0x10000000u).CopyTo(eot, 12);
        for (var index = eot.Length - font.Length; index < eot.Length; index++) eot[index] ^= 0x50;

        Assert.Equal(font, codec.Decode(eot));
    }

    [Fact]
    public void EmbeddedOpenTypeCompressoConDatiNonMtxSiRifiuta()
    {
        var font = new SyntheticFont().Build();
        var codec = new EmbeddedOpenTypeCodec();
        var eot = codec.Encode(font, _reader.Read(font).Single());
        BitConverter.GetBytes(0x4u).CopyTo(eot, 12);

        Assert.Throws<InvalidDataException>(() => codec.Decode(eot));
    }

    [Fact]
    public void CategoriaDaPanoseEDaPitch()
    {
        var categories = new FontCategoryClassifier();
        Assert.Equal(FontCategory.SansSerif, categories.Classify(_reader.Read(new SyntheticFont().Build()).Single()));
        Assert.Equal(FontCategory.Serif, categories.Classify(_reader.Read(new SyntheticFont { Panose = [2, 2, 6, 3, 5, 4, 5, 2, 3, 4] }.Build()).Single()));
        Assert.Equal(FontCategory.Monospace, categories.Classify(_reader.Read(new SyntheticFont { FixedPitch = true }.Build()).Single()));
        Assert.Equal(FontCategory.Handwriting, categories.Classify(_reader.Read(new SyntheticFont { Panose = [3, 0, 0, 0, 0, 0, 0, 0, 0, 0] }.Build()).Single()));
        Assert.Equal(FontCategory.SansSerif, categories.FromName("sans-serif"));
        Assert.Equal(FontCategory.Handwriting, categories.FromName("HANDWRITING"));
    }

    [Fact]
    public void ScrittureDaiBitUnicodeEDaiSottoinsiemi()
    {
        var coverage = new FontScriptCoverage();
        Assert.Equal([FontScript.Latin, FontScript.Greek, FontScript.Cyrillic], coverage.FromUnicodeRanges([0x00000283u, 0, 0, 0]));
        Assert.Equal([FontScript.Latin, FontScript.Cyrillic, FontScript.Japanese], coverage.FromSubsets(["latin", "cyrillic-ext", "japanese", "menu"]));
    }
}

public class LicenseTests
{
    private readonly FontLicenseClassifier _classifier = new();

    [Theory]
    [InlineData("OFL-1.1", FontLicenseKind.Ofl)]
    [InlineData("Apache-2.0", FontLicenseKind.Apache)]
    [InlineData("UFL-1.0", FontLicenseKind.Ubuntu)]
    [InlineData("mit", FontLicenseKind.Mit)]
    [InlineData("CC0-1.0", FontLicenseKind.Cc0)]
    [InlineData("Unlicense", FontLicenseKind.PublicDomain)]
    public void IdentificativiSpdx(string spdx, FontLicenseKind kind)
    {
        var license = _classifier.FromSpdx(spdx);
        Assert.Equal(kind, license.Kind);
        Assert.True(license.IsFree);
    }

    [Fact]
    public void DescrizioneOflDallaTabellaName()
    {
        var license = _classifier.FromDescription("This Font Software is licensed under the SIL Open Font License, Version 1.1.", null);
        Assert.Equal(FontLicenseKind.Ofl, license.Kind);
        Assert.Equal(LicenseFreedom.Free, license.Freedom);
        Assert.NotNull(license.Url);
    }

    [Fact]
    public void LicenzaUbuntuEApacheDaUrl()
    {
        Assert.Equal(FontLicenseKind.Ubuntu, _classifier.FromDescription(null, "https://ubuntu.com/legal/font-licence").Kind);
        Assert.Equal(FontLicenseKind.Apache, _classifier.FromDescription(null, "http://www.apache.org/licenses/LICENSE-2.0").Kind);
    }

    [Fact]
    public void UnaLicenzaCommercialeEConRestrizioni()
    {
        var license = _classifier.FromDescription("This font is licensed to you under the terms of the EULA. All rights reserved.", null);
        Assert.Equal(FontLicenseKind.Proprietary, license.Kind);
        Assert.Equal(LicenseFreedom.Restricted, license.Freedom);
    }

    [Fact]
    public void SenzaDescrizioneLaLicenzaESconosciuta()
    {
        Assert.Equal(LicenseFreedom.Unknown, _classifier.FromDescription(null, null).Freedom);
        Assert.Equal(LicenseFreedom.Unknown, _classifier.FromSpdx(null).Freedom);
    }

    [Fact]
    public void LaParolaSubmitNonDiventaMit()
    {
        Assert.NotEqual(FontLicenseKind.Mit, _classifier.FromDescription("Please submit fixes upstream.", null).Kind);
    }
}
