using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerObjectCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    ITextCatalog texts,
    IContentFlipping flipping,
    PulseOffice.UI.Controls.Links.IHyperlinkPrompt hyperlinks,
    PulseOffice.UI.Controls.Links.ILinkFollower links) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Command("ledger.object.flipHorizontal", content => Flip(content, horizontal: true)),
        Command("ledger.object.flipVertical", content => Flip(content, horizontal: false)),
        Command("ledger.object.rotateRight", content => Rotate(content, 90)),
        Command("ledger.object.rotateLeft", content => Rotate(content, -90)),
    ];

    public IEnumerable<CommandDefinition> GetDialogCommands() =>
    [
        Prototype("ledger.object.rename") with
        {
            MenuPath = "format/object",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject is not null,
            Execute = (_, ct) => RenameAsync(ct),
        },
        Prototype("ledger.object.hyperlink") with
        {
            MenuPath = "insert/link",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject is not null,
            Execute = (_, ct) => HyperlinkAsync(ct),
        },
        Prototype("ledger.object.hyperlink.open") with
        {
            MenuPath = "insert/link",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            EnabledWhen = _ => Context.SelectedObject is { Hyperlink.Length: > 0 },
            Execute = (_, _) =>
            {
                if (Context.SelectedObject is { Hyperlink: { Length: > 0 } link }) links.Follow(link, _ => false);
                return Task.CompletedTask;
            },
        },
        Prototype("ledger.object.hyperlink.remove") with
        {
            MenuPath = "insert/link",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject is { Hyperlink.Length: > 0 },
            Execute = (_, _) =>
            {
                SetLink(null, texts["command.ledger.object.hyperlink.remove.title"]);
                return Task.CompletedTask;
            },
        },
    ];

    private async Task HyperlinkAsync(CancellationToken ct)
    {
        if (Context.SelectedObject is not { } placed) return;
        var request = new PulseOffice.UI.Controls.Links.LinkRequest("command.ledger.object.hyperlink.dialog.title")
        {
            Address = placed.Hyperlink ?? string.Empty,
            ShowsText = false,
            TextEditable = false,
            Kinds = [PulseOffice.UI.Controls.Links.LinkKind.Web, PulseOffice.UI.Controls.Links.LinkKind.File, PulseOffice.UI.Controls.Links.LinkKind.Mail],
            CanRemove = placed.Hyperlink is not null,
        };
        if (await hyperlinks.AskAsync(request, ct).ConfigureAwait(true) is not { } choice) return;
        SetLink(choice.Remove ? null : choice.Address, texts["command.ledger.object.hyperlink.title"]);
    }

    private void SetLink(string? link, string description)
    {
        if (Context.Workbook is not { } workbook || Context.SelectedObject is not { } placed) return;
        var value = link?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
        if (value == placed.Hyperlink) return;
        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;
        Replace(sheet, index, placed, placed with { Hyperlink = value }, description);
    }

    private CommandDefinition Command(string id, Func<EmbeddedObject, EmbeddedObject?> change) =>
        Prototype(id) with
        {
            IconKey = id,
            MenuPath = "format/object",
            ContextPaths = ["cell/arrange"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.SelectedObject is not null,
            Execute = (_, _) =>
            {
                Apply(id, change);
                return Task.CompletedTask;
            },
        };

    private EmbeddedObject? Flip(EmbeddedObject content, bool horizontal) =>
        flipping.Flips(content) ? flipping.Flip(content, horizontal) : null;

    private static EmbeddedObject? Rotate(EmbeddedObject content, double degrees) => content switch
    {
        ShapeObject shape => shape with
        {
            RotationDegrees = Normalise(shape.RotationDegrees + degrees),
        },

        VectorGraphicObject vector => vector with
        {
            RotationDegrees = Normalise(vector.RotationDegrees + degrees),
        },

        _ => null,
    };

    private static double Normalise(double degrees) => (degrees % 360 + 360) % 360;

    private void Apply(string id, Func<EmbeddedObject, EmbeddedObject?> change)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { } placed) return;

        if (change(placed.Content) is not { } content) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;

        var updated = placed with { Content = content };

        Replace(sheet, index, placed, updated, texts[$"command.{id}.title"]);
    }

    private void Replace(
        Worksheet sheet, int index, PlacedObject previous, PlacedObject updated, string description)
    {
        sheet.Objects[index] = updated;

        Context.History?.Push(new DelegateAction(
            description,
            redo: () => sheet.Objects[index] = updated,
            undo: () => sheet.Objects[index] = previous));

        Context.SelectObject(updated);
        Context.RequestRefresh();
    }

    private async Task RenameAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.SelectedObject is not { } placed) return;

        var form = new FormPromptView(
            texts["object.rename.explanation"],
            [new TextField("name", texts["object.name"], placed.Content.Title ?? placed.Id)],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "command.ledger.object.rename.title" }, ct)
            .ConfigureAwait(true);

        if (values is null) return;
        if (values["name"] as string is not { Length: > 0 } name) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;

        Replace(
            sheet, index, placed,
            placed with { Content = placed.Content with { Title = name } },
            texts["command.ledger.object.rename.title"]);
    }
}
