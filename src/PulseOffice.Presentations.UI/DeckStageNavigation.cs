using PulseOffice.Foundation.Zoom;
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

internal sealed class DeckStageNavigation(DeckViewState state, IDeckViewHost host, PulseOffice.UI.Controls.Zoom.IZoomLadder zoomLadder)
{
    private readonly DeckViewState _state = state;
    private readonly IDeckViewHost _host = host;
    private readonly SlideView _canvas = state.Canvas;
    private readonly DeckViewOptions _options = state.Options;
    private readonly PulseOffice.UI.Controls.Zoom.IZoomLadder _zoomLadder = zoomLadder;

    public ScrollViewer? Scroll
    {
        get => _stageScroll;
        set => _stageScroll = value;
    }

    public void EndPan() => _panFrom = null;

    private ScrollViewer? _stageScroll;

    public void SetZoom(ViewZoom zoom) => SetZoom(zoom, record: true);

    public void SetZoom(ViewZoom zoom, bool record)
    {
        if (record) _options.ZoomHistory.Record(_canvas.Zoom, zoom);
        _canvas.Zoom = zoom;
        if (_stageScroll is { } scroll)
        {
            scroll.HorizontalScrollBarVisibility = zoom.Mode == ViewZoomMode.Fixed
                ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                : Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
            scroll.VerticalScrollBarVisibility = zoom.Mode == ViewZoomMode.FitWhole
                ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        }
        _host.AttachContext();
    }

    public void OnStageWheel(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0 || e.Delta.Y == 0) return;
        var current = _canvas.EffectiveZoom;
        SetZoom(ZoomLimits.Slides.At(e.Delta.Y > 0 ? _zoomLadder.StepIn(current) : _zoomLadder.StepOut(current)));
        e.Handled = true;
    }

    private (Point Pointer, Vector Offset)? _panFrom;

    public void OnStagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panFrom is not { } from || _stageScroll is not { } scroll) return;
        var now = e.GetPosition(scroll);
        scroll.Offset = from.Offset - (now - from.Pointer);
    }

    public bool HandleTool(PointerPressedEventArgs e)
    {
        if (_stageScroll is not { } scroll) return false;
        switch (_options.Tool)
        {
            case CanvasPointerTool.Pan:
                _panFrom = (e.GetPosition(scroll), scroll.Offset);
                e.Handled = true;
                return true;
            case CanvasPointerTool.Zoom:
                var current = _canvas.EffectiveZoom;
                var at = e.GetPosition(_canvas);
                var next = e.KeyModifiers.HasFlag(KeyModifiers.Alt) ? _zoomLadder.StepOut(current) : _zoomLadder.StepIn(current);
                SetZoom(ZoomLimits.Slides.At(next));
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    scroll.Offset = new Vector(at.X * next / current - scroll.Viewport.Width / 2, at.Y * next / current - scroll.Viewport.Height / 2),
                    Avalonia.Threading.DispatcherPriority.Background);
                e.Handled = true;
                return true;
            default:
                return false;
        }
    }

    public void ZoomToSelection()
    {
        if (_stageScroll is not { } scroll || _canvas.Selection.Count == 0) return;
        var left = _canvas.Selection.Min(element => element.Bounds.Left.Value);
        var top = _canvas.Selection.Min(element => element.Bounds.Top.Value);
        var right = _canvas.Selection.Max(element => element.Bounds.Left.Value + element.Bounds.Width.Value);
        var bottom = _canvas.Selection.Max(element => element.Bounds.Top.Value + element.Bounds.Height.Value);
        var width = Math.Max(1, right - left) / SlideView.TwipsPerPixel;
        var height = Math.Max(1, bottom - top) / SlideView.TwipsPerPixel;
        var factor = ZoomLimits.Slides.Clamp(0.85 * Math.Min(scroll.Viewport.Width / width, scroll.Viewport.Height / height));
        SetZoom(ZoomLimits.Slides.At(factor));
        var anchor = _canvas.Selection[0];
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var origin = _canvas.BoundsOf(anchor);
            var x = origin.X + (left - anchor.Bounds.Left.Value) / SlideView.TwipsPerPixel * factor;
            var y = origin.Y + (top - anchor.Bounds.Top.Value) / SlideView.TwipsPerPixel * factor;
            scroll.Offset = new Vector(x + width * factor / 2 - scroll.Viewport.Width / 2, y + height * factor / 2 - scroll.Viewport.Height / 2);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
