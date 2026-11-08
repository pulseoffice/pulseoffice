using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record RandomChoice(
    string Area, RandomDistribution Distribution, double Minimum, double Maximum, double Mean, double Deviation, double Probability, int Trials)
{
    public RandomFillRequest Request(CellRange range) => new(range, Distribution)
    {
        Minimum = Minimum,
        Maximum = Maximum,
        Mean = Mean,
        StandardDeviation = Deviation,
        Probability = Probability,
        Trials = Trials,
    };
}

public interface IRandomSampler
{
    IReadOnlyList<double> Sample(RandomChoice choice, int count);
}

public sealed class RandomSampler(ISheetRandomFill random) : IRandomSampler
{
    private const int PreviewSeed = 20_261_022;

    public IReadOnlyList<double> Sample(RandomChoice choice, int count)
    {
        ArgumentNullException.ThrowIfNull(choice);
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(Math.Max(0, count - 1), 0));
        return [.. random.Plan(choice.Request(range) with { Seed = PreviewSeed })
            .Select(entry => entry.Value.TryCoerceToNumber(out var number) ? number : double.NaN)
            .Where(double.IsFinite)];
    }
}

public sealed class RandomView : LedgerDialogView
{
    private const int Samples = 400;
    private const int Bins = 12;
    private const double ChartHeight = 70;

    private readonly IRandomSampler _sampler;
    private readonly Dictionary<string, DialogRow> _rows = new(StringComparer.Ordinal);
    private readonly Grid _bars = new() { Height = ChartHeight };
    private readonly TextBlock _range;

