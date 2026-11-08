using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

if (args[0] == "--arity")
{
    var arityServices = new ServiceCollection();
    arityServices.AddSheetsEngine();
    using var arityProvider = arityServices.BuildServiceProvider();
    foreach (var definition in arityProvider.GetRequiredService<PulseOffice.Sheets.Engine.Evaluation.Functions.IFunctionRegistry>().All)
        Console.WriteLine($"{definition.Name}\t{definition.MinArguments}\t{definition.MaxArguments}");
    return;
}
if (args[0] == "--perf")
{
    var rows = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    using var sampler = Environment.GetEnvironmentVariable("ALLOC") == "1" ? new AllocationSampler() : null;
    var perfServices = new ServiceCollection();
    perfServices.AddSheetsEngine();
    using var perfProvider = perfServices.BuildServiceProvider();
    var perfBook = PulseOffice.Sheets.Workbook.CreateDefault();
    var sheet = perfBook.Sheets[0];
    for (var row = 0; row < rows; row++)
    {
        for (var column = 0; column < 8; column++)
            sheet.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(row, column), CellValue.Number(row + column));
        sheet.SetFormula(new PulseOffice.Sheets.Addressing.CellAddress(row, 8), $"=A{row + 1}*2+B{row + 1}");
        sheet.SetFormula(new PulseOffice.Sheets.Addressing.CellAddress(row, 9), $"=I{row + 1}+C{row + 1}");
    }
    for (var row = 0; row < 50; row++)
        sheet.SetFormula(new PulseOffice.Sheets.Addressing.CellAddress(row, 10), (Environment.GetEnvironmentVariable("PERF_FORMULA") ?? "=SUM(A:A)+SUMIF(B:B;\">\"&{0};C:C)+VLOOKUP({1};A:J;10;0)").Replace("{0}", row.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{1}", (row * 7).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    Console.WriteLine($"workbook={Retained() / 1048576}MB");
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var engine = perfProvider.GetRequiredService<ICalculationEngineFactory>().Create(perfBook);
    Console.WriteLine($"engine={Retained() / 1048576}MB");
    engine.RecalculateAll();
    Console.WriteLine($"cells={rows * 10} recalculateAll={clock.Elapsed.TotalSeconds:F2}s");
    sampler?.Print(15);
    var before = engine.GetValue(new PulseOffice.Sheets.SheetCell(0, new PulseOffice.Sheets.Addressing.CellAddress(0, 10)));
    clock.Restart();
    engine.SetValue(new PulseOffice.Sheets.SheetCell(0, new PulseOffice.Sheets.Addressing.CellAddress(5, 0)), CellValue.Number(42));
    var after = engine.GetValue(new PulseOffice.Sheets.SheetCell(0, new PulseOffice.Sheets.Addressing.CellAddress(0, 10)));
    Console.WriteLine($"K1 {before} -> {after}");
    Console.WriteLine($"edit={clock.Elapsed.TotalMilliseconds:F0}ms memory={Retained() / 1048576}MB");
    return;
}
if (args[0] == "--cell")
{
    using var cellInput = File.OpenRead(args[1]);
    var cellBook = new OdsSpreadsheetReader().Read(cellInput).Require();
    var cellSheet = cellBook.FindSheet(args[2])!;
    PulseOffice.Sheets.Addressing.CellAddress.TryParseA1(args[3], out var probe);
    Console.WriteLine(cellSheet[probe]);
    Console.WriteLine(cellBook.Calculation);
    var cellServices = new ServiceCollection();
    cellServices.AddSheetsEngine();
    using var cellProvider = cellServices.BuildServiceProvider();
    var cellEngine = cellProvider.GetRequiredService<ICalculationEngineFactory>().Create(cellBook);
    cellEngine.RecalculateAll();
    Console.WriteLine(cellEngine.GetValue(new PulseOffice.Sheets.SheetCell(cellBook.IndexOf(cellSheet), probe)));
    Console.WriteLine(cellSheet[probe]);
    return;
}
if (args[0] == "--dump")
{
    ISpreadsheetReader dumpReader = args[1].EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? new XlsxSpreadsheetReader() : new OdsSpreadsheetReader();
    using var dumpInput = File.OpenRead(args[1]);
    var dumpBook = dumpReader.Read(dumpInput).Require();
    var dumpServices = new ServiceCollection();
    dumpServices.AddSheetsEngine();
    using var dumpProvider = dumpServices.BuildServiceProvider();
    var dumpEngine = dumpProvider.GetRequiredService<ICalculationEngineFactory>().Create(dumpBook);
    dumpEngine.RecalculateAll();
    for (var index = 0; index < dumpBook.Sheets.Count; index++)
    {
        var dumpSheet = dumpBook.Sheets[index];
        if (dumpSheet.UsedRange() is not { } used) continue;
        for (var row = used.Start.Row; row <= used.End.Row; row++)
            for (var column = used.Start.Column; column <= used.End.Column; column++)
            {
                var address = new PulseOffice.Sheets.Addressing.CellAddress(row, column);
                var value = dumpEngine.GetValue(new PulseOffice.Sheets.SheetCell(index, address));
                if (value.IsEmpty) continue;
                var text = value.Kind switch
                {
                    ValueKind.Number => value.AsNumber().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    ValueKind.Boolean => value.AsBoolean() ? "1" : "0",
                    _ => value.ToString(),
                };
                Console.WriteLine($"{dumpSheet.Name}\t{address.ToA1()}\t{text}");
            }
    }
    return;
}
if (args[0] == "--pivot")
{
    var pivotBook = PulseOffice.Sheets.Workbook.CreateDefault();
    var pivotSource = pivotBook.Sheets[0];
    pivotSource.Name = "Dati vendite";
    string[] regions = ["Nord", "Sud", "Nord", "Centro", "Sud"];
    string[] months = ["Gen", "Gen", "Feb", "Feb", "Mar"];
    pivotSource.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), CellValue.Text("Regione"));
    pivotSource.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 1), CellValue.Text("Importo"));
    pivotSource.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 2), CellValue.Text("Mese"));
    for (var row = 0; row < regions.Length; row++)
    {
        pivotSource.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(row + 1, 0), CellValue.Text(regions[row]));
        pivotSource.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(row + 1, 1), CellValue.Number(10 * (row + 1)));
        pivotSource.SetValue(new PulseOffice.Sheets.Addressing.CellAddress(row + 1, 2), CellValue.Text(months[row]));
    }
    pivotBook.AddSheet("Pivot 1");
    pivotBook.Pivots.Add(new PulseOffice.Sheets.Pivot.PivotDefinition
    {
        Id = "p1",
        Name = "Riepilogo",
        SourceSheet = pivotSource.Name,
        SourceRange = new PulseOffice.Sheets.Addressing.CellRange(PulseOffice.Sheets.Addressing.CellAddress.First, new PulseOffice.Sheets.Addressing.CellAddress(5, 2)),
        DestinationSheet = "Pivot 1",
        Destination = new PulseOffice.Sheets.Addressing.CellAddress(2, 1),
        OutputRange = new PulseOffice.Sheets.Addressing.CellRange(new PulseOffice.Sheets.Addressing.CellAddress(2, 1), new PulseOffice.Sheets.Addressing.CellAddress(7, 4)),
        Fields =
        [
            new PulseOffice.Sheets.Pivot.PivotField { SourceName = "Regione", SourceColumnIndex = 0, Area = PulseOffice.Sheets.Pivot.PivotArea.Row, AreaOrder = 0, ExcludedValues = ["Sud"] },
            new PulseOffice.Sheets.Pivot.PivotField { SourceName = "Mese", SourceColumnIndex = 2, Area = PulseOffice.Sheets.Pivot.PivotArea.Column, AreaOrder = 0 },
            new PulseOffice.Sheets.Pivot.PivotField { SourceName = "Importo", SourceColumnIndex = 1, Area = PulseOffice.Sheets.Pivot.PivotArea.Value, Aggregation = PulseOffice.Sheets.Pivot.PivotAggregation.Sum },
        ],
    });
    using (var output = File.Create(Path.Combine(args[1], "pivot.xlsx"))) new XlsxSpreadsheetWriter().Write(pivotBook, output);
    return;
}
if (args[0] == "--freeze")
{
    var frozenBook = PulseOffice.Sheets.Workbook.CreateDefault();
    frozenBook.Sheets[0].Frozen = new PulseOffice.Sheets.FrozenPanes(2, 1);
    frozenBook.Sheets[0].SetValue(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), CellValue.Number(1));
    using (var output = File.Create(Path.Combine(args[1], "freeze.ods"))) new OdsSpreadsheetWriter().Write(frozenBook, output);
    using (var output = File.Create(Path.Combine(args[1], "freeze.xlsx"))) new XlsxSpreadsheetWriter().Write(frozenBook, output);
    return;
}
if (args[0] == "--spill")
{
    var spillServices = new ServiceCollection();
    spillServices.AddSheetsEngine();
    using var spillProvider = spillServices.BuildServiceProvider();
    var factory = spillProvider.GetRequiredService<ICalculationEngineFactory>();
    var source = PulseOffice.Sheets.Workbook.CreateDefault();
    var spillEngine = factory.Create(source);
    spillEngine.SetFormula(new PulseOffice.Sheets.SheetCell(0, new PulseOffice.Sheets.Addressing.CellAddress(0, 0)), "=SEQUENCE(3)");
    foreach (var (writer, format) in new (ISpreadsheetWriter, ISpreadsheetReader)[] { (new OdsSpreadsheetWriter(), new OdsSpreadsheetReader()), (new XlsxSpreadsheetWriter(), new XlsxSpreadsheetReader()) })
    {
        using var buffer = new MemoryStream();
        writer.Write(source, buffer);
        if (args.Length > 1) File.WriteAllBytes(Path.Combine(args[1], writer is OdsSpreadsheetWriter ? "spill.ods" : "spill.xlsx"), buffer.ToArray());
        buffer.Position = 0;
        var copy = format.Read(buffer).Require();
        var copyEngine = factory.Create(copy);
        copyEngine.RecalculateAll();
        Console.WriteLine($"{writer.GetType().Name}: A1={copyEngine.GetValue(new PulseOffice.Sheets.SheetCell(0, new PulseOffice.Sheets.Addressing.CellAddress(0, 0)))} A2={copyEngine.GetValue(new PulseOffice.Sheets.SheetCell(0, new PulseOffice.Sheets.Addressing.CellAddress(1, 0)))} A2formula={copy.Sheets[0][new PulseOffice.Sheets.Addressing.CellAddress(1, 0)].Formula ?? "-"}");
    }
    return;
}
if (args[0] == "--roundtrip")
{
    foreach (var file in File.ReadAllLines(args[1]))
    {
        try
        {
            ISpreadsheetReader source = file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? new XlsxSpreadsheetReader() : new OdsSpreadsheetReader();
            PulseOffice.Sheets.Workbook original;
            using (var stream = File.OpenRead(file))
            {
                var result = source.Read(stream);
                if (result.Value is not { } loaded) { Console.WriteLine($"{file}\tREAD-FAIL\t{string.Join(" ", result.Diagnostics.Select(d => d.Code))}"); continue; }
                original = loaded;
            }
            var reference = Facets(original);
            foreach (var (writer, back) in new (ISpreadsheetWriter, ISpreadsheetReader)[] { (new OdsSpreadsheetWriter(), new OdsSpreadsheetReader()), (new XlsxSpreadsheetWriter(), new XlsxSpreadsheetReader()) })
            {
                using var buffer = new MemoryStream();
                writer.Write(original, buffer);
                buffer.Position = 0;
                var copy = back.Read(buffer).Require();
                var declared = writer.CheckFidelity(original).Losses.Select(loss => loss.Code).ToHashSet();
                var facets = Facets(copy);
                foreach (var (name, values) in reference)
                {
                    var other = facets.TryGetValue(name, out var found) ? found : [];
                    if (values.SequenceEqual(other)) continue;
                    var first = values.Zip(other).FirstOrDefault(pair => pair.First != pair.Second);
                    var sample = first == default ? $"count {values.Count} -> {other.Count}: {string.Join(" ", values.Except(other).Take(4))}" : $"{first.First} -> {first.Second}";
                    Console.WriteLine($"{Path.GetFileName(file)}\t{(writer is OdsSpreadsheetWriter ? "ods" : "xlsx")}\t{name}\t{sample}\tdeclared={string.Join(",", declared)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{file}\tEXCEPTION\t{ex.GetType().Name}: {ex.Message}");
        }
    }
    return;
}
var culture = Environment.GetEnvironmentVariable("PULSE_CULTURE") is { Length: > 0 } fixedCulture ? fixedCulture : "en-US";
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
var seed = int.TryParse(Environment.GetEnvironmentVariable("PULSE_SEED"), out var parsedSeed) ? parsedSeed : 20261017;
var corpus = args[0] == "--corpus";
var paths = corpus
    ? Directory.EnumerateFiles(args[1], "*.fods", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList()
    : [args[0]];
var toleranceArgument = corpus ? 2 : 1;
var tolerance = args.Length > toleranceArgument ? double.Parse(args[toleranceArgument], System.Globalization.CultureInfo.InvariantCulture) : 1e-9;
string[] volatileFunctions = ["RAND(", "RANDBETWEEN(", "RANDARRAY(", "RAND.NV(", "RANDBETWEEN.NV(", "NOW(", "INFO("];
var totals = (Checked: 0, Differences: 0, Volatile: 0, Failed: 0);
var punctuationOnly = 0;
var summaries = 0;
foreach (var path in paths)
{
    try
    {
        var (checkedCells, differences, skipped) = Compare(path);
        totals = (totals.Checked + checkedCells, totals.Differences + differences, totals.Volatile + skipped, totals.Failed);
        if (!corpus) Console.WriteLine($"checked={checkedCells} differences={differences} volatile={skipped}");
    }
    catch (Exception ex)
    {
        totals.Failed++;
        Console.WriteLine($"{path}\tEXCEPTION\t{ex.GetType().Name}: {ex.Message}");
    }
}
if (corpus) Console.WriteLine($"files={paths.Count} checked={totals.Checked} differences={totals.Differences} summaries={summaries} true={totals.Differences - summaries} punctuationOnly={punctuationOnly} volatile={totals.Volatile} failed={totals.Failed} culture={culture} seed={seed}");

bool PunctuationOnly(CellValue reference, CellValue mine)
{
    if (reference.Kind != ValueKind.Text || mine.Kind != ValueKind.Text) return false;
    var (a, b) = (reference.AsText(), mine.AsText());
    if (!a.TrimStart('{').StartsWith('=') || !b.TrimStart('{').StartsWith('=')) return false;
    string Plain(string text) => new([.. text.Where(character => !char.IsWhiteSpace(character)).Select(character => character is ',' or ';' or '.' ? ';' : char.ToUpperInvariant(character))]);
    return Plain(a) == Plain(b);
}

(int Checked, int Differences, int Volatile) Compare(string path)
{
    ISpreadsheetReader reader = path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? new XlsxSpreadsheetReader() : new OdsSpreadsheetReader();
    using var input = File.OpenRead(path);
    var book = reader.Read(input).Require();
    var expected = book.Sheets
        .SelectMany(sheet => sheet.NonEmptyCells().Where(entry => entry.Value.HasFormula || entry.Value.IsPartOfArrayFormula).Select(entry => (sheet, entry.Key, entry.Value)))
        .ToList();
    var services = new ServiceCollection();
    services.AddSingleton<TimeProvider>(new FixedTimeProvider(SavedAt(path)));
    services.AddSingleton<PulseOffice.Sheets.Engine.Evaluation.Functions.Providers.IRandomSource>(new PulseOffice.Sheets.Engine.Evaluation.Functions.Providers.SeededRandomSource(seed));
    services.AddSheetsEngine();
    using var provider = services.BuildServiceProvider();
    var compared = provider.GetRequiredService<ICalculationEngineFactory>().Create(book);
    compared.Pivots = new PulseOffice.Sheets.Pivot.PivotDataLookup(book,
        new PulseOffice.Sheets.Pivot.PivotEngine(new PulseOffice.Sheets.Pivot.PivotAggregator(), new PulseOffice.Sheets.Pivot.PivotGrouping(), new PulseOffice.Sheets.Pivot.PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser())),
        new PulseOffice.Sheets.Pivot.PivotLabels("Total Result", "{0} Result"));
    compared.RecalculateAll();
    var differences = 0;
    var skipped = 0;
    var randomSheets = expected.Where(item => item.Value.Formula is { } text && text.Contains("RANDARRAY(", StringComparison.OrdinalIgnoreCase)).Select(item => item.sheet.Name).ToHashSet();
    foreach (var (sheet, address, cell) in expected)
    {
        if (cell.Formula is { } formula && volatileFunctions.Any(name => formula.Contains(name, StringComparison.OrdinalIgnoreCase))
            || cell.Formula is null && randomSheets.Contains(sheet.Name))
        {
            skipped++;
            continue;
        }
        var actual = sheet[address].Value;
        if (Agrees(cell.Value, actual)) continue;
        if (PunctuationOnly(cell.Value, actual)) { punctuationOnly++; continue; }
        if (cell.Formula is { } summary && summary.StartsWith("=AND(", StringComparison.OrdinalIgnoreCase)) summaries++;
        differences++;
        Console.WriteLine($"{(corpus ? Path.GetFileName(path) + "\t" : string.Empty)}{sheet.Name}!{address}\t{cell.Formula}\tLO={Show(cell.Value)}\tPulse={Show(actual)}");
    }
    return (expected.Count - skipped, differences, skipped);
}

DateTimeOffset SavedAt(string path)
{
    var fallback = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    if (!path.EndsWith(".fods", StringComparison.OrdinalIgnoreCase)) return fallback;
    using var reader = System.Xml.XmlReader.Create(path, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit });
    while (reader.Read())
    {
        if (reader.NodeType != System.Xml.XmlNodeType.Element) continue;
        if (reader.LocalName is "date" or "creation-date" && reader.NamespaceURI.Contains("dc/elements", StringComparison.Ordinal) || reader.LocalName == "date" && reader.Prefix == "dc")
            return DateTimeOffset.TryParse(reader.ReadElementContentAsString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var saved) ? saved : fallback;
        if (reader.LocalName == "body") break;
    }
    return fallback;
}

bool Agrees(CellValue reference, CellValue mine)
{
    if (reference.IsError || mine.IsError) return reference.IsError && mine.IsError && reference.ErrorText == mine.ErrorText;
    if (IsBlankText(reference) && IsBlankText(mine)) return true;
    if (reference.Kind == ValueKind.Text || mine.Kind == ValueKind.Text) return reference.AsText() == mine.AsText() && reference.Kind == mine.Kind;
    if (reference.IsEmpty && mine.IsEmpty) return true;
    var a = reference.AsNumber();
    var b = mine.AsNumber();
    return Math.Abs(a - b) <= tolerance * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
}

bool IsBlankText(CellValue value) => value.IsEmpty || (value.Kind == ValueKind.Text && value.AsText().Length == 0);

string Show(CellValue value) => value.IsError ? value.ErrorText : $"{value.Kind}:{value}";

Dictionary<string, List<string>> Facets(PulseOffice.Sheets.Workbook book)
{
    var facets = new Dictionary<string, List<string>>();
    void Add(string name, string value) => (facets.TryGetValue(name, out var list) ? list : facets[name] = []).Add(value);
    foreach (var sheet in book.Sheets)
    {
        Add("sheets", $"{sheet.Name}|hidden={sheet.IsHidden}|tab={sheet.TabColorHex}");
        foreach (var (address, cell) in sheet.NonEmptyCells().OrderBy(pair => pair.Key))
        {
            if (!cell.Value.IsEmpty || cell.HasFormula)
                Add("values", $"{sheet.Name}!{address}={(cell.Value.Kind == ValueKind.Number ? cell.Value.AsNumber().ToString("G15", System.Globalization.CultureInfo.InvariantCulture) : cell.Value.ToString())}");
            if (cell.HasFormula) Add("formulas", $"{sheet.Name}!{address}{cell.Formula}");
            if (cell.Hyperlink is not null) Add("links", $"{sheet.Name}!{address}{cell.Hyperlink}");
            var style = book.Styles.Get(sheet.StyleIdAt(address));
            Add("styles", $"{sheet.Name}!{address} b={style.Font.Bold} i={style.Font.Italic} u={style.Font.Underline} bg={style.BackgroundHex?.ToUpperInvariant()} fg={style.Font.ColorHex.ToUpperInvariant()} fmt={book.NumberFormats.CodeOf(style.NumberFormatId)} h={style.Horizontal} wrap={style.WrapText} border={style.Borders.Left.Style}/{style.Borders.Top.Style}/{style.Borders.Right.Style}/{style.Borders.Bottom.Style}");
        }
        foreach (var merge in sheet.Merges.All.OrderBy(range => range.Start)) Add("merges", $"{sheet.Name}!{merge}");
        foreach (var comment in sheet.Comments.OrderBy(pair => pair.Key)) Add("comments", $"{sheet.Name}!{comment.Key}={comment.Value.Text}");
        Add("validations", $"{sheet.Name}:{sheet.Validations.Count}");
        Add("conditional", $"{sheet.Name}:{sheet.ConditionalRules.Count}");
        Add("protection", $"{sheet.Name}:{sheet.Protection.IsEnabled}");
        Add("objects", $"{sheet.Name}:{string.Join(",", sheet.Objects.Select(placed => placed.Content.GetType().Name).Order())}");
        Add("autofilter", $"{sheet.Name}:{sheet.AutoFilter.Range}");
    }
    foreach (var name in book.DefinedNames.OrderBy(name => name.Name).ThenBy(name => name.Scope)) Add("names", $"{name.Scope}:{name.Name}={name.Expression}");
    Add("pivots", book.Pivots.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    return facets;
}

long Retained()
{
    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    var info = GC.GetGCMemoryInfo(GCKind.FullBlocking);
    return info.HeapSizeBytes - info.FragmentedBytes;
}

sealed class FixedTimeProvider(DateTimeOffset moment) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => moment.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("Fixed", moment.Offset, "Fixed", "Fixed");
}
