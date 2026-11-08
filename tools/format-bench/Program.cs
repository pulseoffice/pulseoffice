using System.Diagnostics;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

var mode = args[0];
var format = args[1];
var path = args[2];
if (format is "odp" or "pptx" or "odt" or "docx")
{
    using var sampler = Environment.GetEnvironmentVariable("ALLOC") == "1" ? new AllocationSampler() : null;
    new LongDocumentBench().Run(mode, format, path, args.Length > 3 ? int.Parse(args[3]) : 1000);
    sampler?.Print(25);
    return;
}
var rows = mode == "write" && args.Length > 3 ? int.Parse(args[3]) : 500_000;

ISpreadsheetWriter writer = format == "ods" ? new OdsSpreadsheetWriter() : new XlsxSpreadsheetWriter();
ISpreadsheetReader reader = format == "ods" ? new OdsSpreadsheetReader() : new XlsxSpreadsheetReader();

var clock = Stopwatch.StartNew();
if (mode == "dump")
{
    foreach (var file in File.ReadAllLines(path))
    {
        try
        {
            using var input = File.OpenRead(file);
            var result = reader.Read(input);
            Console.WriteLine($"{file}\t{(result.Value is { } book ? Summary(book) : "FAIL")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{file}\tEXCEPTION {ex.GetType().Name}");
        }
    }
    return;
}
if (mode == "resaveall")
{
    foreach (var file in File.ReadAllLines(path))
    {
        ISpreadsheetReader source = file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? new XlsxSpreadsheetReader() : new OdsSpreadsheetReader();
        var target = Path.Combine(args[3], Path.GetFileNameWithoutExtension(file) + "." + format);
        try
        {
            using var input = File.OpenRead(file);
            var result = source.Read(input);
            if (result.Value is not { } book) { Console.WriteLine($"{file}\tFAIL"); continue; }
            using var output = File.Create(target);
            writer.Write(book, output);
            Console.WriteLine($"{file}\tOK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{file}\tEXCEPTION {ex.GetType().Name} {ex.Message}");
        }
    }
    return;
}
if (mode == "resave")
{
    using (var input = File.OpenRead(path))
    using (var output = File.Create(args[3]))
        writer.Write(reader.Read(input).Require(), output);
    return;
}
if (mode == "present")
{
    using var input = File.OpenRead(path);
    var book = reader.Read(input).Require();
    var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
    PulseOffice.Sheets.Engine.Evaluation.Functions.SheetsEngineServiceCollectionExtensions.AddSheetsEngine(services);
    using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
    var engine = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
        .GetRequiredService<PulseOffice.Sheets.Engine.Recalculation.ICalculationEngineFactory>(provider).Create(book);
    engine.RecalculateAll();
    var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
        System.Globalization.CultureInfo.GetCultureInfo("en-US"), serial => new DateTime(1899, 12, 30).AddDays(serial)));
    var sheet = book.Sheets[0];
    for (var row = 0; row <= (sheet.UsedRange()?.End.Row ?? -1); row++)
    {
        var cells = new List<string>();
        for (var column = 0; column <= (sheet.UsedRange()?.End.Column ?? -1); column++)
        {
            var address = new CellAddress(row, column);
            var cell = sheet[address] with { StyleId = sheet.StyleIdAt(address) };
            cells.Add(presenter.Present(cell, book.Styles, book.NumberFormats, 30).Text);
        }
        Console.WriteLine(string.Join("\t", cells));
    }
    return;
}
if (mode == "write")
{
    var workbook = Synthetic(rows);
    var built = clock.Elapsed;
    var modelBytes = GC.GetTotalMemory(true);
    clock.Restart();
    using (var stream = File.Create(path)) writer.Write(workbook, stream);
    Report($"write {format} rows={rows} build={built.TotalSeconds:F1}s model={modelBytes / 1048576}MB");
}
else
{
    using var stream = File.OpenRead(path);
    var result = reader.Read(stream);
    Report($"read {format} ok={result.Succeeded} cells={result.Value?.Sheets[0].CellCount}");
}

