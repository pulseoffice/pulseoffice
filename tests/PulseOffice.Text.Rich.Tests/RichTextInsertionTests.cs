using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public sealed class RichTextInsertionTests
{
    private readonly RichTextInsertion _insertion = new();

    private static RichText Text(params string[] lines) =>
        new(PlaceholderRole.Free, [.. lines.Select(line => new TextParagraph(line).WithRuns([new TextRun(line)]))]);

    [Fact]
    public void Un_paragrafo_si_inserisce_al_cursore_tenendo_i_suoi_attributi()
    {
        var result = _insertion.Insert(Text("abcd"), (2, 2), [new TextParagraph("X").WithRuns([new TextRun("X") { Bold = true }])]);
        Assert.Equal("abXcd", result.PlainText);
        Assert.Equal(["ab", "X", "cd"], result.Paragraphs[0].Runs.Select(run => run.Text));
        Assert.True(result.Paragraphs[0].Runs[1].Bold);
    }

    [Fact]
    public void Piu_paragrafi_spezzano_quello_di_destinazione_e_sostituiscono_la_selezione()
    {
        var fragment = new[]
        {
            new TextParagraph("uno").WithRuns([new TextRun("uno") { Italic = true }]),
            new TextParagraph("due").WithRuns([new TextRun("due")]),
            new TextParagraph("tre").WithRuns([new TextRun("tre") { Underline = true }]),
        };
        var result = _insertion.Insert(Text("prima", "dopo"), (2, 9), fragment);
        Assert.Equal("pruno\ndue\ntreo", result.PlainText);
        Assert.True(result.Paragraphs[0].Runs[1].Italic);
        Assert.True(result.Paragraphs[2].Runs[0].Underline);
        Assert.Equal("o", result.Paragraphs[2].Runs[^1].Text);
    }

    [Fact]
    public void Senza_intervallo_si_accoda_in_fondo()
    {
        var result = _insertion.Insert(Text("a"), null, [new TextParagraph("b").WithRuns([new TextRun("b")])]);
        Assert.Equal("ab", result.PlainText);
        Assert.Single(result.Paragraphs[0].Runs);
    }

    [Fact]
    public void Molte_parti_con_lo_stesso_formato_si_fondono_in_tempo_lineare()
    {
        var runs = Enumerable.Range(0, 200_000).Select(index => new TextRun(index % 2 == 0 ? "ab" : "c")).ToList();
        var fragment = new TextParagraph(string.Concat(runs.Select(run => run.Text))).WithRuns(runs);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = _insertion.Insert(Text("x"), null, [fragment]);
        watch.Stop();

        Assert.Single(result.Paragraphs[0].Runs);
        Assert.Equal(1 + 300_000, result.Paragraphs[0].Runs[0].Text.Length);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"merging took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void I_campi_restano_parti_a_se_e_il_formato_diverso_interrompe_la_fusione()
    {
        var fragment = new TextParagraph("12#34").WithRuns(
        [
            new TextRun("1"), new TextRun("2"), new TextRun("#") { Field = TextFieldKind.SlideNumber },
            new TextRun("3"), new TextRun("4") { Bold = true }, new TextRun(string.Empty),
        ]);

        var result = _insertion.Insert(Text("a"), null, [fragment]);

        Assert.Equal(["a12", "#", "3", "4"], result.Paragraphs[0].Runs.Select(run => run.Text));
        Assert.Equal(TextFieldKind.SlideNumber, result.Paragraphs[0].Runs[1].Field);
        Assert.True(result.Paragraphs[0].Runs[3].Bold);
    }
}
