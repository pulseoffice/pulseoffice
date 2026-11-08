using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class HostSystemTests
{
    [Theory]
    [InlineData(HostSystemKind.MacOS, StringComparison.OrdinalIgnoreCase)]
    [InlineData(HostSystemKind.Windows, StringComparison.OrdinalIgnoreCase)]
    [InlineData(HostSystemKind.Linux, StringComparison.Ordinal)]
    [InlineData(HostSystemKind.OtherUnix, StringComparison.Ordinal)]
    public void Paths_compare_by_the_case_rules_of_the_system(HostSystemKind kind, StringComparison expected) =>
        Assert.Equal(expected, ((IHostSystem)new FixedHostSystem(kind)).PathComparison);

    [Fact]
    public void Only_windows_executables_carry_the_exe_extension()
    {
        Assert.Equal("python3.exe", ((IHostSystem)new FixedHostSystem(HostSystemKind.Windows)).ExecutableName("python3"));
        Assert.Equal("python3", ((IHostSystem)new FixedHostSystem(HostSystemKind.MacOS)).ExecutableName("python3"));
    }

    [Fact]
    public void The_runtime_host_is_the_system_the_tests_run_on()
    {
        IHostSystem host = new RuntimeHostSystem();

        Assert.Equal(OperatingSystem.IsMacOS(), host.IsMacOS);
        Assert.Equal(OperatingSystem.IsWindows(), host.IsWindows);
        Assert.Equal(OperatingSystem.IsLinux(), host.IsLinux);
        Assert.Equal(Environment.OSVersion.VersionString, host.VersionString);
    }
}
