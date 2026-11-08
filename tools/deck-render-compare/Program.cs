using System.Globalization;
using SkiaSharp;

var arguments = new CompareArguments(args);
var measure = new SlideSimilarity(new Thumbnailer(arguments.Width));
var report = new CorpusReport(arguments.Threshold, arguments.CorpusThreshold);

foreach (var reference in Directory.EnumerateFiles(arguments.ReferenceDirectory, "*.png").Order(StringComparer.Ordinal))
{
    var ours = Path.Combine(arguments.OursDirectory, Path.GetFileName(reference));
    if (!File.Exists(ours))
    {
        report.Missing(Path.GetFileNameWithoutExtension(reference));
        continue;
    }
    report.Add(Path.GetFileNameWithoutExtension(reference), measure.Compare(reference, ours));
}

if (arguments.SheetPath is { } sheet) new ContactSheet(arguments.Threshold).Write(sheet, arguments.ReferenceDirectory, arguments.OursDirectory, report.Scores);
return report.Print(Console.Out);

internal sealed class CompareArguments
{
    public CompareArguments(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("usage: deck-render-compare <reference-dir> <ours-dir> [slide-threshold] [corpus-threshold] [width]");
        ReferenceDirectory = args[0];
        OursDirectory = args[1];
        Threshold = args.Length > 2 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 0.80;
        CorpusThreshold = args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 0.92;
        Width = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 96;
        SheetPath = args.Length > 5 ? args[5] : null;
    }

    public string? SheetPath { get; }

    public string ReferenceDirectory { get; }

    public string OursDirectory { get; }

    public double Threshold { get; }

    public double CorpusThreshold { get; }

    public int Width { get; }
}

internal sealed class Thumbnailer(int width)
{
    private const int Height = 54;

    public byte[] Gray(string path)
    {
        using var source = SKBitmap.Decode(path) ?? throw new InvalidDataException(path);
        using var scaled = source.Resize(new SKImageInfo(width, Height * width / 96, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell));
        var pixels = new byte[scaled.Width * scaled.Height];
        for (var y = 0; y < scaled.Height; y++)
            for (var x = 0; x < scaled.Width; x++)
            {
                var colour = scaled.GetPixel(x, y);
                var alpha = colour.Alpha / 255.0;
                pixels[y * scaled.Width + x] = (byte)Math.Round((0.299 * colour.Red + 0.587 * colour.Green + 0.114 * colour.Blue) * alpha + 255 * (1 - alpha));
            }
        return pixels;
    }
}

internal sealed class SlideSimilarity(Thumbnailer thumbnails)
{
    private const int Tolerance = 40;

    public double Compare(string reference, string ours)
    {
        var expected = thumbnails.Gray(reference);
        var actual = thumbnails.Gray(ours);
        var differing = expected.Zip(actual).Count(pair => Math.Abs(pair.First - pair.Second) > Tolerance);
        return 1.0 - (double)differing / expected.Length;
    }
}

internal sealed class CorpusReport(double threshold, double corpusThreshold)
{
    private readonly List<(string Name, double Score)> _scores = [];
    private readonly List<string> _missing = [];

    public IReadOnlyList<(string Name, double Score)> Scores => _scores;

    public void Add(string name, double score) => _scores.Add((name, score));

    public void Missing(string name) => _missing.Add(name);

    public int Print(TextWriter output)
    {
        foreach (var (name, score) in _scores.OrderBy(entry => entry.Score))
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{score:0.000}\t{name}{(score < threshold ? "\tBELOW" : string.Empty)}"));
        foreach (var name in _missing) output.WriteLine($"missing\t{name}");

        var mean = _scores.Count == 0 ? 0 : _scores.Average(entry => entry.Score);
        var below = _scores.Count(entry => entry.Score < threshold);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"slides {_scores.Count}, missing {_missing.Count}, mean {mean:0.000}, below {threshold:0.00}: {below}"));
        return mean >= corpusThreshold && below == 0 ? 0 : 1;
    }
}

internal sealed class ContactSheet(double threshold)
{
    private const int Cell = 240;
    private const int CellHeight = 150;
    private const int Label = 16;

    public void Write(string path, string referenceDirectory, string oursDirectory, IReadOnlyList<(string Name, double Score)> scores)
    {
        var below = scores.Where(entry => entry.Score < threshold).OrderBy(entry => entry.Score).ToList();
        if (below.Count == 0) return;
        using var surface = SKSurface.Create(new SKImageInfo(Cell * 2 + 10, below.Count * (CellHeight + Label)));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.LightGray);
        using var font = new SKFont(SKTypeface.Default, 12);
        using var ink = new SKPaint { Color = SKColors.Black };
        for (var row = 0; row < below.Count; row++)
        {
            var top = row * (CellHeight + Label);
            canvas.DrawText(string.Create(CultureInfo.InvariantCulture, $"{below[row].Score:0.000} {below[row].Name}"), 4, top + 12, font, ink);
            Draw(canvas, Path.Combine(referenceDirectory, below[row].Name + ".png"), 0, top + Label);
            Draw(canvas, Path.Combine(oursDirectory, below[row].Name + ".png"), Cell + 10, top + Label);
        }
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(path, data.ToArray());
    }

    private void Draw(SKCanvas canvas, string path, int left, int top)
    {
        using var bitmap = SKBitmap.Decode(path);
        if (bitmap is null) return;
        canvas.DrawBitmap(bitmap, new SKRect(left, top, left + Cell, top + CellHeight));
    }
}
