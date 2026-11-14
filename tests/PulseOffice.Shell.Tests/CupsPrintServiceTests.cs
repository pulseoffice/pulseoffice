using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class CupsPrintServiceTests
{
    private sealed class Answers(params (string Command, ProcessOutcome Outcome)[] answers) : IExternalProcess
    {
        public List<(string Executable, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string>? Environment)> Calls { get; } = [];

        public Task<ProcessOutcome?> RunAsync(string executable, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null,
            string? input = null, CancellationToken ct = default)
        {
            Calls.Add((executable, arguments, environment));
            var match = answers.FirstOrDefault(answer => answer.Command == executable + " " + string.Join(' ', arguments));
            return Task.FromResult<ProcessOutcome?>(match.Outcome ?? new ProcessOutcome(0, string.Empty, string.Empty));
        }
    }

    [Fact]
    public async Task Printers_come_from_lpstat_with_the_default_marked()
    {
        var processes = new Answers(
            ("lpstat -e", new ProcessOutcome(0, "Office_Laser\nHome\n", string.Empty)),
            ("lpstat -d", new ProcessOutcome(0, "system default destination: Home\n", string.Empty)));

        var printers = await new CupsPrintService(processes).PrintersAsync(TestContext.Current.CancellationToken);

        Assert.Equal([new Printer("Office_Laser", "Office Laser", false), new Printer("Home", "Home", true)], printers);
        Assert.All(processes.Calls, call => Assert.Equal("C", call.Environment!["LC_ALL"]));
    }

    [Fact]
    public async Task A_job_carries_printer_copies_collation_sides_and_an_absolute_path()
    {
        var processes = new Answers();

        var result = await new CupsPrintService(processes).PrintAsync(new PrintRequest
        {
            FilePath = "out.pdf",
            PrinterId = "Home",
            Copies = 2,
            Collate = false,
            Sides = PrintSides.LongEdge,
            JobName = "Budget",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var (executable, arguments, _) = Assert.Single(processes.Calls);
        Assert.Equal("lp", executable);
        Assert.Equal(["-d", "Home", "-n", "2", "-o", "collate=false", "-o", "sides=two-sided-long-edge", "-t", "Budget", Path.GetFullPath("out.pdf")], arguments);
    }

    [Fact]
    public async Task A_refused_job_reports_what_lp_said()
    {
        var processes = new Answers(("lp -t PulseOffice " + Path.GetFullPath("x.pdf"), new ProcessOutcome(1, string.Empty, "lp: The printer or class does not exist.")));

        var result = await new CupsPrintService(processes).PrintAsync(new PrintRequest { FilePath = "x.pdf" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("does not exist", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }
}
