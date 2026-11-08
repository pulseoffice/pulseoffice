using Avalonia.Controls;
using Avalonia.Input;
using PulseOffice.UI.Commands;

namespace PulseOffice.Shell.Surfaces;

public sealed class CommandSurfaceFactory(
    CommandRegistry registry,
    RibbonProjection ribbon,
    CommandExecutor executor,
    IDocumentWorkspace workspace,
    IMenuPlanner planner,
    PulseOffice.UI.Controls.Text.ITextEditingRouter textEditing)
    : PulseOffice.UI.Controls.Services.IContextMenuFactory
{
    public ContextMenu? Build(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var context = workspace.Context;
        var nodes = registry.BuildContextMenu(context, root);

        if (nodes.Count == 0) return null;

        var menu = new ContextMenu();

        foreach (var group in nodes.SelectMany(node => node.Children))
        {
            var leaves = group.Children
                .Where(child => child.Command is not null)
                .Select(child => BuildMenuItem(child, context))
                .OfType<MenuItem>()
                .ToList();

            var submenus = group.Children
                .Where(child => child.Command is null)
                .Select(child => BuildMenuItem(child, context))
                .OfType<MenuItem>()
                .ToList();

            if (leaves.Count == 0 && submenus.Count == 0) continue;

            if (menu.Items.Count > 0) menu.Items.Add(new Separator());

            menu.Items.Add(GroupHeader(group.Segment));

            foreach (var item in leaves) menu.Items.Add(item);
            foreach (var item in submenus) menu.Items.Add(item);
        }

        return menu.Items.Count == 0 ? null : menu;
    }

    public bool Open(string root, Control host, Action? closed = null) => Show(root, host, null, closed);

    public bool OpenAt(string root, Control host, Avalonia.Rect anchor, Action? closed = null) => Show(root, host, anchor, closed);

    private bool Show(string root, Control host, Avalonia.Rect? anchor, Action? closed)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (Blocked?.Invoke() == true || Build(root) is not { } menu) return false;

        if (anchor is { } rect)
        {
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
            menu.PlacementRect = rect;
        }
        else menu.Placement = PlacementMode.Pointer;
        menu.SetValue(Avalonia.Controls.Primitives.TemplatedControl.FontWeightProperty, Avalonia.Media.FontWeight.Normal);
        menu.SetValue(TextBlock.TextAlignmentProperty, Avalonia.Media.TextAlignment.Left);
        host.ContextMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(host.ContextMenu, menu)) host.ContextMenu = null;
            closed?.Invoke();
        };
        menu.Open(host);
        if (anchor is not null)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => FirstItem(menu)?.Focus(NavigationMethod.Directional), Avalonia.Threading.DispatcherPriority.Loaded);
        return true;
    }

    private MenuItem? FirstItem(ContextMenu menu) =>
        menu.Items.OfType<MenuItem>().FirstOrDefault(item => item.IsEnabled && item.Focusable && item.IsVisible);

    public Menu BuildMenu(CommandContext context)
    {
        var menu = new Menu();

        foreach (var node in registry.BuildMenu(context))
            if (BuildTopLevel(node, context) is { } item)
                menu.Items.Add(item);

        return menu;
    }

    private MenuItem? BuildTopLevel(CommandNode node, CommandContext context)
    {
        var top = new MenuItem { Header = node.Segment };

        FillItems(top.Items, node, context);

        return top.Items.Count > 0 ? top : null;
    }

    private void FillItems(Avalonia.Controls.ItemCollection items, CommandNode node, CommandContext context)
    {
        foreach (var block in planner.Blocks(node))
        {
            var built = block.Nodes
                .Select(child => BuildMenuItem(child, context))
                .OfType<MenuItem>()
                .ToList();

            if (built.Count == 0) continue;

            if (items.Count > 0) items.Add(new Separator());

            if (block.Title is { Length: > 0 } title) items.Add(GroupHeader(title));

            foreach (var item in built) items.Add(item);
        }
    }

    public void FillNativeMenu(NativeMenu menu, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(menu);

        menu.Items.Clear();

        foreach (var node in registry.BuildMenu(context))
            if (BuildNativeTopLevel(node, context) is { } item)
                menu.Items.Add(item);
    }

    private NativeMenuItem? BuildNativeTopLevel(CommandNode node, CommandContext context)
    {
        var submenu = new NativeMenu();

        FillNativeItems(submenu, node, context);

        return submenu.Items.Count == 0 ? null : new NativeMenuItem(node.Segment) { Menu = submenu, IsEnabled = Suspended?.Invoke() != true };
    }

    private void FillNativeItems(NativeMenu menu, CommandNode node, CommandContext context)
    {
        foreach (var block in planner.Blocks(node))
        {
            var built = block.Nodes
                .Select(child => BuildNativeMenuItem(child, context))
                .OfType<NativeMenuItem>()
                .ToList();

            if (built.Count == 0) continue;

            if (menu.Items.Count > 0) menu.Items.Add(new NativeMenuItemSeparator());

            foreach (var item in built) menu.Items.Add(item);
        }
    }

    public RibbonModel BuildRibbon(CommandContext context) => ribbon.Build(context);

    public Func<bool>? Blocked { get; set; }

    public Func<bool>? Suspended { get; set; }

    private bool Available(CommandDefinition command, CommandContext context) =>
        Suspended?.Invoke() != true && command.IsApplicable(context);

    public void Execute(CommandDefinition command, CommandContext context)
    {
        if (Blocked?.Invoke() == true) return;
        executor.Execute(command, context);
    }

    public CommandDefinition? FindCommand(string id) => registry.Find(new CommandId(id));

    private static Control GroupHeader(string title) => new MenuItem
    {
        Focusable = false,
        IsHitTestVisible = false,
        IsTabStop = false,
        Header = new Border
        {
            Padding = new Avalonia.Thickness(4, 6, 12, 3),
            Child = new TextBlock
            {
                Text = title.ToUpperInvariant(),
                FontSize = 10,
                LetterSpacing = 0.6,
                Opacity = 0.6,
            },
        },
    };

    private MenuItem? BuildMenuItem(CommandNode node, CommandContext context)
    {
        if (node.Command is { } command)
        {
            var leaf = new MenuItem
            {
                Header = node.Segment,
                InputGesture = ParseGesture(command.DefaultGesture),
                IsEnabled = Available(command, context),
            };

            leaf.Click += (_, _) =>
            {
                if (!textEditing.TryRoute(command.DefaultGesture)) Execute(command, workspace.Context);
            };
            if (registry.DescriptionOf(command) is { } description) ToolTip.SetTip(leaf, description);
            return leaf;
        }

        var group = new MenuItem { Header = node.Segment, IsEnabled = Suspended?.Invoke() != true };

        FillItems(group.Items, node, context);

        return group.Items.Count == 0 ? null : group;
    }

    private NativeMenuItem? BuildNativeMenuItem(CommandNode node, CommandContext context)
    {
        if (node.Command is { } command)
        {
            var leaf = new NativeMenuItem(node.Segment)
            {
                Gesture = NativeGesture(ParseGesture(command.DefaultGesture)),
                IsEnabled = Available(command, context),
            };

            leaf.Click += (_, _) =>
            {
                if (!textEditing.TryRoute(command.DefaultGesture)) Execute(command, workspace.Context);
            };
            return leaf;
        }

        var submenu = new NativeMenu();

        FillNativeItems(submenu, node, context);

        return submenu.Items.Count == 0 ? null : new NativeMenuItem(node.Segment) { Menu = submenu, IsEnabled = Suspended?.Invoke() != true };
    }

    private static KeyGesture? NativeGesture(KeyGesture? gesture) =>
        gesture is null || gesture.KeyModifiers != KeyModifiers.None || gesture.Key is >= Key.F1 and <= Key.F24
            ? gesture
            : null;

    private static KeyGesture? ParseGesture(string? gesture) =>
        PulseOffice.UI.Controls.Shortcuts.ShortcutGesture.Parse(gesture) is { } parsed
            ? new KeyGesture(parsed.Key, parsed.Modifiers)
            : null;
}
