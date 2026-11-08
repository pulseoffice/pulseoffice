using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class PlatformRound22AuditTests
{
    private sealed class RecordingLaunch : IShellLaunch
    {
        public List<string> Targets { get; } = [];

        public OperationResult<bool> Launch(string target)
        {
            Targets.Add(target);
            return OperationResult<bool>.Ok(true);
        }
    }

    private sealed class RecordingProcess : IExternalProcess
    {
        public List<string> Arguments { get; } = [];

        public Task<ProcessOutcome?> RunAsync(string executable, IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment = null, string? input = null, CancellationToken ct = default)
        {
            Arguments.AddRange(arguments);
            return Task.FromResult<ProcessOutcome?>(new ProcessOutcome(0, string.Empty, string.Empty));
        }
    }

    private sealed class RecordingRunner : ISharingProcessRunner
    {
        public List<string> Arguments { get; } = [];

        public Task<OperationResult<bool>> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            Arguments.AddRange(arguments);
            return Task.FromResult(OperationResult<bool>.Ok(true));
        }
    }

    private sealed class EveryExecutable : IExecutableLocator
    {
        public bool Exists(string executable) => true;
    }

    [Fact]
    public void ARelativeFileWhoseNameLooksLikeAnOptionReachesTheLauncherAsAFullPath()
    {
        var name = "-n-" + Guid.NewGuid().ToString("N") + ".txt";
        File.WriteAllText(name, "x");
        try
        {
            var launch = new RecordingLaunch();

            Assert.True(new ShellOpener(launch).OpenFile(name).Succeeded);
            Assert.True(Path.IsPathFullyQualified(Assert.Single(launch.Targets)));
        }
        finally
        {
            File.Delete(name);
        }
    }

    [Fact]
    public void OnlyHttpAndHttpsAddressesReachTheLauncher()
    {
        var launch = new RecordingLaunch();
        var opener = new ShellOpener(launch);

        Assert.False(opener.OpenUrl("file:///etc/passwd").Succeeded);
        Assert.False(opener.OpenUrl("javascript:alert(1)").Succeeded);
        Assert.True(opener.OpenUrl("https://example.org/a b").Succeeded);
        Assert.Single(launch.Targets);
    }

    [Fact]
    public async Task XdgEmailNeverReceivesARecipientThatIsAMailtoUriOrAnOption()
    {
        var process = new RecordingProcess();

        await new XdgMailComposer(new EveryExecutable(), process).ComposeAsync(new MailDraft
        {
            To = ["mailto:a@example.org?attach=/etc/passwd", "--utf8", "ada@example.org"],
            Cc = ["b@example.org?attach=~/.ssh/id_rsa", "rossi@example.org"],
        }, TestContext.Current.CancellationToken);

        Assert.Contains("ada@example.org", process.Arguments);
        Assert.Contains("rossi@example.org", process.Arguments);
        Assert.DoesNotContain(process.Arguments, argument => argument.Contains("attach=", StringComparison.Ordinal));
        Assert.DoesNotContain("--utf8", process.Arguments);
    }

    [Fact]
    public async Task XdgEmailAttachmentsAndBluetoothFilesArePassedAsFullPaths()
    {
        var process = new RecordingProcess();
        await new XdgMailComposer(new EveryExecutable(), process).ComposeAsync(new MailDraft { Attachments = ["-x.pdf"] }, TestContext.Current.CancellationToken);
        Assert.True(Path.IsPathFullyQualified(process.Arguments[^1]));

        var runner = new RecordingRunner();
        await new BluetoothSendToSharing(runner, new EveryExecutable()).SendAsync(["-x.pdf"], TestContext.Current.CancellationToken);
        Assert.True(Path.IsPathFullyQualified(Assert.Single(runner.Arguments)));
    }
}
