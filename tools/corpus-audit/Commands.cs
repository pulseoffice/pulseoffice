using System.Diagnostics;
using System.Text;

namespace PulseOffice.Tools.CorpusAudit;

public sealed record AuditOptions(string Root, string Corpus, string Results, string Summary, int Jobs, TimeSpan Timeout);

public interface IAuditCommand
{
    string Name { get; }

    int Run(IReadOnlyList<string> arguments);
}

public sealed class ListCommand(IEnumerable<IAuditFamily> families) : IAuditCommand
{
    private readonly string[] _areas = ["sc/qa", "sw/qa", "sd/qa"];

    public string Name => "list";

    public int Run(IReadOnlyList<string> arguments)
    {
        var root = arguments[0];
        var extensions = families.SelectMany(family => family.Extensions).ToHashSet(StringComparer.Ordinal);
        var files = _areas.SelectMany(area => Directory.EnumerateFiles(Path.Combine(root, area), "*", SearchOption.AllDirectories))
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal);
        File.WriteAllLines(arguments[1], files);
        return 0;
    }
}

public sealed class OneCommand(IFileAuditor auditor) : IAuditCommand
{
    public string Name => "one";

    public int Run(IReadOnlyList<string> arguments)
    {
        foreach (var row in auditor.Audit(arguments[0], arguments[1])) Console.Out.WriteLine(row.ToLine());
        return 0;
    }
}

public interface IWorkerLauncher
{
    IReadOnlyList<AuditRow> Launch(string root, string relative, TimeSpan timeout);
}

public sealed class WorkerLauncher : IWorkerLauncher
{
    public IReadOnlyList<AuditRow> Launch(string root, string relative, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!) == "dotnet") start.ArgumentList.Add(typeof(WorkerLauncher).Assembly.Location);
        foreach (var argument in new[] { "one", root, relative }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(true);
            return [new AuditRow(relative, "-", "-", 0, 0, "TIMEOUT", "-")];
        }

        var rows = output.Result.Split('\n').Select(line => AuditRow.Parse(line.TrimEnd('\r'))).OfType<AuditRow>().ToList();
        return rows.Count > 0 ? rows : [new AuditRow(relative, "-", "-", 0, 0, "CRASH:exit" + process.ExitCode, "-")];
    }
}

public sealed class RunCommand(IWorkerLauncher launcher, ISummaryWriter summary) : IAuditCommand
{
    public string Name => "run";

    public int Run(IReadOnlyList<string> arguments)
    {
        var options = new AuditOptions(arguments[0], arguments[1], arguments[2], arguments[3],
            arguments.Count > 4 ? int.Parse(arguments[4], System.Globalization.CultureInfo.InvariantCulture) : Math.Max(1, Environment.ProcessorCount - 2),
            TimeSpan.FromSeconds(arguments.Count > 5 ? int.Parse(arguments[5], System.Globalization.CultureInfo.InvariantCulture) : 120));
        var files = File.ReadAllLines(options.Corpus).Where(line => line.Length > 0).ToList();
        var rows = new System.Collections.Concurrent.ConcurrentBag<AuditRow>();
        var done = 0;
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = options.Jobs }, file =>
        {
            foreach (var row in launcher.Launch(options.Root, file, options.Timeout)) rows.Add(row);
            var finished = Interlocked.Increment(ref done);
            if (finished % 100 == 0) Console.Error.WriteLine($"{finished}/{files.Count}");
        });
        var ordered = rows.OrderBy(row => row.File, StringComparer.Ordinal).ThenBy(row => row.Target, StringComparer.Ordinal)
            .ThenBy(row => row.Feature, StringComparer.Ordinal).ToList();
        File.WriteAllLines(options.Results, ordered.Where(row => row.Class != "OK").Select(row => row.ToLine()), new UTF8Encoding(false));
        File.WriteAllText(options.Summary, summary.Compose(files.Count, ordered), new UTF8Encoding(false));
        Console.Out.WriteLine($"LOSS {ordered.Count(row => row.Class == "LOSS")} DECL {ordered.Count(row => row.Class == "DECL")}");
        return 0;
    }
}

public interface ISummaryWriter
{
    string Compose(int files, IReadOnlyList<AuditRow> rows);
}

public sealed class SummaryWriter : ISummaryWriter
{
    public string Compose(int files, IReadOnlyList<AuditRow> rows)
    {
        var text = new StringBuilder();
        var copies = rows.Where(row => row.Feature == "-").ToList();
        text.AppendLine("# Corpus audit summary").AppendLine();
        text.AppendLine($"Files: {files}; copies written: {copies.Count(row => row.Target != "-")}").AppendLine();
        text.AppendLine("## Status").AppendLine();
        foreach (var group in copies.GroupBy(row => row.Class).OrderBy(group => group.Key, StringComparer.Ordinal))
            text.AppendLine($"- {group.Key}: {group.Count()}");
        var losses = rows.Where(row => row.Class is "LOSS" or "DECL").ToList();
        text.AppendLine().AppendLine($"## Losses: LOSS {losses.Count(row => row.Class == "LOSS")}, DECL {losses.Count(row => row.Class == "DECL")}").AppendLine();
        text.AppendLine("| Pair | Feature | LOSS | DECL |").AppendLine("|---|---|---|---|");
        foreach (var group in losses.GroupBy(row => (Pair: Path.GetExtension(row.File).ToLowerInvariant() + "->" + row.Target, row.Feature))
                     .OrderBy(group => group.Key.Pair, StringComparer.Ordinal).ThenBy(group => group.Key.Feature, StringComparer.Ordinal))
            text.AppendLine($"| {group.Key.Pair} | {group.Key.Feature} | {group.Count(row => row.Class == "LOSS")} | {group.Count(row => row.Class == "DECL")} |");
        text.AppendLine().AppendLine("## Failures").AppendLine();
        foreach (var row in copies.Where(row => row.Class is not ("OK" or "READFAIL" or "NOTZIP")))
            text.AppendLine($"- {row.File} -> {row.Target}: {row.Class}");
        return text.ToString();
    }
}
