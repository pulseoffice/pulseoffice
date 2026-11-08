using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.UI.Controls.Dialogs;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record DataFormChoice(IReadOnlyDictionary<int, IReadOnlyList<string>> Records);

public sealed class DataFormModel
{
    private readonly List<IReadOnlyList<string>> _original;
    private readonly List<string[]> _current;

    public DataFormModel(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> records)
    {
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        ArgumentNullException.ThrowIfNull(records);
        _original = [.. records.Select(record => (IReadOnlyList<string>)[.. Normalized(record)])];
        _current = [.. _original.Select(record => record.ToArray())];
        if (_current.Count == 0) Append();
    }

    public IReadOnlyList<string> Headers { get; }

    public int Count => _current.Count;

    public int Position { get; private set; }

    public IReadOnlyList<string> Record(int index) => _current[index];

    public bool IsNew(int index) => index >= _original.Count;

    public void Move(int position) => Position = Math.Clamp(position, 0, _current.Count - 1);

    public void Set(int field, string value) => _current[Position][field] = value ?? string.Empty;

    public void Append()
    {
        _current.Add(new string[Headers.Count].Select(_ => string.Empty).ToArray());
        Position = _current.Count - 1;
    }

    public void Restore()
    {
        if (IsNew(Position)) Array.Fill(_current[Position], string.Empty);
        else _current[Position] = [.. _original[Position]];
    }

    public bool Changed(int index) =>
        IsNew(index) ? _current[index].Any(value => value.Length > 0) : !_current[index].SequenceEqual(_original[index], StringComparer.Ordinal);

    public IReadOnlyDictionary<int, IReadOnlyList<string>> Edits() =>
        Enumerable.Range(0, _current.Count).Where(Changed).ToDictionary(index => index, index => (IReadOnlyList<string>)_current[index]);

    private IEnumerable<string> Normalized(IReadOnlyList<string> record) =>
        Enumerable.Range(0, Headers.Count).Select(index => index < record.Count ? record[index] ?? string.Empty : string.Empty);
}

public sealed class DataFormView : LedgerDialogView
{
    private readonly List<TextBox> _fields = [];
    private readonly TextBlock _position;
    private bool _loading;

    public DataFormView(LedgerDialogKit kit, DataFormModel model)
        : base(kit, "dataForm.explanation")
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _position = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = Avalonia.Media.FontWeight.SemiBold, MinWidth = 120 };
        First = Nav("dataForm.first", "dataForm.nav.first", () => Go(0));
        Previous = Nav("dataForm.previous", "dataForm.nav.previous", () => Go(Model.Position - 1));
        Next = Nav("dataForm.next", "dataForm.nav.next", () => Go(Model.Position + 1));
        Last = Nav("dataForm.last", "dataForm.nav.last", () => Go(Model.Count - 1));
        New = new Button { Content = kit["dataForm.new"] };
        New.Click += (_, _) =>
        {
            Model.Append();
            Load();
        };
        Restore = new Button { Content = kit["dataForm.restore"] };
        Restore.Click += (_, _) =>
        {
            Model.Restore();
            Load();
        };
        Sketch = new CellGridSketch(kit, 64);

        var navigator = new StackPanel
        {
            Spacing = kit.Metrics.SpacingSmall,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = kit.Metrics.SpacingSmall, Children = { First, Previous, _position, Next, Last } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = kit.Metrics.SpacingSmall, Children = { New, Restore } },
            },
        };
        Layout.Preview = PreviewPane("dataForm.preview", 280, Sketch);
        var records = Layout.AddSection((string?)null);
        records.Add(null, navigator, DialogRowKind.Wide);
        var fields = Layout.AddSection(kit["dataForm.section.fields"]);
        for (var index = 0; index < model.Headers.Count; index++)
        {
            var field = index;
            var box = new TextBox { MinWidth = 220 };
            Avalonia.Automation.AutomationProperties.SetName(box, model.Headers[index]);
            box.TextChanged += (_, _) =>
            {
                if (_loading) return;
                Model.Set(field, box.Text ?? string.Empty);
                Show();
            };
            _fields.Add(box);
            fields.Add(model.Headers[index], box);
        }

        Finish("dataForm.confirm", fields);
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.PageDown) Go(Model.Position + 1);
            else if (args.Key == Key.PageUp) Go(Model.Position - 1);
            else return;
            args.Handled = true;
        };
        Load();
    }

    public DataFormModel Model { get; }

    public IReadOnlyList<TextBox> Fields => _fields;

    public Button First { get; }

    public Button Previous { get; }

    public Button Next { get; }

    public Button Last { get; }

    public Button New { get; }

    public Button Restore { get; }

    public CellGridSketch Sketch { get; }

    public string PositionText => _position.Text ?? string.Empty;

    public void Go(int position)
    {
        Model.Move(position);
        Load();
    }

    protected override object? Result() => new DataFormChoice(Model.Edits());

    private Button Nav(string tipKey, string icon, Action run)
    {
        var button = new Button { Content = Kit.Icon(icon, 0.8), Padding = new Thickness(6, 4) };
        button.Classes.Add("dialogQuiet");
        ToolTip.SetTip(button, Kit[tipKey]);
        Avalonia.Automation.AutomationProperties.SetName(button, Kit[tipKey]);
        button.Click += (_, _) => run();
        return button;
    }

    private void Load()
    {
        _loading = true;
        var record = Model.Record(Model.Position);
        for (var index = 0; index < _fields.Count; index++) _fields[index].Text = record[index];
        _loading = false;
        Show();
    }

    private void Show()
    {
        _position.Text = Model.IsNew(Model.Position)
            ? Kit["dataForm.newRecord"]
            : Kit.Texts.Format("dataForm.position", Model.Position + 1, Model.Count);
        First.IsEnabled = Previous.IsEnabled = Model.Position > 0;
        Next.IsEnabled = Last.IsEnabled = Model.Position < Model.Count - 1;
        Restore.IsEnabled = Model.Changed(Model.Position);

        var start = Math.Clamp(Model.Position - 2, 0, Math.Max(0, Model.Count - (SketchCells.Rows - 1)));
        var rows = new List<IReadOnlyList<SketchCell>>
        {
            Model.Headers.Take(SketchCells.Columns).Select(header => new SketchCell(header, Header: true)).ToList(),
        };
        for (var index = start; index < Math.Min(Model.Count, start + SketchCells.Rows - 1); index++)
            rows.Add([.. Model.Record(index).Take(SketchCells.Columns).Select(value => new SketchCell(value, Marked: index == Model.Position))]);
        Sketch.Show(rows);
        Hold(Model.Edits().Count == 0);
    }
}
