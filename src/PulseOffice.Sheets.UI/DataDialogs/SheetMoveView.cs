using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record SheetMoveChoice(int Position, bool Copy, string Name);

public sealed class SheetMovePlan
{
    private readonly IWorksheetTabPolicy _names;

    public SheetMovePlan(IReadOnlyList<string> sheets, int source, IWorksheetTabPolicy names)
    {
        Sheets = sheets ?? throw new ArgumentNullException(nameof(sheets));
        Source = source;
        _names = names ?? throw new ArgumentNullException(nameof(names));
    }

    public IReadOnlyList<string> Sheets { get; }

    public int Source { get; }

    public IReadOnlyList<string> Result(int position, bool copy, string copyName)
    {
        var order = Sheets.ToList();
        var moved = copy ? copyName : order[Source];
        if (!copy) order.RemoveAt(Source);
        var at = copy || position <= Source ? position : position - 1;
        order.Insert(Math.Clamp(at, 0, order.Count), moved);
        return order;
    }

    public string? NameProblem(string name, bool copy)
    {
        if (!copy) return null;
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return null;
        if (!_names.NameAllowed(trimmed)) return "sheet.name.invalid";
        return Sheets.Contains(trimmed, StringComparer.CurrentCultureIgnoreCase) ? "sheet.name.taken" : null;
    }
}

public sealed class SheetMoveView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly LedgerDialogKit _kit;
    private readonly SheetMovePlan _plan;
    private readonly string _suggested;
    private readonly WrapPanel _tabs = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _nameError;
    private readonly DialogRow _nameRow;

    public SheetMoveView(LedgerDialogKit kit, SheetMovePlan plan, string suggestedCopyName)
    {
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _suggested = suggestedCopyName;

        Action = kit.Choices("sheet.move.action",
        [
            ("move", "sheet.position", "sheet.move.moveAction"),
            ("copy", "sort.level.copy", "sheet.move.copyAction"),
        ], 0, showLabels: true);

        Positions = new ListBox
        {
            ItemsSource = (List<string>)[.. plan.Sheets.Select(name => kit.Texts.Format("sheet.move.before", name)), kit["sheet.move.last"]],
            SelectedIndex = plan.Sheets.Count,
            MaxHeight = 200,
        };
        AutomationProperties.SetName(Positions, kit["sheet.move.position"]);

        CopyName = new TextBox { PlaceholderText = suggestedCopyName, MinWidth = 220 };
        _nameError = kit.Note();
        _nameError.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        var layout = new TwoPaneDialogLayout(kit.Metrics)
        {
            Header = kit.Help("sheet.move.help"),
            Preview = new StackPanel { Width = 220, Spacing = kit.Metrics.SpacingSmall, Children = { kit.Caption("sheet.move.result"), _tabs } },
        };
        var what = layout.AddSection(kit.Texts.Format("sheet.move.explanation", plan.Sheets[plan.Source]));
        what.Add(null, Action, DialogRowKind.Wide);
        var where = layout.AddSection(kit["sheet.move.position"]);
        where.Add(null, new PanelListFrame(kit.Metrics, Positions), DialogRowKind.Wide);
        _nameRow = where.Add(kit["sheet.move.name"], new StackPanel { Spacing = 2, Children = { CopyName, _nameError } });
        Layout = layout;
        Content = layout;

        Action.SelectionChanged += (_, _) => Refresh();
        Positions.SelectionChanged += (_, _) => Refresh();
        CopyName.TextChanged += (_, _) => Refresh();

        Footer = new DialogFooter()
            .WithCancel(kit["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(kit["sheet.move.confirm"], () => Completed?.Invoke(this, Choice), out var confirm, primary: true);
        Confirm = confirm;
        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => Layout.SuggestedWidth;

    public TwoPaneDialogLayout Layout { get; }

    public IconChoiceGroup Action { get; }

    public ListBox Positions { get; }

    public TextBox CopyName { get; }

    public Button Confirm { get; }

    public IReadOnlyList<string> ResultTabs => [.. _tabs.Children.OfType<Border>().Select(tab => (tab.Child as TextBlock)?.Text ?? string.Empty)];

    public SheetMoveChoice Choice => new(Math.Max(Positions.SelectedIndex, 0), Action.SelectedIndex == 1, (CopyName.Text ?? string.Empty).Trim());

    private void Refresh()
    {
        var choice = Choice;
        _nameRow.IsVisible = choice.Copy;
        var problem = _plan.NameProblem(choice.Name, choice.Copy);
        _nameError.Text = problem is null ? string.Empty : _kit.Texts.Format(problem, choice.Name);
        _nameError.IsVisible = problem is not null;
        Confirm.IsEnabled = problem is null;

        var shownName = choice.Name.Length > 0 ? choice.Name : _suggested;
        var result = _plan.Result(choice.Position, choice.Copy, shownName);
        var moved = choice.Copy ? shownName : _plan.Sheets[_plan.Source];
        _tabs.Children.Clear();
        foreach (var name in result)
        {
            var highlighted = name == moved;
            var tab = new Border
            {
                Padding = new Thickness(8, 3),
                Margin = new Thickness(0, 0, 2, 2),
                CornerRadius = new CornerRadius(0, 0, 4, 4),
                BorderThickness = new Thickness(1, 0, 1, highlighted ? 2 : 1),
                Child = new TextBlock { Text = name, FontSize = _kit.Metrics.FontSizeSmall, FontWeight = highlighted ? FontWeight.SemiBold : FontWeight.Normal },
            };
            tab.Themed(Border.BorderBrushProperty, highlighted ? "PulseAccent" : Palette.Keys.Border)
               .Themed(Border.BackgroundProperty, highlighted ? "PulseAccentSubtle" : Palette.Keys.Surface);
            _tabs.Children.Add(tab);
        }
    }
}
