using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class LegacyFormFieldTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Plain()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "x", DefaultStyleNames.Standard));
        return document;
    }

    private IReadOnlyList<ContentControl> Controls(TextDocument document) =>
        [.. document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<ContentControlMark>()
            .Where(mark => mark.Edge == MarkEdge.Start).Select(mark => mark.Control!)];

    private string Text(TextDocument document) => string.Concat(document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));

    [Fact]
    public void I_campi_modulo_di_Word_diventano_controlli_del_contenuto()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Plain(), stream).Succeeded);
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var paragraph = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().First();
            paragraph.RemoveAllChildren<W.Run>();
            paragraph.Append(
                new W.Run(new W.FieldChar(new W.FormFieldData(new W.FormFieldName { Val = "Accetto" },
                    new W.CheckBox(new W.AutomaticallySizeFormField(), new W.DefaultCheckBoxFormFieldState { Val = false }, new W.Checked())))
                { FieldCharType = W.FieldCharValues.Begin }),
                new W.Run(new W.FieldCode(" FORMCHECKBOX ")),
                new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }),
                new W.Run(new W.FieldChar(new W.FormFieldData(new W.FormFieldName { Val = "Nome" }, new W.TextInput())) { FieldCharType = W.FieldCharValues.Begin }),
                new W.Run(new W.FieldCode(" FORMTEXT ")),
                new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
                new W.Run(new W.Text("Ada")),
                new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }),
                new W.Run(new W.FieldChar(new W.FormFieldData(new W.FormFieldName { Val = "Scelta" },
                    new W.DropDownListFormField(new W.DropDownListSelection { Val = 1 }, new W.ListEntryFormField { Val = "Uno" }, new W.ListEntryFormField { Val = "Due" })))
                { FieldCharType = W.FieldCharValues.Begin }),
                new W.Run(new W.FieldCode(" FORMDROPDOWN ")),
                new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var controls = Controls(read.Value!);

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX123");
        Assert.Equal([ContentControlKind.CheckBox, ContentControlKind.PlainText, ContentControlKind.DropDownList], controls.Select(control => control.Kind));
        Assert.True(controls[0].Checked);
        Assert.Equal("Nome", controls[1].Title);
        Assert.Equal(["Uno", "Due"], controls[2].Items.Select(item => item.DisplayText));
        Assert.Equal("☒AdaDue", Text(read.Value!));
    }

    [Fact]
    public void I_segnaposti_dei_campi_modulo_ODT_diventano_controlli_del_contenuto()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Plain(), stream).Succeeded);
        const string field = "urn:openoffice:names:experimental:ooo-ms-interop:xmlns:field:1.0";
        var fields =
            $"<field:fieldmark xmlns:field=\"{field}\" text:name=\"__Fieldmark__0\" field:type=\"vnd.oasis.opendocument.field.FORMCHECKBOX\"><field:param field:name=\"Checkbox_Checked\" field:value=\"true\"/></field:fieldmark>"
            + $"<field:fieldmark-start xmlns:field=\"{field}\" text:name=\"Nome\" field:type=\"vnd.oasis.opendocument.field.FORMTEXT\"/>Ada<field:fieldmark-end xmlns:field=\"{field}\"/>"
            + $"<field:fieldmark xmlns:field=\"{field}\" text:name=\"__Fieldmark__2\" field:type=\"vnd.oasis.opendocument.field.FORMDROPDOWN\"><field:param field:name=\"Dropdown_ListEntry\" field:value=\"Uno\"/><field:param field:name=\"Dropdown_ListEntry\" field:value=\"Due\"/><field:param field:name=\"Dropdown_Selected\" field:value=\"1\"/></field:fieldmark>";
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8)) content = reader.ReadToEnd();
            var at = content.IndexOf(">x</text:", StringComparison.Ordinal);
            Assert.True(at > 0);
            content = content[..(at + 1)] + fields + content[(at + 2)..];
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
        stream.Position = 0;

        var document = new OdtReader().Read(stream).Value!;
        var controls = Controls(document);

        Assert.Equal([ContentControlKind.CheckBox, ContentControlKind.PlainText, ContentControlKind.DropDownList], controls.Select(control => control.Kind));
        Assert.True(controls[0].Checked);
        Assert.Null(controls[0].Title);
        Assert.Equal("Nome", controls[1].Title);
        Assert.Equal("☒AdaDue", Text(document));
    }

    [Fact]
    public void I_controlli_del_contenuto_attorno_a_una_cella_non_si_perdono()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Plain(), stream).Succeeded);
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var body = package.MainDocumentPart!.Document!.Body!;
            var cell = new W.TableCell(new W.TableCellProperties(new W.TableCellWidth { Width = "2000", Type = W.TableWidthUnitValues.Dxa }),
                new W.Paragraph(new W.Run(new W.Text("Nome"))));
            var wrapped = new W.SdtCell(new W.SdtProperties(new W.SdtAlias { Val = "Campo" }, new W.SdtContentText()), new W.SdtContentCell(cell));
            body.InsertAt(new W.Table(new W.TableGrid(new W.GridColumn { Width = "2000" }), new W.TableRow(wrapped)), 0);
        }
        stream.Position = 0;

        var document = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        var table = document.Body.Blocks.OfType<TableBlock>().Single();
        var marks = table.Rows[0].Cells[0].Content.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<ContentControlMark>().ToList();

        var start = Assert.Single(marks, mark => mark.Edge == MarkEdge.Start);
        Assert.Equal("Campo", start.Control!.Title);
        Assert.Contains(marks, mark => mark.Edge == MarkEdge.End);
    }
}
