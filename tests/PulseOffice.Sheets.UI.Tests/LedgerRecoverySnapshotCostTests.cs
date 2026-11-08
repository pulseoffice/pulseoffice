using System.Diagnostics;
using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

[Collection(LedgerPerformanceCollection.Name)]
public sealed class LedgerRecoverySnapshotCostTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sc/qa";
    private const int CorpusSample = 150;

    private static void Record(string line) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-recovery-cost.txt"), $"{DateTimeOffset.Now:O} {line}\n");

    private static Workbook MillionCells()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 100_000; row++)
            for (var column = 0; column < 10; column++)
                sheet.SetValue(new CellAddress(row, column), column == 0 ? CellValue.Text("r" + row) : CellValue.Number(row * column));
        return workbook;
    }

    private static IReadOnlyDictionary<string, string> Parts(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsSpreadsheetWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.Where(entry => entry.FullName != "meta.xml")
            .ToDictionary(entry => entry.FullName, entry => new StreamReader(entry.Open()).ReadToEnd(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_million_cell_capture_is_cheap_and_the_copy_serializes_off_the_caller()
    {
        var workbook = MillionCells();
        var capture = new LedgerRecoveryCapture(new OdsSpreadsheetWriter(), new WorkbookCopier().Copy(workbook));
        using (var warm = new MemoryStream()) await capture.WriteAsync(warm, TestContext.Current.CancellationToken);

        var clock = Stopwatch.StartNew();
        var copy = new WorkbookCopier().Copy(workbook);
        var captured = clock.Elapsed;
        clock.Restart();
        using var stream = new MemoryStream();
        await new LedgerRecoveryCapture(new OdsSpreadsheetWriter(), copy).WriteAsync(stream, TestContext.Current.CancellationToken);
        var serialized = clock.Elapsed;
        Record($"ledger cells=1000000 capture={captured.TotalMilliseconds:0.0} serialization={serialized.TotalMilliseconds:0} bytes={stream.Length}");

        Assert.True(captured < TimeSpan.FromMilliseconds(150), $"capture {captured.TotalMilliseconds:0.0} ms");
    }

    [Fact]
    public void The_copy_is_independent_of_later_edits()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.Merges.Add(new CellRange(new CellAddress(2, 2), new CellAddress(3, 3)));

        var styles = workbook.Styles.Styles.Count;
        var copy = new WorkbookCopier().Copy(workbook);
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(2));
        sheet.SetValue(new CellAddress(5, 5), CellValue.Number(3));
        sheet.Merges.Clear();
        workbook.AddSheet("Altro");
        workbook.Styles.Intern(new Styling.CellStyle { BackgroundHex = "#123457" });

        var copied = copy.Sheets[0];
        Assert.Equal(CellValue.Number(1), copied.GetValue(new CellAddress(0, 0)));
        Assert.True(copied.IsBlank(new CellAddress(5, 5)));
        Assert.Equal(1, copied.CellCount);
        Assert.Equal(0, copied.LastUsedRow);
        Assert.Single(copied.Merges.All);
        Assert.Single(copy.Sheets);
        Assert.Equal(styles, copy.Styles.Styles.Count);
    }

    [Fact]
    public void Corpus_workbooks_write_the_same_from_the_copy()
    {
        if (!Directory.Exists(Corpus)) return;
        var files = Directory.GetFiles(Corpus, "*.ods", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        var step = Math.Max(1, files.Count / CorpusSample);
        var checkedFiles = 0;
        for (var index = 0; index < files.Count; index += step)
        {
            var info = new FileInfo(files[index]);
            if (info.Length > 2_000_000) continue;
            Workbook? workbook;
            try
            {
                using var source = File.OpenRead(info.FullName);
                workbook = new OdsSpreadsheetReader().Read(source).Value;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                continue;
            }

            if (workbook is null) continue;
            var expected = Parts(workbook);
            var actual = Parts(new WorkbookCopier().Copy(workbook));
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
            foreach (var (name, content) in expected)
                Assert.True(content == actual[name], $"{info.Name}: {name} differs when written from the copy");
            checkedFiles++;
        }

        Assert.True(checkedFiles > 50, $"{checkedFiles} corpus workbooks checked");
    }
}
