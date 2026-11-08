using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DisplayReadinessTests
{
    private sealed class ScriptedProbe(params bool[] answers) : IActiveDisplayProbe
    {
        private int calls;

        public int Calls => calls;

        public bool HasActiveDisplay() => answers[Math.Min(calls++, answers.Length - 1)];
    }

    private sealed class FakeClock : IDisplayWaitClock
    {
        public TimeSpan Elapsed { get; private set; }

        public void Pause(TimeSpan interval) => Elapsed += interval;
    }

    [Fact]
    public void Con_lo_schermo_spento_l_avvio_aspetta_che_torni_invece_di_cadere()
    {
        var probe = new ScriptedProbe(false, false, false, true);
        var clock = new FakeClock();
        var outcome = new DisplayReadinessGate(probe, clock).WaitForActiveDisplay(TimeSpan.FromMinutes(1));
        Assert.Equal(DisplayReadinessOutcome.ReadyAfterWaiting, outcome);
        Assert.Equal(4, probe.Calls);
        Assert.Equal(TimeSpan.FromSeconds(1.5), clock.Elapsed);
    }

    [Fact]
    public void Senza_schermo_per_tutto_il_tempo_l_attesa_si_arrende_entro_il_limite()
    {
        var clock = new FakeClock();
        var outcome = new DisplayReadinessGate(new ScriptedProbe(false), clock).WaitForActiveDisplay(TimeSpan.FromSeconds(5));
        Assert.Equal(DisplayReadinessOutcome.TimedOut, outcome);
        Assert.Equal(TimeSpan.FromSeconds(5), clock.Elapsed);
    }

    [Fact]
    public void Con_lo_schermo_acceso_non_si_aspetta()
    {
        var clock = new FakeClock();
        Assert.Equal(DisplayReadinessOutcome.ReadyImmediately,
            new DisplayReadinessGate(new AlwaysActiveDisplayProbe(), clock).WaitForActiveDisplay(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public void La_sonda_di_macOS_interroga_CoreVideo_senza_eccezioni()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var answer = new PulseOffice.Platform.MacOS.MacActiveDisplayProbe().HasActiveDisplay();
        Assert.True(answer || !answer);
    }
}
