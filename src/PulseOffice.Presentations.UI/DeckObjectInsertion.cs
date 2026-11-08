using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckObjectInsertion(DeckViewState state, IDeckViewHost host, PulseOffice.UI.Controls.Objects.IRasterImageFiles images, PulseOffice.Drawing.Assets.IImageExtents extents, Panel modeHost)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly Presentation _deck = state.Deck;
    private readonly DeckActions _actions = state.Actions;
    private readonly ITextCatalog _texts = state.Texts;
    private readonly DesignTokens.Metrics _metrics = state.Metrics;
    private readonly PulseOffice.UI.Controls.Objects.IRasterImageFiles _images = images;
    private readonly Panel _modeHost = modeHost;
    private Control? _openPrompt;

    public bool InsertImageBytes(byte[] data, string name, string? linkPath = null)
    {
        if (_state.Current is not { } slide || _images.PixelSize(data) is not var (pw, ph)) return false;

        var largest = new PulseOffice.Foundation.Units.DocumentSize(
            new PulseOffice.Foundation.Units.Twip(Math.Min(PulseOffice.Foundation.Units.Twip.FromCentimeters(24).Value, _deck.Size.Width.Value)), _deck.Size.Height);
        var natural = extents.Natural(pw, ph > 0 && pw > 0 ? ph : (int)Math.Round(Math.Max(1, pw) * 0.66), new PulseOffice.Drawing.Assets.ImageBounds(largest, PulseOffice.Foundation.Units.Twip.FromCentimeters(4)));
        var (w, h) = (natural.Width, natural.Height);
        var x = new PulseOffice.Foundation.Units.Twip((_deck.Size.Width.Value - w.Value) / 2);
        var y = new PulseOffice.Foundation.Units.Twip((_deck.Size.Height.Value - h.Value) / 2);

        var id = Guid.NewGuid().ToString("N");
        PulseOffice.Document.Embedding.EmbeddedObject image = linkPath is null && IsSvg(name)
            ? new PulseOffice.Drawing.Assets.VectorGraphicObject
            {
                Id = id,
                NaturalSize = new PulseOffice.Foundation.Units.DocumentSize(w, h),
                Asset = new PulseOffice.Drawing.Assets.VectorAsset
                {
                    Id = id,
                    Name = Path.GetFileNameWithoutExtension(name),
                    SourceKind = PulseOffice.Drawing.Assets.VectorSourceKind.InlineSvg,
                    SvgContent = System.Text.Encoding.UTF8.GetString(data),
                },
            }
            : new PulseOffice.Drawing.Assets.RasterImageObject
            {
                Id = id,
                Data = data,
                MediaType = _images.MediaTypeOf(name),
                LinkPath = linkPath,
                PixelWidth = pw,
                PixelHeight = ph,
                NaturalSize = new PulseOffice.Foundation.Units.DocumentSize(w, h),
            };

        _actions.InsertObject(slide, new ObjectElement
        {
            Id = image.Id,
            Bounds = new PulseOffice.Foundation.Units.DocumentRect(x, y, w, h),
            ZOrder = slide.Elements.Count,
            Content = image,
        });
        return true;
    }

    private bool IsSvg(string name) => string.Equals(Path.GetExtension(name), ".svg", StringComparison.OrdinalIgnoreCase);

    public System.Threading.Tasks.Task InsertFormulaAsync()
    {
        _host.SetViewMode(DeckViewMode.Normal);
        PromptText(_texts["deck.formula.placeholder"], expression =>
        {
            if (_state.Current is not { } slide || string.IsNullOrWhiteSpace(expression)) return;

            var w = PulseOffice.Foundation.Units.Twip.FromCentimeters(8);
            var h = PulseOffice.Foundation.Units.Twip.FromCentimeters(3);
            var x = new PulseOffice.Foundation.Units.Twip((_deck.Size.Width.Value - w.Value) / 2);
            var y = new PulseOffice.Foundation.Units.Twip((_deck.Size.Height.Value - h.Value) / 2);

            var content = new PulseOffice.Document.Embedding.FormulaObject
            {
                Id = Guid.NewGuid().ToString("N"),
                Expression = expression,
            };

            _actions.InsertObject(slide, new ObjectElement
            {
                Id = content.Id,
                Bounds = new PulseOffice.Foundation.Units.DocumentRect(x, y, w, h),
                ZOrder = slide.Elements.Count,
                Content = content,
            });
        });
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private void PromptText(string placeholder, Action<string> onConfirm)
    {
        var box = new TextBox { PlaceholderText = placeholder, Width = 380 };
        var host = new Border
        {
            Child = box,
            Padding = new Thickness(_metrics.SpacingMedium),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 60, 0, 0),
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        if (_openPrompt is { } previous) _modeHost.Children.Remove(previous);
        _openPrompt = host;
        _modeHost.Children.Add(host);

        void Close()
        {
            if (ReferenceEquals(_openPrompt, host)) _openPrompt = null;
            _modeHost.Children.Remove(host);
        }

        box.LostFocus += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!host.IsKeyboardFocusWithin && box.ContextFlyout is not { IsOpen: true } && box.ContextMenu is not { IsOpen: true }) Close();
        });

        box.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) { var t = box.Text ?? string.Empty; Close(); onConfirm(t); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Avalonia.Threading.Dispatcher.UIThread.Post(() => box.Focus());
    }
}