void Report(string label)
{
    var process = Process.GetCurrentProcess();
    process.Refresh();
    Console.WriteLine($"{label} time={clock.Elapsed.TotalSeconds:F1}s peak={process.PeakWorkingSet64 / 1048576}MB allocated={GC.GetTotalAllocatedBytes() / 1048576}MB file={new FileInfo(path).Length / 1048576}MB");
}

static Workbook Synthetic(int rows)
{
    var workbook = new Workbook();
    var sheet = workbook.AddSheet("Data");
    for (var row = 0; row < rows; row++)
    {
        for (var column = 0; column < 8; column++)
            sheet.SetValue(new CellAddress(row, column), CellValue.Number(row * 10 + column + 0.25));
        sheet.SetValue(new CellAddress(row, 8), CellValue.Text($"Item {row % 997}"));
        sheet.SetValue(new CellAddress(row, 9), CellValue.Text($"Region {row % 7}"));
    }
    return workbook;
}

static string Summary(Workbook book)
{
    var hash = new System.Text.StringBuilder();
    foreach (var sheet in book.Sheets)
    {
        hash.Append(sheet.Name).Append('|').Append(sheet.CellCount).Append('|');
        foreach (var (address, cell) in sheet.NonEmptyCells())
            hash.Append(address).Append(cell.Value).Append(cell.Formula).Append(book.Styles.Get(cell.StyleId)).Append(book.Styles.Get(cell.StyleId).NumberFormatId >= 164 ? book.NumberFormats.Get(book.Styles.Get(cell.StyleId).NumberFormatId).Code : "").Append(cell.ExplicitStyle)
                .Append(cell.Hyperlink).Append(cell.StyleName).Append(cell.ArrayFormulaRange).Append(';');
        hash.Append("|M").Append(string.Join(",", sheet.Merges.All));
        hash.Append("|C").Append(string.Join(",", sheet.Comments.OrderBy(c => c.Key).Select(c => c.Key + c.Value.Text)));
        hash.Append("|O").Append(string.Join(",", sheet.Objects.Select(o => o.Content.GetType().Name + o.Anchor + o.Id)));
        hash.Append("|H").Append(string.Join(",", sheet.Layout.RowHeights.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")));
        hash.Append("|A").Append(string.Join(",", sheet.Layout.AutomaticRowHeights.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")));
        hash.Append("|W").Append(string.Join(",", sheet.Layout.ColumnWidths.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")));
        hash.Append("|RS").Append(string.Join(",", sheet.Layout.RowStyles.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")));
        hash.Append("|CS").Append(string.Join(",", sheet.Layout.ColumnStyles.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}")));
        hash.Append("|V").Append(sheet.Validations.Count).Append("|R").Append(sheet.ConditionalRules.Count)
            .Append("|G").Append(sheet.RowGroups.Count).Append(sheet.ColumnGroups.Count)
            .Append("|P").Append(string.Join(",", sheet.PrintRanges)).Append(sheet.RepeatedPrintRows).Append(sheet.RepeatedPrintColumns)
            .Append("|S").Append(sheet.Sparklines.ToString()).Append(sheet.Scenarios.Count)
            .Append("|B").Append(string.Join(",", sheet.RowBreaks)).Append(string.Join(",", sheet.ColumnBreaks)).Append(sheet.IsHidden).Append(sheet.TabColorHex);
        hash.Append("||");
    }
    hash.Append("N").Append(book.DefinedNames.Count()).Append("P").Append(book.Pivots.Count).Append("X").Append(book.ExternalData.AreaLinks.Count);
    if (Environment.GetEnvironmentVariable("DETAIL") is not null) return hash.ToString().Replace(";", ";\n").Replace("|", "\n|");
    var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hash.ToString()));
    return $"{book.Sheets.Count} {book.Sheets.Sum(s => s.CellCount)} {Convert.ToHexString(bytes)[..16]}";
}
