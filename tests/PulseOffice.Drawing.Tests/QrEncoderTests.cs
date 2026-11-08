using PulseOffice.Drawing.Assets;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class QrEncoderTests
{
    private readonly QrVersionTable _versions = new();

    [Fact]
    public void ReedSolomonDaIlVettoreNotoDiHelloWorld()
    {
        byte[] data = [32, 91, 11, 120, 209, 114, 220, 77, 67, 64, 236, 17, 236, 17, 236, 17];

        var remainder = new QrReedSolomon().Remainder(data, 10);

        Assert.Equal(new byte[] { 196, 35, 39, 119, 235, 215, 231, 226, 93, 23 }, remainder);
    }

    [Theory]
    [InlineData(1, QrErrorCorrection.Low, 17)]
    [InlineData(1, QrErrorCorrection.Medium, 14)]
    [InlineData(1, QrErrorCorrection.Quartile, 11)]
    [InlineData(1, QrErrorCorrection.High, 7)]
    [InlineData(4, QrErrorCorrection.Medium, 62)]
    [InlineData(10, QrErrorCorrection.Medium, 213)]
    [InlineData(40, QrErrorCorrection.Low, 2953)]
    [InlineData(40, QrErrorCorrection.High, 1273)]
    public void LeCapacitaInByteSonoQuelleDellaNorma(int version, QrErrorCorrection level, int bytes) =>
        Assert.Equal(bytes, _versions.Get(version, level).ByteCapacity);

    [Fact]
    public void ICentriDiAllineamentoSonoQuelliDellaNorma()
    {
        Assert.Equal([6, 22, 38], _versions.Get(7, QrErrorCorrection.Low).AlignmentCentres);
        Assert.Equal([6, 34, 60, 86, 112, 138], _versions.Get(32, QrErrorCorrection.Low).AlignmentCentres);
        Assert.Empty(_versions.Get(1, QrErrorCorrection.Low).AlignmentCentres);
    }

    [Fact]
    public void IBitDiFormatoEVersioneSonoQuelliNoti()
    {
        var layout = new QrLayout(_versions.Get(7, QrErrorCorrection.Medium));

        Assert.Equal(0b101010000010010, layout.FormatBits(QrErrorCorrection.Medium, 0));
        Assert.Equal(0b111011111000100, layout.FormatBits(QrErrorCorrection.Low, 0));
        Assert.Equal(0x07C94, layout.VersionBits(7));
    }

    [Theory]
    [InlineData(17, QrErrorCorrection.Low, 21)]
    [InlineData(18, QrErrorCorrection.Low, 25)]
    [InlineData(17, QrErrorCorrection.High, 29)]
    public void SceglieLaVersionePiuPiccolaCheContieneIDati(int length, QrErrorCorrection level, int size)
    {
        var matrix = new QrEncoder().Encode(new string('a', length), level);

        Assert.Equal(size, matrix.GetLength(0));
    }

    [Theory]
    [InlineData(QrErrorCorrection.Low)]
    [InlineData(QrErrorCorrection.Medium)]
    [InlineData(QrErrorCorrection.Quartile)]
    [InlineData(QrErrorCorrection.High)]
    public void IlLivelloScrittoNelFormatoEQuelloChiesto(QrErrorCorrection level)
    {
        var matrix = new QrEncoder().Encode("https://pulseoffice.dev", level);

        var bits = 0;
        for (var i = 0; i <= 5; i++) bits |= (matrix[8, i] ? 1 : 0) << i;
        bits |= (matrix[8, 7] ? 1 : 0) << 6;
        bits |= (matrix[8, 8] ? 1 : 0) << 7;
        bits |= (matrix[7, 8] ? 1 : 0) << 8;
        for (var i = 9; i < 15; i++) bits |= (matrix[14 - i, 8] ? 1 : 0) << i;

        var layout = new QrLayout(_versions.Get(1, level));
        var mask = Enumerable.Range(0, 8).Single(candidate => layout.FormatBits(level, candidate) == bits);

        Assert.InRange(mask, 0, 7);
    }

    [Fact]
    public void UnTestoTroppoLungoNonEntra()
    {
        var encoder = new QrEncoder();

        Assert.True(encoder.Fits(new string('a', 1273), QrErrorCorrection.High));
        Assert.False(encoder.Fits(new string('a', 1274), QrErrorCorrection.High));
        Assert.True(encoder.Fits(new string('a', 1274), QrErrorCorrection.Low));
        Assert.Throws<ArgumentException>(() => encoder.Encode(new string('a', 3000), QrErrorCorrection.Low));
    }

    [Fact]
    public void LeVersioniGrandiPortanoIBitDiVersione()
    {
        var matrix = new QrEncoder().Encode(new string('x', 200), QrErrorCorrection.Medium);
        var size = matrix.GetLength(0);
        var number = (size - 17) / 4;

        var bits = 0;
        for (var i = 0; i < 18; i++) bits |= (matrix[size - 11 + (i % 3), i / 3] ? 1 : 0) << i;

        Assert.True(number >= 7);
        Assert.Equal(new QrLayout(_versions.Get(number, QrErrorCorrection.Medium)).VersionBits(number), bits);
    }
}
