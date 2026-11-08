using Avalonia.Controls;
using PulseOffice.UI.Controls.Tabs;

namespace PulseOffice.Sheets.UI;

public interface ISheetTabSurface
{
    bool Shows(int index);

    Task<string?> EditNameAsync(int index, string current, Func<string, string?> problem);

    Task<T?> ShowAtAsync<T>(int index, Func<Action<T?>, Control> content) where T : class;
}

public sealed class SheetTabSurface(SheetTabStrip strip, TabOverlays overlays) : ISheetTabSurface
{
    public TabOverlays Overlays => overlays;

    public bool Shows(int index) => strip.IsEffectivelyVisible && strip.BoundsOf(index) is not null;

    public Task<string?> EditNameAsync(int index, string current, Func<string, string?> problem) =>
        strip.IsEffectivelyVisible && strip.BoundsOf(index) is { } bounds
            ? overlays.EditAsync(strip, bounds, current, problem)
            : Task.FromResult<string?>(null);

    public Task<T?> ShowAtAsync<T>(int index, Func<Action<T?>, Control> content) where T : class =>
        strip.IsEffectivelyVisible && strip.BoundsOf(index) is { } bounds
            ? overlays.ShowAsync(strip, bounds, content)
            : Task.FromResult<T?>(null);
}
