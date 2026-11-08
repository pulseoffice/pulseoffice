using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Agent.Hosting;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Transport;
using PulseOffice.Settings;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Settings;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Agent.UI;

public sealed record AgentConnectionServices(
    IAgentServer Server,
    IAgentAccessCode Code,
    IAgentSessions Sessions,
    IAgentOperationLog Log,
    IAgentPreferences Preferences,
    IAgentClientInstructions Instructions,
    IAgentToolTitles Titles,
    ISystemClipboardText Clipboard);

public sealed class AgentConnectionSettingEditor(AgentConnectionServices services) : ISettingEditor
{
    public bool Handles(SettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Key == AgentSettingKeys.Connection;
    }

    public SettingField Create(SettingEditorContext context) => new AgentConnectionField(context, services);
}

internal sealed class AgentConnectionField : SettingField
{
    private const int ShownOperations = 6;
    private readonly AgentConnectionServices _services;
    private readonly Border _card;
    private readonly Ellipse _dot = new() { Width = 9, Height = 9, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _state = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly SelectableTextBlock _address = Monospace();
    private readonly SelectableTextBlock _code = Monospace();
    private readonly SelectableTextBlock _command = Monospace();
    private readonly StackPanel _operations = new() { Spacing = 2 };
    private readonly Control _details;
    private bool _codeShown;

    public AgentConnectionField(SettingEditorContext context, AgentConnectionServices services) : base(context)
    {
        _services = services;
        var metrics = context.Metrics;

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { _dot, _state } };

        var show = Action(context.Text("agent.code.show"), () =>
        {
            _codeShown = !_codeShown;
            Show();
        });
        var regenerate = Action(context.Text("agent.code.regenerate"), () => _services.Code.Regenerate());

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = metrics.SpacingMedium,
            RowSpacing = metrics.SpacingSmall + 2,
        };
        Place(grid, 0, Label(context.Text("agent.address")), _address,
            Buttons(Action(context.Text("agent.copy"), () => Copy(_services.Server.Address?.ToString()))));
        Place(grid, 1, Label(context.Text("agent.code")), _code,
            Buttons(show, Action(context.Text("agent.copy"), () => Copy(_services.Code.Current)), regenerate));
        Place(grid, 2, Label(context.Text("agent.connect")), _command,
            Buttons(Action(context.Text("agent.copy"), () => Copy(Command()))));

        var hint = new TextBlock
        {
            Text = context.Text("agent.connect.hint"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = metrics.FontSizeSmall,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var recent = new TextBlock
        {
            Text = context.Text("agent.operations"),
            FontWeight = FontWeight.Medium,
            Margin = new Thickness(0, metrics.SpacingSmall, 0, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        _details = new StackPanel { Spacing = metrics.SpacingSmall + 2, Children = { grid, hint, recent, _operations } };

        _card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(metrics.SpacingMedium + 2),
            Child = new StackPanel { Spacing = metrics.SpacingMedium, Children = { header, _details } },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        _card.AttachedToVisualTree += (_, _) => Subscribe(true);
        _card.DetachedFromVisualTree += (_, _) => Subscribe(false);
    }

    public override Control View => _card;

    public override bool IsWide => true;

    protected override void Show()
    {
        var server = _services.Server;
        var sessions = _services.Sessions.Active;

        var (text, colour) = server.IsRunning
            ? sessions.Count > 0
                ? (Context.Texts.Format("agent.state.connected", string.Join(", ", sessions.Select(session => session.Client).Distinct(StringComparer.Ordinal))), Palette.Keys.Success)
                : (Context.Text("agent.state.listening"), Palette.Keys.Info)
            : server.Failure is { } failure && _services.Preferences.Enabled
                ? (Context.Texts.Format("agent.state.failed", failure), Palette.Keys.Danger)
                : (Context.Text("agent.state.off"), Palette.Keys.TextDisabled);
        _state.Text = text;
        _dot.Themed(Shape.FillProperty, colour);

        _details.IsVisible = server.IsRunning;
        _address.Text = server.Address?.ToString() ?? string.Empty;
        var code = _services.Code.Current;
        _code.Text = _codeShown ? code : new string('•', 12) + code[^4..];
        _command.Text = _codeShown ? Command() : Context.Text("agent.connect.masked");
        _command.FontFamily = _codeShown ? _address.FontFamily : FontFamily.Default;

        _operations.Children.Clear();
        var recent = _services.Log.Recent.Take(ShownOperations).ToList();
        if (recent.Count == 0)
            _operations.Children.Add(Faint(Context.Text("agent.operations.none")));
        foreach (var operation in recent)
            _operations.Children.Add(Faint(
                $"{operation.When:HH:mm:ss}  ·  {operation.Client}  ·  {_services.Titles.Of(operation.Tool, operation.Title)}  ·  {Context.Text($"agent.outcome.{operation.Outcome.ToString().ToLowerInvariant()}")}"));
    }

    private string Command() =>
        _services.Server.Address is { } address ? _services.Instructions.ClaudeCode(address, _services.Code.Current) : string.Empty;

    private void Copy(string? text)
    {
        if (!string.IsNullOrEmpty(text)) _ = _services.Clipboard.SetTextAsync(text);
    }

    private void Subscribe(bool on)
    {
        if (on)
        {
            _services.Server.StateChanged += OnChanged;
            _services.Sessions.Changed += OnChanged;
            _services.Log.Changed += OnChanged;
            _services.Code.Changed += OnChanged;
            Refresh();
        }
        else
        {
            _services.Server.StateChanged -= OnChanged;
            _services.Sessions.Changed -= OnChanged;
            _services.Log.Changed -= OnChanged;
            _services.Code.Changed -= OnChanged;
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void Place(Grid grid, int row, Control label, Control value, Control actions)
    {
        Grid.SetRow(label, row);
        Grid.SetRow(value, row);
        Grid.SetColumn(value, 1);
        Grid.SetRow(actions, row);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(label);
        grid.Children.Add(value);
        grid.Children.Add(actions);
    }

    private TextBlock Label(string text) => new TextBlock
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        FontSize = Context.Metrics.FontSizeSmall + 1,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private TextBlock Faint(string text) => new TextBlock
    {
        Text = text,
        FontSize = Context.Metrics.FontSizeSmall,
        TextTrimming = TextTrimming.CharacterEllipsis,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private StackPanel Buttons(params Control[] buttons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Context.Metrics.SpacingSmall, VerticalAlignment = VerticalAlignment.Center };
        foreach (var button in buttons) panel.Children.Add(button);
        return panel;
    }

    private Button Action(string text, Action act)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 3), FontSize = Context.Metrics.FontSizeSmall + 1 };
        button.Click += (_, _) =>
        {
            act();
            Refresh();
        };
        return button;
    }

    private static SelectableTextBlock Monospace() => new()
    {
        FontFamily = new FontFamily("Menlo, Consolas, DejaVu Sans Mono, monospace"),
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
