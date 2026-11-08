using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class HtmlFromOtherAppsTests
{
    private readonly TextInterchange _formats = new();

    private IReadOnlyList<TextParagraph> Paragraphs(string html) =>
        [.. TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader).Body.Blocks.OfType<ParagraphBlock>().Select(block => block.Content)];

    [Fact]
    public void Gli_stili_calcolati_di_una_pagina_web_non_portano_sfondi_del_sito_caratteri_di_sistema_misure_assurde()
    {
        var paragraphs = Paragraphs("""
            <html><body><!--StartFragment--><span style="color: rgb(32, 33, 36); font-family: -apple-system, BlinkMacSystemFont, &quot;Segoe UI&quot;, Roboto, sans-serif; font-size: 16px; background-color: rgb(255, 255, 255); display: inline !important;">Sito</span>
            <h2 style="margin: 0px 0px 24px; background-color: rgba(0, 0, 0, 0); font-family: var(--font-heading)">Titolo</h2>
            <p style="margin-left:-40px; margin-top: -12px; background-color: rgb(250,250,250); font-size: 900px">Testo <span style="font-size: 0.1px; background: rgb(0 0 0 / 0%)">minuscolo</span> <span style="font-family: system-ui, sans-serif; color: transparent">sistema</span></p><!--EndFragment--></body></html>
            """);

        var runs = paragraphs.SelectMany(paragraph => paragraph.Runs).Where(run => run.Text.Trim().Length > 0).ToList();
        Assert.Equal("Segoe UI", runs[0].Font);
        Assert.Equal(12, runs[0].FontSize);
        Assert.Equal(0xFF202124u, runs[0].Color);
        Assert.All(runs, run => Assert.Null(run.Highlight));
        Assert.All(runs, run => Assert.True(run.FontSize is null or (>= 1 and <= 400)));
        Assert.All(runs.Skip(1), run => Assert.Null(run.Font));
        Assert.All(paragraphs, paragraph => Assert.Null(paragraph.Background));
        Assert.All(paragraphs, paragraph => Assert.True(paragraph.SpaceBefore >= 0 && (paragraph.MarginLeft ?? 0) >= 0));
    }

    [Fact]
    public void Google_Docs_e_le_altre_suite_tengono_carattere_grassetto_e_sfondi_voluti()
    {
        var paragraphs = Paragraphs("""
            <meta charset="utf-8"><b style="font-weight:normal;" id="docs-internal-guid-1"><p dir="ltr" style="line-height:1.38;margin-top:0pt;margin-bottom:0pt;"><span style="font-size:11pt;font-family:Arial,sans-serif;color:#000000;background-color:transparent;font-weight:400;">Ciao </span><span style="font-size:11pt;font-family:Arial,sans-serif;background-color:#ffff00;font-weight:700;">Google</span></p></b>
            <p style="background: #dddddd"><font face="Liberation Serif, serif"><font size="3" style="font-size: 12pt">Writer</font></font></p>
            """);

        var google = paragraphs[0].Runs;
        Assert.Equal(["Arial", "Arial"], google.Select(run => run.Font));
        Assert.Equal([false, true], google.Select(run => run.Bold));
        Assert.Equal([null, 0xFFFFFF00u], google.Select(run => run.Highlight));
        Assert.Equal("Liberation Serif", paragraphs[1].Runs[0].Font);
        Assert.Equal(0xFFDDDDDDu, paragraphs[1].Background);
    }

    [Fact]
    public void Nelle_tabelle_per_Ledger_lo_sfondo_trasparente_o_bianco_non_diventa_un_riempimento()
    {
        var reader = new HtmlTableReader(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations()));

        var table = reader.FirstTable("""
            <table><tr><td style="background-color: rgba(0, 0, 0, 0)">a</td><td style="background: rgb(255, 255, 255)">b</td><td style="background:yellow;color:rgba(255,0,0,0.5)">c</td></tr></table>
            """)!;

        Assert.Null(table.Rows[0][0]!.BackgroundHex);
        Assert.Null(table.Rows[0][1]!.BackgroundHex);
        Assert.Equal("#FFFF00", table.Rows[0][2]!.BackgroundHex);
        Assert.Equal("#FF0000", table.Rows[0][2]!.ColorHex);
    }
}
