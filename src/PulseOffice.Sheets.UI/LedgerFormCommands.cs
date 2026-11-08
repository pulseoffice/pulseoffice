using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerFormCommands(
    IActiveSheetContext context,
    IFormControlCatalog catalog,
    IFormControlNaming naming,
    ITextCatalog texts,
    ISheetImageInsertion insertion) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands()
    {
        foreach (var descriptor in catalog.All)
        {
            var kind = descriptor.Kind;

            yield return Prototype($"ledger.form.insert.{Slug(kind)}") with
            {
                IconKey = $"ledger.form.insert.{Slug(kind)}",
                MenuPath = "insert/formControls",
                Surface = CommandSurface.Advanced,
                RequiredState = DocumentState.DocumentOpen,
                ForbiddenState = DocumentState.ReadOnly,
                EnabledWhen = _ => Context.HasTarget,
                Execute = (_, _) =>
                {
                    Insert(kind);
                    return Task.CompletedTask;
                },
            };
        }
    }

    private static string Slug(FormControlKind kind) =>
        char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private void Insert(FormControlKind kind)
    {
        if (Context.Workbook is not { } workbook) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var descriptor = catalog.Describe(kind);

        var control = new FormControlObject
        {
            Id = Guid.NewGuid().ToString("N"),
            ControlKind = kind,
            NaturalSize = descriptor.DefaultSize,
            Properties = catalog.Create(kind, UniqueName(sheet, kind)),
        };

        insertion.Insert(new PlacedObject
        {
            Id = control.Id,
            Content = control,
            Anchor = Context.Selection.Start,
            Size = descriptor.DefaultSize,
        }, texts["history.form.insert"]);
    }

    private string UniqueName(Worksheet sheet, FormControlKind kind) =>
        naming.NextName(kind, sheet.Objects
            .Select(placed => placed.Content)
            .OfType<FormControlObject>()
            .Select(control => control.Properties.Name));
}
