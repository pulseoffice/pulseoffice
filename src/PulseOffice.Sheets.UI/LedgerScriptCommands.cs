using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Text.Scripts;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerScriptCommands(
    IActiveSheetContext context,
    ISheetTextTransform transform,
    PulseOffice.UI.Controls.Prompts.ITextConversionPrompt prompt,
    IStatusBarService status,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.text.chineseConversion", ct => ConvertAsync(
            ct,
            TextConversionKind.SimplifiedToTraditional,
            TextConversionKind.TraditionalToSimplified)),

        Command("ledger.text.hangulHanja", ct => ConvertAsync(
            ct,
            TextConversionKind.HangulToHanja,
            TextConversionKind.HanjaToHangul)),
    ];

    private CommandDefinition Command(string id, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = "tools/language",
        ContextPaths = [],
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        RequiredSelection = SelectionKind.Cell | SelectionKind.CellRange,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };

    private async Task ConvertAsync(
        CancellationToken ct, TextConversionKind forward, TextConversionKind backward)
    {
        if (await prompt.AskAsync(forward, backward, ct).ConfigureAwait(true) is not { } choice) return;

        var converted = transform.ApplyToSelection(choice.Label, choice.Convert);
        status.Report(
            converted ? texts.Format("conversion.done", choice.Label) : texts["conversion.nothing"],
            converted ? NotificationSeverity.Info : NotificationSeverity.Warning);
    }
}
