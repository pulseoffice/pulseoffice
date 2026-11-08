using System.Diagnostics;
using System.IO.Compression;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class DeckRecoveryCopyTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sd/qa";

    private static void Record(string line) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-recovery-cost.txt"), $"{DateTimeOffset.Now:O} {line}\n");

    private static Presentation Deck(int count)
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Find("Slate")!);
        var deck = new Presentation { Master = master };
        var slides = new SlideFactory();
        for (var i = 0; i < count; i++)
        {
            var slide = slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
            var title = slide.Placeholder(PlaceholderRole.Title)!;
            slide.Replace(title with { Text = title.Text.WithPlainText("Slide " + i) });
            var body = slide.Placeholder(PlaceholderRole.Body)!;
            slide.Replace(body with { Text = body.Text.WithPlainText("First point\nSecond point\nThird point") });
            deck.AddExisting(slide);
        }

        return deck;
    }

    private static IReadOnlyDictionary<string, string> Parts(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.Where(entry => entry.FullName != "meta.xml")
            .ToDictionary(entry => entry.FullName, entry => Convert.ToBase64String(Bytes(entry)), StringComparer.Ordinal);
    }

    private static byte[] Bytes(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        using var target = new MemoryStream();
        source.CopyTo(target);
        return target.ToArray();
    }

    [Fact]
    public void Two_hundred_slides_copy_cheaply_for_the_recovery_snapshot()
    {
        var deck = Deck(200);
        using (var warm = new MemoryStream()) new OdpWriter().Write(deck.Copy(), warm);

        var clock = Stopwatch.StartNew();
        var copy = deck.Copy();
        var captured = clock.Elapsed;
        clock.Restart();
        using var stream = new MemoryStream();
        new OdpWriter().Write(copy, stream);
        var serialized = clock.Elapsed;
        Record($"deck slides=200 capture={captured.TotalMilliseconds:0.00} serialization={serialized.TotalMilliseconds:0} bytes={stream.Length}");

        Assert.True(captured < TimeSpan.FromMilliseconds(20), $"capture {captured.TotalMilliseconds:0.00} ms");
    }

    [Fact]
    public void The_copy_is_independent_of_later_edits()
    {
        var deck = Deck(2);
        deck.Grid.AddGuide(new SlideGuide(true, 100));
        var copy = deck.Copy();

        var first = deck.Slides[0];
        var title = first.Placeholder(PlaceholderRole.Title)!;
        first.Replace(title with { Text = title.Text.WithPlainText("Changed") });
        first.Notes = "changed";
        deck.Remove(deck.Slides[1]);
        deck.Grid.ClearGuides();

        Assert.Equal(2, copy.Slides.Count);
        Assert.Equal("Slide 0", copy.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
        Assert.Equal(string.Empty, copy.Slides[0].Notes);
        Assert.Single(copy.Grid.Guides);
    }

    [Fact]
    public void Corpus_presentations_write_the_same_from_the_copy()
    {
        if (!Directory.Exists(Corpus)) return;
        var reader = new OdpReader(new ThemeCatalog(), new MasterFactory());
        var checkedFiles = 0;
        foreach (var file in Directory.GetFiles(Corpus, "*.odp", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var info = new FileInfo(file);
            if (info.Length > 2_000_000) continue;
            Presentation? deck;
            try
            {
                using var source = File.OpenRead(file);
                deck = reader.Read(source).Value;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                continue;
            }

            if (deck is null) continue;
            var expected = Parts(deck);
            var actual = Parts(deck.Copy());
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
            foreach (var (name, content) in expected)
                Assert.True(content == actual[name], $"{info.Name}: {name} differs when written from the copy");
            checkedFiles++;
        }

        Assert.True(checkedFiles > 50, $"{checkedFiles} corpus presentations checked");
    }
}
