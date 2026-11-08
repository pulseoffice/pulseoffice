using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Scripting;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFacadeContractTests
{
    private sealed record Fixture(FolioScene Scene, FolioFacade Doc)
    {
        public ParagraphBlock First => Scene.Document.Body.Blocks.OfType<ParagraphBlock>().First();

        public void ReadOnly(Action write)
        {
            Scene.ReadOnly = true;
            Assert.Equal("The document is read-only.", Assert.Throws<InvalidOperationException>(write).Message);
            Scene.ReadOnly = false;
        }
    }

    private readonly IReadOnlyDictionary<string, Action<Fixture>> Cases = new Dictionary<string, Action<Fixture>>
    {
        ["Name"] = f => Assert.Equal(string.Empty, f.Doc.Name),
        ["Text"] = f =>
        {
            f.Doc.AppendParagraph("uno");
            f.Doc.AppendParagraph("due");
            Assert.Equal("uno\ndue", f.Doc.Text);
        },
        ["ParagraphCount"] = f =>
        {
            f.Doc.AppendParagraph("uno");
            f.Doc.AppendParagraph("due");
            Assert.Equal(2, f.Doc.ParagraphCount);
        },
        ["WordCount"] = f =>
        {
            f.Doc.AppendParagraph("uno due tre");
            Assert.Equal(3, f.Doc.WordCount);
        },
        ["TableCount"] = f =>
        {
            f.Doc.InsertTable(2, 2);
            Assert.Equal(1, f.Doc.TableCount);
        },
        ["MacroCount"] = f => Assert.Equal(f.Scene.Document.Macros.All.Count, f.Doc.MacroCount),
        ["SelectedText"] = f =>
        {
            f.Doc.AppendParagraph("ciao mondo");
            f.Doc.Select(1, 0, 4);
            Assert.Equal("ciao", f.Doc.SelectedText);
        },
        ["TrackChanges"] = f =>
        {
            f.Doc.SetTrackChanges(true);
            Assert.True(f.Doc.TrackChanges);
        },
        ["ChangeCount"] = f =>
        {
            f.Doc.SetTrackChanges(true);
            f.Doc.InsertText("nuovo");
            Assert.True(f.Doc.ChangeCount > 0);
        },
        ["Paragraph"] = f =>
        {
            f.Doc.AppendParagraph("primo");
            Assert.Equal("primo", f.Doc.Paragraph(1));
            Assert.Contains("1 paragraphs", Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Paragraph(2)).Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Paragraph(0));
            Assert.Throws<ArgumentException>(() => f.Doc.Paragraph(double.NaN));
        },
        ["SetParagraph"] = f =>
        {
            Assert.True(f.Doc.SetParagraph(1, "nuovo"));
            Assert.Equal("nuovo", f.First.Text);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.SetParagraph(5, "fuori"));
            f.ReadOnly(() => f.Doc.SetParagraph(1, "bloccato"));
            Assert.Equal("nuovo", f.First.Text);
        },
        ["AppendParagraph"] = f =>
        {
            Assert.Equal(1, f.Doc.AppendParagraph("primo"));
            Assert.Equal(2, f.Doc.AppendParagraph("secondo"));
            f.ReadOnly(() => f.Doc.AppendParagraph("terzo"));
            Assert.Equal(2, f.Doc.ParagraphCount);
        },
        ["InsertText"] = f =>
        {
            Assert.True(f.Doc.InsertText("ciao"));
            Assert.Equal("ciao", f.First.Text);
            Assert.Throws<ArgumentException>(() => f.Doc.InsertText(string.Empty));
            f.ReadOnly(() => f.Doc.InsertText("bloccato"));
        },
        ["Style"] = f =>
        {
            f.Doc.SetStyle(1, "Heading 1");
            Assert.Equal("Heading 1", f.Doc.Style(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Style(3));
        },
        ["SetStyle"] = f =>
        {
            Assert.True(f.Doc.SetStyle(1, "Heading 1"));
            Assert.Equal("Heading 1", f.First.StyleName);
            Assert.Contains("Heading 1", Assert.Throws<ArgumentException>(() => f.Doc.SetStyle(1, "Nessuno")).Message);
            Assert.Throws<ArgumentException>(() => f.Doc.SetStyle(1, string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.SetStyle(9, "Heading 1"));
            f.ReadOnly(() => f.Doc.SetStyle(1, "Heading 2"));
        },
        ["Styles"] = f => Assert.Contains("Heading 1", f.Doc.Styles()),
        ["Bold"] = f =>
        {
            f.Doc.AppendParagraph("forte");
            Assert.True(f.Doc.Bold(1, true));
            Assert.All(f.First.Content.Runs, run => Assert.True(run.Bold));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Bold(2, true));
            f.ReadOnly(() => f.Doc.Bold(1, false));
        },
        ["Italic"] = f =>
        {
            f.Doc.AppendParagraph("obliquo");
            Assert.True(f.Doc.Italic(1, true));
            Assert.All(f.First.Content.Runs, run => Assert.True(run.Italic));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Italic(2, true));
            f.ReadOnly(() => f.Doc.Italic(1, false));
        },
        ["SelectParagraph"] = f =>
        {
            f.Doc.AppendParagraph("tutto");
            Assert.True(f.Doc.SelectParagraph(1));
            Assert.Equal("tutto", f.Doc.SelectedText);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.SelectParagraph(2));
        },
        ["Select"] = f =>
        {
            f.Doc.AppendParagraph("ciao mondo");
            Assert.True(f.Doc.Select(1, 5, 10));
            Assert.Equal("mondo", f.Doc.SelectedText);
            Assert.Contains("10 characters", Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Select(1, 0, 11)).Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Select(1, 6, 2));
            Assert.Throws<ArgumentException>(() => f.Doc.Select(1, double.NaN, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Select(2, 0, 1));
        },
        ["Find"] = f =>
        {
            f.Doc.AppendParagraph("Gatto e gatto");
            Assert.Equal(2, f.Doc.Find("gatto"));
            Assert.Equal(0, f.Doc.Find("cane"));
            Assert.Throws<ArgumentException>(() => f.Doc.Find(string.Empty));
        },
        ["Replace"] = f =>
        {
            f.Doc.AppendParagraph("gatto e gatto");
            Assert.Equal(2, f.Doc.Replace("gatto", "topo"));
            Assert.Equal("topo e topo", f.First.Text);
            Assert.Equal(0, f.Doc.Replace("cane", "lupo"));
            Assert.Throws<ArgumentException>(() => f.Doc.Replace(string.Empty, "x"));
            f.ReadOnly(() => f.Doc.Replace("topo", "ratto"));
            Assert.Equal("topo e topo", f.First.Text);
        },
        ["InsertTable"] = f =>
        {
            Assert.True(f.Doc.InsertTable(2, 3));
            Assert.Equal(1, f.Doc.TableCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.InsertTable(0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.InsertTable(2, 65));
            Assert.Throws<ArgumentException>(() => f.Doc.InsertTable(double.NaN, 2));
            f.ReadOnly(() => f.Doc.InsertTable(1, 1));
            Assert.Equal(1, f.Doc.TableCount);
        },
        ["Cell"] = f =>
        {
            f.Doc.InsertTable(2, 3);
            f.Doc.SetCell(1, 2, 3, "sei");
            Assert.Equal("sei", f.Doc.Cell(1, 2, 3));
            Assert.Contains("1 tables", Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Cell(2, 1, 1)).Message);
            Assert.Contains("2 rows", Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Cell(1, 3, 1)).Message);
            Assert.Contains("3 columns", Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.Cell(1, 1, 4)).Message);
        },
        ["SetCell"] = f =>
        {
            f.Doc.InsertTable(2, 2);
            Assert.True(f.Doc.SetCell(1, 1, 2, "due"));
            Assert.Equal("due", f.Doc.Cell(1, 1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Doc.SetCell(1, 3, 1, "fuori"));
            f.ReadOnly(() => f.Doc.SetCell(1, 1, 1, "bloccato"));
            Assert.Equal(string.Empty, f.Doc.Cell(1, 1, 1));
        },
        ["Bookmarks"] = f =>
        {
            f.Doc.AddBookmark("inizio");
            Assert.Equal(["inizio"], f.Doc.Bookmarks());
        },
        ["AddBookmark"] = f =>
        {
            Assert.True(f.Doc.AddBookmark("inizio"));
            Assert.False(f.Doc.AddBookmark("inizio"));
            Assert.Single(f.Doc.Bookmarks());
            Assert.Throws<ArgumentException>(() => f.Doc.AddBookmark(" "));
            f.ReadOnly(() => f.Doc.AddBookmark("altro"));
        },
        ["GoToBookmark"] = f =>
        {
            f.Doc.AppendParagraph("primo");
            f.Doc.AppendParagraph("secondo");
            f.Doc.Select(2, 3, 3);
            f.Doc.AddBookmark("qui");
            f.Doc.Select(1, 0, 0);
            Assert.True(f.Doc.GoToBookmark("qui"));
            Assert.Equal(f.Scene.Document.Body.Blocks.OfType<ParagraphBlock>().ElementAt(1).Id, f.Scene.Editor.Caret.ParagraphId);
            Assert.Contains("qui", Assert.Throws<ArgumentException>(() => f.Doc.GoToBookmark("altrove")).Message);
            Assert.Throws<ArgumentException>(() => f.Doc.GoToBookmark(string.Empty));
        },
        ["InsertField"] = f =>
        {
            Assert.True(f.Doc.InsertField("paragraphs"));
            Assert.Equal("1", f.First.Text);
            Assert.Contains("date", Assert.Throws<ArgumentException>(() => f.Doc.InsertField("nessuno")).Message);
            Assert.Throws<ArgumentException>(() => f.Doc.InsertField(string.Empty));
            f.ReadOnly(() => f.Doc.InsertField("date"));
        },
        ["UpdateFields"] = f =>
        {
            f.Doc.InsertField("paragraphs");
            f.Doc.AppendParagraph("due");
            f.Doc.AppendParagraph("tre");
            Assert.True(f.Doc.UpdateFields());
            Assert.Equal("3", f.First.Text);
            f.ReadOnly(() => f.Doc.UpdateFields());
        },
        ["SetTrackChanges"] = f =>
        {
            Assert.True(f.Doc.SetTrackChanges(true));
            Assert.True(f.Scene.Document.Changes.IsRecording);
            f.ReadOnly(() => f.Doc.SetTrackChanges(false));
            f.Scene.Document.Changes.ProtectionKey = [1, 2, 3];
            Assert.Contains("password", Assert.Throws<InvalidOperationException>(() => f.Doc.SetTrackChanges(false)).Message);
            Assert.True(f.Scene.Document.Changes.IsRecording);
        },
        ["AcceptAll"] = f =>
        {
            f.Doc.SetTrackChanges(true);
            f.Doc.InsertText("nuovo");
            f.ReadOnly(() => f.Doc.AcceptAll());
            Assert.True(f.Doc.AcceptAll() > 0);
            Assert.Equal(0, f.Doc.ChangeCount);
            Assert.Equal("nuovo", f.Doc.Text);
            Assert.Equal(0, f.Doc.AcceptAll());
        },
        ["RejectAll"] = f =>
        {
            f.Doc.SetTrackChanges(true);
            f.Doc.InsertText("nuovo");
            f.ReadOnly(() => f.Doc.RejectAll());
            Assert.True(f.Doc.RejectAll() > 0);
            Assert.Equal(0, f.Doc.ChangeCount);
            Assert.Equal(string.Empty, f.Doc.Text);
        },
    };

    [Fact]
    public void Ogni_membro_del_catalogo_di_Folio_ha_il_suo_caso()
    {
        var members = new FolioScriptApi().Roots.Single().Members.Select(member => member.Member).ToList();

        Assert.Empty(members.Except(Cases.Keys));
        Assert.Empty(Cases.Keys.Except(members));
    }

    [Fact]
    public Task Ogni_membro_di_Folio_da_errore_chiaro_sugli_argomenti_sbagliati_ed_effetto_sui_giusti() => FolioScene.RunAsync(() =>
    {
        var failures = new List<string>();
        foreach (var member in new FolioScriptApi().Roots.Single().Members)
        {
            var scene = new FolioScene();
            try
            {
                Cases[member.Member](new Fixture(scene, scene.Services.GetRequiredService<FolioFacade>()));
            }
            catch (Exception failure)
            {
                failures.Add($"{member.Member}: {failure.Message}");
            }
            finally
            {
                scene.Window.Close();
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
        return Task.CompletedTask;
    });
}
