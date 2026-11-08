using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerSignatureCommands(
    IActiveSheetContext context,
    PulseOffice.UI.Controls.Forms.ISignatureLinePrompt prompt,
    ITextCatalog texts,
    ISheetImageInsertion insertion)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("ledger.insert.signatureLine"),
            IconKey = "ledger.insert.signatureLine",
            Module = ModuleKind.Sheets,
            MenuPath = "insert/objects",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => context.HasTarget,
            Execute = (_, ct) => SignatureLineAsync(ct),
        },
    ];

    private async Task SignatureLineAsync(CancellationToken ct)
    {
        if (context.Workbook is null) return;
        if (await prompt.AskAsync("command.ledger.insert.signatureLine.title", ct).ConfigureAwait(true) is not { } line) return;

        insertion.Insert(new PlacedObject
        {
            Id = line.Id,
            Content = line,
            Anchor = context.Selection.Start,
            Size = Document.Forms.SignatureLineObject.StandardSize,
        }, texts["history.signatureLine.insert"]);
    }
}
