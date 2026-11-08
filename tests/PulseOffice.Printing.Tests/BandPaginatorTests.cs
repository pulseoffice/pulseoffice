using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public class BandPaginatorTests
{
    private sealed class Band(int first, int last, int size) : IBandMetrics
    {
        private readonly Dictionary<int, int> _sizes = [];
        private readonly HashSet<int> _hidden = [];
        private readonly HashSet<int> _breaks = [];

        public int First => first;
        public int Last => last;

        public Twip SizeOf(int index) => new(_sizes.TryGetValue(index, out var value) ? value : size);

        public bool IsHidden(int index) => _hidden.Contains(index);

        public bool BreaksBefore(int index) => _breaks.Contains(index);

        public Band Size(int index, int value)
        {
            _sizes[index] = value;
            return this;
        }

        public Band Hide(int index)
        {
            _hidden.Add(index);
            return this;
        }

        public Band Break(int index)
        {
            _breaks.Add(index);
            return this;
        }
    }

    private static readonly BandPaginator Paginator = new();

    [Fact]
    public void QuantoCiStaInUnaPaginaRestaInsieme()
    {
        var slices = Paginator.Split(new Band(0, 9, 100), new Twip(1000));

        Assert.Single(slices);
        Assert.Equal(new BandSlice(0, 9), slices[0]);
    }

    [Fact]
    public void CioCheNonCiStaPassaAllaPaginaSuccessiva()
    {
        var slices = Paginator.Split(new Band(0, 9, 100), new Twip(450));

        Assert.Equal(3, slices.Count);
        Assert.Equal(new BandSlice(0, 3), slices[0]);
        Assert.Equal(new BandSlice(4, 7), slices[1]);
        Assert.Equal(new BandSlice(8, 9), slices[2]);
    }

    [Fact]
    public void UnaRigaPiuAltaDellaPaginaOccupaUnaPaginaDaSola()
    {
        var slices = Paginator.Split(new Band(0, 2, 100).Size(1, 5000), new Twip(400));

        Assert.Equal(3, slices.Count);
        Assert.Equal(new BandSlice(1, 1), slices[1]);
    }

    [Fact]
    public void LeRigheNascosteNonOccupanoSpazio()
    {
        var band = new Band(0, 9, 100);
        for (var index = 0; index < 5; index++) band.Hide(index);

        var slices = Paginator.Split(band, new Twip(500));

        Assert.Single(slices);
    }

    [Fact]
    public void UnInterruzioneImpostaApreUnaPaginaAnchePerPoco()
    {
        var slices = Paginator.Split(new Band(0, 5, 100).Break(2), new Twip(1000));

        Assert.Equal(2, slices.Count);
        Assert.Equal(new BandSlice(0, 1), slices[0]);
        Assert.Equal(new BandSlice(2, 5), slices[1]);
    }

    [Fact]
    public void LeRigheRipetuteTolgonoSpazioAOgniPagina()
    {
        var slices = Paginator.Split(new Band(0, 8, 100), new Twip(500), new BandSlice(0, 1));

        Assert.Equal(new BandSlice(0, 4), slices[0]);
        Assert.Equal(new BandSlice(5, 7), slices[1]);
        Assert.Equal(new BandSlice(8, 8), slices[2]);
    }

    [Fact]
    public void LeRigheRipetuteNonInCimaSonoUnIntervalloEsplicito()
    {
        var slices = Paginator.Split(new Band(0, 9, 100), new Twip(500), new BandSlice(3, 4));

        Assert.Equal(new BandSlice(0, 4), slices[0]);
        Assert.Equal(new BandSlice(5, 7), slices[1]);
        Assert.Equal(new BandSlice(8, 9), slices[2]);
    }

    [Fact]
    public void RigheRipetuteOltreLaFineNonProduconoPagineFuoriBanda()
    {
        var slices = Paginator.Split(new Band(0, 2, 100), new Twip(500), new BandSlice(0, 20));

        Assert.Single(slices);
        Assert.Equal(new BandSlice(0, 2), slices[0]);
    }

    [Fact]
    public void UnaBandaVuotaNonProduceAlcunaPagina()
    {
        Assert.Empty(Paginator.Split(new Band(0, -1, 100), new Twip(500)));
    }

    [Fact]
    public void UnaPaginaNonCominciaConRigheNascoste()
    {
        var slices = Paginator.Split(new Band(0, 2, 100).Hide(0).Size(1, 500), new Twip(200));

        Assert.Equal([new BandSlice(1, 1), new BandSlice(2, 2)], slices);
    }

    [Fact]
    public void UnaPaginaCheIniziaDentroITitoliRiservaLoSpazioDeiTitoliChePrecedono()
    {
        var slices = new BandPaginator().Split(new Band(0, 9, 100), new Twip(300), new BandSlice(0, 3));

        Assert.Equal(new BandSlice(0, 2), slices[0]);
        Assert.Equal(new BandSlice(3, 3), slices[1]);
    }
}