    public RandomView(LedgerDialogKit kit, IRandomSampler sampler, string area, IReferenceSource ranges)
        : base(kit, "random.explanation")
    {
        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        Area = new RangeFieldControl(new RangeField("area", kit["random.range"], area, ranges), kit.Texts, kit.Metrics);
        Distribution = kit.Choices("random.distribution",
        [
            ("uniform", "random.kind.uniform", "random.uniform"),
            ("uniformInteger", "random.kind.uniformInteger", "random.uniformInteger"),
            ("normal", "random.kind.normal", "random.normal"),
            ("bernoulli", "random.kind.bernoulli", "random.bernoulli"),
            ("binomial", "random.kind.binomial", "random.binomial"),
        ], 0, showLabels: true, columns: 3);
        Minimum = NumberBox(0);
        Maximum = NumberBox(1);
        Mean = NumberBox(0);
        Deviation = NumberBox(1);
        Probability = NumberBox(0.5);
        Trials = NumberBox(10);
        Sample = new CellGridSketch(kit, 70);
        _range = kit.Note();

        var chart = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 6, 6, 0),
            Child = _bars,
        }.Themed(Border.BackgroundProperty, Palette.Keys.Surface)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        for (var bin = 0; bin < Bins; bin++) _bars.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        Layout.Preview = PreviewPane("random.preview", 220, chart, _range, Kit.Caption("random.firstValues"), Sample);

        var where = Layout.AddSection(kit["random.section.where"]);
        where.Add(kit["random.range"], Area);
        where.Add(kit["random.distribution"], Distribution, alignTop: true);
        var parameters = Layout.AddSection(kit["random.section.parameters"]);
        _rows["minimum"] = parameters.Add(kit["random.minimum"], Minimum);
        _rows["maximum"] = parameters.Add(kit["random.maximum"], Maximum);
        _rows["mean"] = parameters.Add(kit["random.mean"], Mean);
        _rows["deviation"] = parameters.Add(kit["random.deviation"], Deviation);
        _rows["probability"] = parameters.Add(kit["random.probability"], Probability);
        _rows["trials"] = parameters.Add(kit["random.trials"], Trials);
        Finish("random.confirm", parameters);

        Area.TextChanged += (_, _) => Refresh();
        Distribution.SelectionChanged += (_, _) => Refresh();
        foreach (var box in new[] { Minimum, Maximum, Mean, Deviation, Probability, Trials }) box.TextChanged += (_, _) => Refresh();
        Refresh();
    }

    public RangeFieldControl Area { get; }

    public IconChoiceGroup Distribution { get; }

    public TextBox Minimum { get; }

    public TextBox Maximum { get; }

    public TextBox Mean { get; }

    public TextBox Deviation { get; }

    public TextBox Probability { get; }

    public TextBox Trials { get; }

    public CellGridSketch Sample { get; }

    public IReadOnlyList<double> Heights => [.. _bars.Children.OfType<Border>().Select(bar => bar.Height)];

    public bool ParameterVisible(string key) => _rows[key].IsVisible;

    public RandomChoice? Choice
    {
        get
        {
            var kind = Kind;
            var values = Parameters.Select(parameter => Uses(kind, parameter.Key) ? ReadNumber(parameter.Box) : parameter.Fallback).ToArray();
            if (values.Any(value => value is null)) return null;
            return new RandomChoice(Area.Text, kind,
                values[0]!.Value, values[1]!.Value, values[2]!.Value, values[3]!.Value, values[4]!.Value,
                (int)Math.Clamp(Math.Round(values[5]!.Value), int.MinValue, int.MaxValue));
        }
    }

    protected override object? Result() => Choice;

    private RandomDistribution Kind => (RandomDistribution)Math.Max(Distribution.SelectedIndex, 0);

    private IEnumerable<(string Key, TextBox Box, double? Fallback)> Parameters =>
    [
        ("minimum", Minimum, 0), ("maximum", Maximum, 1), ("mean", Mean, 0),
        ("deviation", Deviation, 1), ("probability", Probability, 0.5), ("trials", Trials, 10),
    ];

    private bool Uses(RandomDistribution kind, string key) => key switch
    {
        "minimum" or "maximum" => kind is RandomDistribution.Uniform or RandomDistribution.UniformInteger,
        "mean" or "deviation" => kind == RandomDistribution.Normal,
        "probability" => kind is RandomDistribution.Bernoulli or RandomDistribution.Binomial,
        _ => kind == RandomDistribution.Binomial,
    };

    private void Refresh()
    {
        var kind = Kind;
        var uniform = Uses(kind, "minimum");
        foreach (var parameter in Parameters) _rows[parameter.Key].IsVisible = Uses(kind, parameter.Key);

        var choice = Choice;
        var problem = !Area.IsValid ? Kit["status.analysis.badReference"]
            : choice is null ? Kit["series.invalidNumber"]
            : uniform && choice.Minimum > choice.Maximum ? Kit["random.badBounds"]
            : kind == RandomDistribution.Normal && choice.Deviation < 0 ? Kit["random.badDeviation"]
            : choice.Probability is < 0 or > 1 ? Kit["random.badProbability"]
            : kind == RandomDistribution.Binomial && choice.Trials is < 1 or > 1000 ? Kit["random.badTrials"]
            : null;
        Report(problem);
        Draw(problem is null && choice is not null ? _sampler.Sample(choice, Samples) : []);
    }

    private void Draw(IReadOnlyList<double> values)
    {
        _bars.Children.Clear();
        Sample.Show([.. values.Take(SketchCells.Rows).Select(value => (IReadOnlyList<SketchCell>)[new SketchCell(value.ToString("G6", CultureInfo.CurrentCulture), Number: true)])]);
        _range.Text = values.Count == 0 ? string.Empty
            : Kit.Texts.Format("random.rangeNote", values.Min().ToString("G4", CultureInfo.CurrentCulture), values.Max().ToString("G4", CultureInfo.CurrentCulture));
        if (values.Count == 0) return;

        var low = values.Min();
        var high = values.Max();
        var width = high - low;
        var counts = new int[Bins];
        foreach (var value in values) counts[width <= 0 ? 0 : Math.Min(Bins - 1, (int)((value - low) / width * Bins))]++;
        var tallest = Math.Max(1, counts.Max());
        for (var bin = 0; bin < Bins; bin++)
        {
            var bar = new Border
            {
                Height = Math.Max(1, ChartHeight * counts[bin] / tallest),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
                CornerRadius = new CornerRadius(2, 2, 0, 0),
            };
            Grid.SetColumn(bar, bin);
            _bars.Children.Add(bar);
        }
    }
}
