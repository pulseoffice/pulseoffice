using Avalonia.Controls;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed class PasteSpecialView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly IPasteSketcher _sketcher;
    private readonly IconChoiceGroup _parts;
    private readonly IconChoiceGroup _operations;
    private readonly CheckBox _skipBlanks;
    private readonly CheckBox _transpose;
    private readonly TextBlock _summary;
    private readonly LedgerDialogKit _kit;

    public PasteSpecialView(LedgerDialogKit kit, IPasteSketcher sketcher, PasteSpecialChoice? initial = null)
    {
        ArgumentNullException.ThrowIfNull(kit);
        _kit = kit;
        _sketcher = sketcher ?? throw new ArgumentNullException(nameof(sketcher));
        var start = initial ?? new PasteSpecialChoice();

        _parts = kit.Choices("dialog.pasteSpecial.parts",
        [
            ("all", "paste.part.all", "paste.parts.all"),
            ("formulas", "paste.part.formulas", "paste.parts.formulas"),
            ("values", "paste.part.values", "paste.parts.values"),
            ("formats", "paste.part.formats", "paste.parts.styles"),
            ("comments", "paste.part.comments", "paste.parts.comments"),
            ("validation", "paste.part.validation", "paste.parts.validation"),
        ], (int)start.Part, showLabels: true, columns: 2);

        _operations = kit.Choices("dialog.pasteSpecial.operation",
        [
            ("none", "paste.op.none", "paste.operation.none"),
            ("add", "paste.op.add", "paste.operation.add"),
            ("subtract", "paste.op.subtract", "paste.operation.subtract"),
            ("multiply", "paste.op.multiply", "paste.operation.multiply"),
            ("divide", "paste.op.divide", "paste.operation.divide"),
        ], (int)start.Operation);

        _skipBlanks = kit.Check("dialog.pasteSpecial.skipBlanks", start.SkipBlanks);
        _transpose = kit.Check("dialog.pasteSpecial.transpose", start.Transpose);
        _summary = kit.Note();

        Sketch = new PasteSketchView(kit.Metrics, kit["paste.sketch.clipboard"], kit["paste.sketch.result"]);

        Layout = new TwoPaneDialogLayout(kit.Metrics) { Header = kit.Help("dialog.pasteSpecial.explanation"), Preview = Sketch };
        Layout.AddSection(kit["paste.section.paste"]).Add(null, _parts, DialogRowKind.Wide);
        var operation = Layout.AddSection(kit["paste.section.operation"]);
        operation.Add(null, _operations, DialogRowKind.Wide);
        operation.Add(null, _summary, DialogRowKind.Wide);
        var options = Layout.AddSection(kit["form.section.options"]);
        options.Add(null, _skipBlanks, DialogRowKind.Indented);
        options.Add(null, _transpose, DialogRowKind.Indented);

        _parts.SelectionChanged += (_, _) => Refresh();
        _operations.SelectionChanged += (_, _) => Refresh();
        _skipBlanks.IsCheckedChanged += (_, _) => Refresh();
        _transpose.IsCheckedChanged += (_, _) => Refresh();

        Content = Layout;

        Footer = new DialogFooter()
            .WithCancel(kit["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(kit["paste.link"], () => Completed?.Invoke(this, Choice with { Link = true }), out var link)
            .WithAction(kit["paste.confirm"], () => Completed?.Invoke(this, Choice), primary: true);
        LinkButton = link;
        Avalonia.Automation.AutomationProperties.SetHelpText(link, kit["paste.link.help"]);

        Refresh();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => Layout.SuggestedWidth;

    public TwoPaneDialogLayout Layout { get; }

    public PasteSketchView Sketch { get; }

    public IconChoiceGroup Parts => _parts;

    public IconChoiceGroup Operations => _operations;

    public CheckBox SkipBlanks => _skipBlanks;

    public CheckBox Transpose => _transpose;

    public Button LinkButton { get; }

    public PasteSpecialChoice Choice => new(
        (PastePart)Math.Max(_parts.SelectedIndex, 0),
        (PasteOperation)Math.Max(_operations.SelectedIndex, 0),
        _skipBlanks.IsChecked == true,
        _transpose.IsChecked == true);

    private void Refresh()
    {
        var choice = Choice;
        _operations.IsEnabled = choice.OperationApplies;
        _summary.Text = choice.OperationApplies
            ? _kit[$"paste.operation.{Key(choice.Operation)}.hint"]
            : _kit["paste.operation.notApplicable"];
        Sketch.Show(_sketcher.Sketch(choice));
    }

    private string Key(PasteOperation operation) => operation switch
    {
        PasteOperation.Add => "add",
        PasteOperation.Subtract => "subtract",
        PasteOperation.Multiply => "multiply",
        PasteOperation.Divide => "divide",
        _ => "none",
    };
}
