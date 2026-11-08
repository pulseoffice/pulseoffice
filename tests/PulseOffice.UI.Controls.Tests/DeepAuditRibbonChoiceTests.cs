using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditRibbonChoiceTests
{
    [Fact]
    public Task LaGalleriaNonRicostruisceITasselliSeNullaECambiato() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var current = "a";
        IReadOnlyList<RibbonChoice> choices = [new("a", "A"), new("b", "B")];
        var spec = new ChoiceSpec(() => choices, () => current) { Look = RibbonChoiceLook.Gallery };
        var command = new CommandDefinition { Id = new CommandId("test.gallery"), RibbonControl = spec, Execute = (_, _) => Task.CompletedTask };
        var item = new RibbonItem(command, true, "Gallery", null);
        var services = new RibbonServices(
            new IconCatalog(), new TextCatalog(UiLanguage.English), null, new RibbonStripLayout(), new RibbonReductionPlanner(),
            new RibbonSlotCatalog([new ChoiceSlotFactory(new WcagColorContrast())]));
        var context = new RibbonBuildContext(services, DesignTokens.For(InterfaceDensity.Compact), _ => { });
        var slot = new ChoiceSlot(item, RibbonForm.Full, context, new WcagColorContrast());

        var first = AllButtons(slot);
        slot.Refresh(item);
        var same = AllButtons(slot);
        current = "b";
        slot.Refresh(item);
        var moved = AllButtons(slot);

        Assert.Equal(first, same);
        Assert.NotEqual(first, moved);
        return Task.CompletedTask;
    }, CancellationToken.None);

    private static List<Button> AllButtons(Control root) =>
        [.. Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<Button>().Where(button => button.Classes.Contains("tile"))];
}
