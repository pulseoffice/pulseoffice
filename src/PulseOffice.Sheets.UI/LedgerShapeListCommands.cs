using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.TextFormat;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerShapeListCommands(
    IActiveSheetContext context,
    IListFormatDialog dialog,
    IParagraphListFormats formats,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public const string BulletsNumbering = "ledger.shapeText.bulletsNumbering";

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype(BulletsNumbering) with
        {
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Available,
            Execute = (_, ct) => BulletsAsync(ct),
        },
        Direction("ledger.shapeText.leftToRight", PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight),
        Direction("ledger.shapeText.rightToLeft", PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft),
    ];

    private CommandDefinition Direction(string id, PulseOffice.Text.Rich.Bidi.TextDirection direction) =>
        Prototype(id) with
        {
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Available,
            CheckedWhen = _ => Available && (Context.ShapeText!.Paragraph!.Direction ?? PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight) == direction,
            Execute = (_, _) =>
            {
                if (Available && Context.ShapeText is { } shapeText)
                {
                    shapeText.ApplyParagraphs(texts["history.draw.shapeTextDirection"], paragraph => paragraph with { Direction = direction });
                    Context.RequestRefresh();
                }
                return Task.CompletedTask;
            },
        };

    private bool Available => ObjectsEditable && Context.ShapeText is { IsActive: true, Paragraph: not null };

    public async Task BulletsAsync(CancellationToken ct)
    {
        if (!Available || Context.ShapeText is not { Paragraph: { } first } shapeText) return;
        if (await dialog.AskAsync(formats.Request(first), "command.ledger.shapeText.bulletsNumbering.title", ct).ConfigureAwait(true) is not { } answer || !Available) return;
        var chosen = answer.Levels[answer.Level];
        shapeText.ApplyParagraphs(texts["history.draw.shapeTextList"], paragraph => formats.Apply(paragraph, chosen, answer.Level));
        Context.RequestRefresh();
    }
}
