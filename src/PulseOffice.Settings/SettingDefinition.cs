using PulseOffice.Foundation.Modules;

namespace PulseOffice.Settings;

public enum SettingScope
{
    Factory,

    Policy,

    User,

    Document,
}

public static class SettingScopes
{
    public static int Precedence(SettingScope scope) => scope switch
    {
        SettingScope.Factory => 0,
        SettingScope.User => 1,
        SettingScope.Document => 2,
        SettingScope.Policy => 3,
        _ => 0,
    };

    public static IReadOnlyList<SettingScope> ByPrecedence { get; } =
    [.. Enum.GetValues<SettingScope>().OrderBy(Precedence)];
}

public enum SettingKind { Boolean, Integer, Decimal, Text, Choice, Path, Color, Keys }

public sealed record SettingChoice(string Value);

public sealed record SettingDefinition
{
    public required string Key { get; init; }

    public required SettingKind Kind { get; init; }
    public required object DefaultValue { get; init; }

    public required string Category { get; init; }

    public string TitleKey => $"setting.{Key}.title";

    public string DescriptionKey => $"setting.{Key}.description";

    public string KeywordsKey => $"setting.{Key}.keywords";

    public string UnitKey => $"setting.{Key}.unit";

    public string? OptionsSource { get; init; }

    public string ChoiceKey(string value) => $"setting.{Key}.choice.{value}";

    public string ChoiceDescriptionKey(string value) => $"setting.{Key}.choice.{value}.description";

    public ModuleKind Module { get; init; } = ModuleKind.Shell;

    public SettingScope MaximumScope { get; init; } = SettingScope.User;

    public IReadOnlyList<SettingChoice> Choices { get; init; } = [];

    public double? Minimum { get; init; }
    public double? Maximum { get; init; }

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public bool RequiresRestart { get; init; }

    public bool IsAdvanced { get; init; }

    public bool IsValid(object? value)
    {
        if (value is null) return false;

        return Kind switch
        {
            SettingKind.Boolean => value is bool,
            SettingKind.Integer => Numeric(value) is { } i && Math.Floor(i) == i && InRange(i),
            SettingKind.Decimal => Numeric(value) is { } d && InRange(d),
            SettingKind.Choice => value is string choice && Choices.Any(c => c.Value == choice),
            SettingKind.Text or SettingKind.Path or SettingKind.Color or SettingKind.Keys => value is string,
            _ => false,
        };
    }

    private double? Numeric(object value) => value switch
    {
        int or long or short or byte or sbyte or ushort or uint or ulong or float or double or decimal
            => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture) is var number && double.IsFinite(number) ? number : null,
        _ => null,
    };

    private bool InRange(double value) =>
        (Minimum is null || value >= Minimum) && (Maximum is null || value <= Maximum);
}

public interface ISettingsProvider
{
    IEnumerable<SettingDefinition> GetSettings();
}
