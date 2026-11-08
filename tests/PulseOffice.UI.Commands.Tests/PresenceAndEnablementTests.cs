using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public class PresenceAndEnablementTests
{
    private static readonly FakeTextCatalog Texts = new();

    private static CommandDefinition Command(
        string id,
        DocumentState required = DocumentState.None,
        DocumentState forbidden = DocumentState.None,
        SelectionKind selection = SelectionKind.None,
        Func<CommandContext, bool>? enabledWhen = null,
        ModuleKind module = ModuleKind.Shell)
    {
        Texts.Add($"command.{id}.title", id);

        return new CommandDefinition
        {
            Id = new CommandId(id),
            MenuPath = "file/save",
            Module = module,
            RequiredState = required,
            ForbiddenState = forbidden,
            RequiredSelection = selection,
            EnabledWhen = enabledWhen,
            Execute = (_, _) => Task.CompletedTask,
        };
    }

    private static CommandContext Open(
        DocumentState extra = DocumentState.None, SelectionKind selection = SelectionKind.None) =>
        new(ModuleKind.Shell, selection, DocumentState.DocumentOpen | extra);

    [Fact]
    public void UnComandoNonApplicabileRestaPresenteMaSpento()
    {
        var command = Command("p.one", required: DocumentState.DocumentOpen | DocumentState.CanUndo);

        Assert.True(command.IsPresent(Open()));
        Assert.False(command.IsEnabled(Open()));
        Assert.True(command.IsEnabled(Open(DocumentState.CanUndo)));
    }

    [Fact]
    public void SenzaDocumentoIlComandoSparisce()
    {
        var command = Command("p.two", required: DocumentState.DocumentOpen);

        Assert.False(command.IsPresent(CommandContext.Empty));
    }

    [Fact]
    public void LaSolaLetturaSpegneNonNasconde()
    {
        var command = Command("p.three",
            required: DocumentState.DocumentOpen, forbidden: DocumentState.ReadOnly);

        var readOnly = Open(DocumentState.ReadOnly);

        Assert.True(command.IsPresent(readOnly));
        Assert.False(command.IsEnabled(readOnly));
    }

    [Fact]
    public void LaSelezioneRichiestaSpegneNonNasconde()
    {
        var command = Command("p.four",
            required: DocumentState.DocumentOpen, selection: SelectionKind.CellRange);

        Assert.True(command.IsPresent(Open(selection: SelectionKind.Cell)));
        Assert.False(command.IsEnabled(Open(selection: SelectionKind.Cell)));
        Assert.True(command.IsEnabled(Open(selection: SelectionKind.CellRange)));
    }

    [Fact]
    public void LaCondizioneDiContenutoSpegneNonNasconde()
    {
        var command = Command("p.five", required: DocumentState.DocumentOpen, enabledWhen: _ => false);

        Assert.True(command.IsPresent(Open()));
        Assert.False(command.IsEnabled(Open()));
    }

    [Fact]
    public void IlMenuMostraAncheIComandiSpenti()
    {
        var registry = new CommandRegistry(Texts);
        registry.Register(Command("p.six", required: DocumentState.DocumentOpen, enabledWhen: _ => false));

        var group = Assert.Single(Assert.Single(registry.BuildMenu(Open())).Children);

        Assert.Single(group.Children);
        Assert.False(group.Children[0].Command!.IsEnabled(Open()));
    }

    [Fact]
    public void IlMenuContestualeInveceNasconde()
    {
        var registry = new CommandRegistry(Texts);

        registry.Register(Command("p.seven", required: DocumentState.DocumentOpen, enabledWhen: _ => false)
            with { ContextPaths = ["cell/clipboard"], Surface = CommandSurface.ContextMenu });

        Assert.Empty(registry.BuildContextMenu(Open(), "cell"));
    }
}
