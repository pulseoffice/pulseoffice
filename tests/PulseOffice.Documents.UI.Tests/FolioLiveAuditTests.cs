using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioLiveAuditTests
{
    [Fact]
    public Task Tab_a_inizio_voce_di_elenco_cambia_livello_invece_di_scrivere_una_tabulazione() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Primo");
        scene.Editor.Apply((editor, range) => editor.SetList(scene.Document, scene.History, range, DefaultStyleNames.List1, 0), scene.Editor.ParagraphRange());
        scene.Press(Key.Enter);

        scene.Press(Key.Tab);
        scene.Type("Secondo");

        Assert.Equal(["Primo", "Secondo"], scene.Paragraphs);
        Assert.Equal(1, scene.Editor.CurrentParagraph().BulletLevel);

        scene.Press(Key.Home);
        scene.Press(Key.Tab, RawInputModifiers.Shift);
        Assert.Equal(0, scene.Editor.CurrentParagraph().BulletLevel);

        scene.Press(Key.End);
        scene.Press(Key.Tab);
        Assert.Equal("Secondo\t", scene.Paragraphs[1]);
    }, CancellationToken.None);

    [Fact]
    public Task Il_completamento_non_accettato_si_ritira_quando_si_esce_dalla_parola() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        foreach (var letter in "Digitalizzazione ") scene.Type(letter.ToString());
        scene.Press(Key.Enter);
        foreach (var letter in "Digit") scene.Type(letter.ToString());
        Assert.Equal("Digitalizzazione", scene.Paragraphs[1]);

        scene.Press(Key.Right);

        Assert.Equal("Digit", scene.Paragraphs[1]);
        Assert.Equal(5, scene.Editor.Caret.Offset);
    }, CancellationToken.None);

    [Fact]
    public Task Dopo_la_nota_a_pie_di_pagina_si_scrive_nella_nota() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Testo");
        var insert = new FolioInsertCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), null!, null!, null!, null!, null!, null!, null!, null!, null!, new PulseOffice.Foundation.Identity.SystemUserIdentity()).GetCommands()
            .Single(command => command.Id.Value == "folio.insert.footnote");

        insert.Execute(new CommandContext(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen), CancellationToken.None).GetAwaiter().GetResult();
        scene.Type("Fonte");

        var note = Assert.Single(scene.Document.Notes.All);
        Assert.Equal("Fonte", note.Content.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Single().Text);
        Assert.Equal(["Testo"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task Annulla_toglie_una_parola_alla_volta_non_un_carattere() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        foreach (var letter in "uno due tre") scene.Type(letter.ToString());

        scene.History.Undo();
        Assert.Equal(["uno due "], scene.Paragraphs);
        scene.History.Undo();
        Assert.Equal(["uno "], scene.Paragraphs);
        scene.History.Redo();
        scene.History.Redo();
        Assert.Equal(["uno due tre"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task Il_cursore_dopo_il_richiamo_si_disegna_dopo_il_numero_della_nota() => FolioScene.Session.Dispatch(() =>
    {
        var plain = new FolioScene();
        plain.Type("anonimo");
        var noted = new FolioScene();
        noted.Type("anonimo");
        noted.Editor.Insert((documents, at) => documents.InsertNote(noted.Document, noted.History, at, PulseOffice.Documents.Annotations.NoteKind.Footnote, "fonte"), "insert-note");
        var queries = noted.Services.GetRequiredService<PulseOffice.Documents.Layout.Queries.ILayoutQueries>();
        var at = new PulseOffice.Documents.Editing.DocumentPosition(noted.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Id, 7);
        var plainAt = at with { ParagraphId = plain.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Id };

        var withNote = queries.Caret(noted.Editor.Layout, at)!.Value;
        var without = queries.Caret(plain.Editor.Layout, plainAt)!.Value;

        Assert.True(withNote.X > without.X + 1, $"{withNote.X} vs {without.X}");
    }, CancellationToken.None);

    [Fact]
    public Task Attivare_l_intestazione_porta_il_cursore_nell_intestazione() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Corpo");
        var header = new FolioPageCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), new PulseOffice.Documents.UI.Editing.FolioPageStyles(new PulseOffice.Documents.Blocks.BlockTree()), null!,
                scene.Services.GetRequiredService<PulseOffice.Documents.Editing.IStyleEditing>(), null!).GetCommands()
            .Single(command => command.Id.Value == "folio.page.header");

        header.Execute(new CommandContext(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen), CancellationToken.None).GetAwaiter().GetResult();
        scene.Type("Intestazione");

        var page = scene.Document.Styles.Page.All.First(style => style.Header.Enabled);
        Assert.Equal("Intestazione", page.Header.Default.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Single().Text);
        Assert.Equal(["Corpo"], scene.Paragraphs);
    }, CancellationToken.None);

    [Fact]
    public Task Le_intestazioni_si_applicano_da_tastiera() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Introduzione");
        var commands = new FolioRibbonCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), null!, null!, new NoMemory(), null!, null!, null!, null!, null!).GetCommands().ToList();
        var heading = commands.Single(command => command.Id.Value == "folio.style.heading2");

        Assert.Equal("Ctrl+2", heading.DefaultGesture);
        heading.Execute(new CommandContext(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen), CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(2), scene.Editor.CurrentParagraph().StyleName);
        Assert.Equal("Ctrl+1", commands.Single(command => command.Id.Value == "folio.style.heading1").DefaultGesture);
        Assert.Equal("Ctrl+3", commands.Single(command => command.Id.Value == "folio.style.heading3").DefaultGesture);
    }, CancellationToken.None);

    [Fact]
    public Task Dopo_l_inserimento_della_tabella_il_cursore_e_nella_prima_cella() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Prova");
        var tables = new AnsweringTablePrompt(new PulseOffice.UI.Controls.Tables.Insert.TableInsertChoice(2, 2, new PulseOffice.Document.Tables.TableStylePresets().All[2], true, true));
        var insert = new FolioInsertCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), null!, null!, null!, null!, tables, scene.Services.GetRequiredService<IFolioTableLooks>(), new PulseOffice.Localization.TextCatalog(), null!, null!, new PulseOffice.Foundation.Identity.SystemUserIdentity()).GetCommands()
            .Single(command => command.Id.Value == "folio.insert.table");

        insert.Execute(new CommandContext(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen), CancellationToken.None).GetAwaiter().GetResult();
        scene.Type("A");

        var table = scene.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>().Single();
        Assert.Equal("A", table.Cell(0, 0).Content.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Single().Text);
        Assert.Equal("Prova", scene.Paragraphs[0]);
        Assert.Equal("Box List Blue", table.StyleName);
        Assert.Equal(1, table.HeaderRows);
        Assert.Equal("command.folio.insert.table.title", tables.Asked!.TitleKey);
    }, CancellationToken.None);

    [Fact]
    public Task La_griglia_rapida_inserisce_la_tabella_senza_dialogo_e_si_annulla_in_un_passo() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Prova");
        var tables = new AnsweringTablePrompt(new PulseOffice.UI.Controls.Tables.Insert.TableInsertChoice(2, 2, new PulseOffice.Document.Tables.TableStylePresets().All[0], true, true));
        var insert = new FolioInsertCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), null!, null!, null!, null!, tables, scene.Services.GetRequiredService<IFolioTableLooks>(), new PulseOffice.Localization.TextCatalog(), null!, null!, new PulseOffice.Foundation.Identity.SystemUserIdentity()).GetCommands()
            .Single(command => command.Id.Value == "folio.insert.table");
        Assert.IsType<PulseOffice.UI.Commands.TableGridSpec>(insert.RibbonControl);

        insert.Execute(new CommandContext(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen) { Argument = new PulseOffice.UI.Commands.RibbonTableSize(4, 3) }, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Null(tables.Asked);
        var table = scene.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>().Single();
        Assert.Equal(4, table.Rows.Count);
        Assert.Equal(3, table.ColumnCount);
        scene.History.Undo();
        Assert.Empty(scene.Document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>());
    }, CancellationToken.None);

    [Fact]
    public Task Il_completamento_offerto_non_spezza_l_annullamento_per_parola() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        foreach (var letter in "Digitalizzazione ") scene.Type(letter.ToString());
        scene.Press(Key.Enter);
        foreach (var letter in "Digitale ") scene.Type(letter.ToString());
        Assert.Equal("Digitale ", scene.Paragraphs[1]);

        scene.History.Undo();

        Assert.Equal("", scene.Paragraphs[1]);
    }, CancellationToken.None);
}

internal sealed class AnsweringTablePrompt(PulseOffice.UI.Controls.Tables.Insert.TableInsertChoice answer) : PulseOffice.UI.Controls.Tables.Insert.ITableInsertPrompt
{
    public PulseOffice.UI.Controls.Tables.Insert.TableInsertRequest? Asked { get; private set; }

    public Task<PulseOffice.UI.Controls.Tables.Insert.TableInsertChoice?> AskAsync(PulseOffice.UI.Controls.Tables.Insert.TableInsertRequest request, CancellationToken ct = default)
    {
        Asked = request;
        return Task.FromResult<PulseOffice.UI.Controls.Tables.Insert.TableInsertChoice?>(answer);
    }

    public PulseOffice.UI.Controls.Tables.Insert.TableInsertChoice Quick(int rows, int columns, PulseOffice.UI.Controls.Tables.Insert.TableInsertLimits limits) =>
        answer with { Rows = rows, Columns = columns };
}
