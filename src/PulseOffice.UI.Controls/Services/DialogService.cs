using Avalonia.Controls;

namespace PulseOffice.UI.Controls.Services;

public sealed record DialogOptions
{
    public required string TitleKey { get; init; }

    public IReadOnlyList<object> TitleArguments { get; init; } = [];

    public double PreferredWidth { get; init; } = 520;

    public double PreferredHeight { get; init; } = double.NaN;

    public bool CanCancelWithEscape { get; init; } = true;

    public bool CanCancelWithBackdrop { get; init; }

    public string? AccessibleDescription { get; init; }
}

public interface IDialogService
{
    bool IsDialogOpen { get; }

    Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default);

    Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default);

    void Close(object? result);
}

public interface IDialogSizeHint
{
    double? PreferredWidth { get; }
}

public interface IDialogContent
{
    event EventHandler<object?>? Completed;

    bool CanClose => true;
}
