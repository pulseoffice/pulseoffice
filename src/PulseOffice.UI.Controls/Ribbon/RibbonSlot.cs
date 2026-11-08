using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public enum RibbonShape { Large, Small, Icon, Tile, Wide }

public abstract class RibbonSlot : ContentControl
{
    protected RibbonSlot(RibbonItem item, RibbonForm form, RibbonBuildContext context)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Form = form;
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public RibbonItem Item { get; private set; }

    public string CommandId => Item.Command.Id.Value;

    protected RibbonForm Form { get; }

    protected RibbonBuildContext Context { get; }

    public void Refresh(RibbonItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        IsEnabled = item.IsEnabled;
        Update();
        Announce();
    }

    protected void Initialise()
    {
        IsEnabled = Item.IsEnabled;
        Content = Build();
        Update();
        Announce();
    }

    private readonly List<Control> _announced = [];

    private void Announce()
    {
        if (Content is not Control root) return;
        foreach (var control in root.GetSelfAndLogicalDescendants().OfType<Control>().Where(Speaks))
        {
            if (!_announced.Contains(control) && !string.IsNullOrEmpty(AutomationProperties.GetName(control))) continue;
            if (!_announced.Contains(control)) _announced.Add(control);
            AutomationProperties.SetName(control, Item.Title);
            AutomationProperties.SetHelpText(control, Item.Tooltip);
        }
    }

    private bool Speaks(Control control) => control is Button or ToggleButton or ComboBox or TextBox or NumericUpDown;

    protected abstract Control Build();

    protected abstract void Update();
}

public interface IRibbonSlotFactory
{
    bool Accepts(RibbonItem item);

    RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context);
}

public interface IRibbonSlotCatalog
{
    RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context);
}

public sealed class RibbonSlotCatalog(IReadOnlyList<IRibbonSlotFactory> factories) : IRibbonSlotCatalog
{
    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        factories.First(factory => factory.Accepts(item)).Create(item, shape, form, context);
}
