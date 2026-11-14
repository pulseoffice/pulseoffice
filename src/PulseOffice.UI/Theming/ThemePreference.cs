using Avalonia;
using Avalonia.Styling;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Theming;

public enum ThemePreference { System, Light, Dark }

public interface IThemeService
{
    ThemePreference Preference { get; }

    bool IsDark { get; }

    void Apply(ThemePreference preference);

    event EventHandler? ThemeChanged;
}

public sealed class ThemeService : IThemeService, IDisposable
{
    private readonly Application _application;

    public ThemeService(Application application)
    {
        _application = application;
        _application.ActualThemeVariantChanged += OnVariantChanged;
    }

    public void Dispose() => _application.ActualThemeVariantChanged -= OnVariantChanged;

    private void OnVariantChanged(object? sender, EventArgs e) => ThemeChanged?.Invoke(this, EventArgs.Empty);

    public ThemePreference Preference { get; private set; } = ThemePreference.System;

    public bool IsDark => _application.ActualThemeVariant == ThemeVariant.Dark;

    public event EventHandler? ThemeChanged;

    public void Apply(ThemePreference preference)
    {
        Preference = preference;

        _application.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}

public interface IAccentService
{
    ModuleKind ActiveModule { get; }
    void SetActiveModule(ModuleKind module);
    event EventHandler? AccentChanged;
}

public sealed class AccentService : IAccentService
{
    public const string AccentKey = "PulseAccent";
    public const string AccentTextKey = "PulseAccentText";
    public const string AccentSubtleKey = "PulseAccentSubtle";

    private readonly Application _application;
    private readonly IThemeService _theme;
    private readonly IAccentPalette _shades;

    public AccentService(Application application, IThemeService theme, IAccentPalette shades)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _shades = shades ?? throw new ArgumentNullException(nameof(shades));
        _theme.ThemeChanged += (_, _) => SetActiveModule(ActiveModule);
    }

    private void ApplySystemAccent(Avalonia.Media.Color accent)
    {
        _application.Resources["SystemAccentColor"] = accent;

        _application.Resources["SystemAccentColorLight1"] = Shift(accent, 0.18);
        _application.Resources["SystemAccentColorLight2"] = Shift(accent, 0.34);
        _application.Resources["SystemAccentColorLight3"] = Shift(accent, 0.50);
        _application.Resources["SystemAccentColorDark1"] = Shift(accent, -0.14);
        _application.Resources["SystemAccentColorDark2"] = Shift(accent, -0.26);
        _application.Resources["SystemAccentColorDark3"] = Shift(accent, -0.38);
    }

    private static Avalonia.Media.Color Shift(Avalonia.Media.Color color, double amount)
    {
        var target = amount >= 0 ? 255.0 : 0.0;
        var weight = Math.Abs(amount);

        byte Mix(byte channel) => (byte)Math.Round(channel + (target - channel) * weight);

        return Avalonia.Media.Color.FromArgb(color.A, Mix(color.R), Mix(color.G), Mix(color.B));
    }

    public ModuleKind ActiveModule { get; private set; } = ModuleKind.Shell;

    public event EventHandler? AccentChanged;

    public void SetActiveModule(ModuleKind module)
    {
        ActiveModule = module;

        var hex = module == ModuleKind.Shell
            ? DesignTokens.ShellAccent
            : ModuleIdentity.For(module).AccentHex;

        var brand = Avalonia.Media.Color.Parse(hex);
        var (accent, text) = _shades.For(brand, _theme.IsDark);
        _application.Resources[AccentKey] = new Avalonia.Media.SolidColorBrush(accent);
        _application.Resources[AccentTextKey] = new Avalonia.Media.SolidColorBrush(text);

        _application.Resources[AccentSubtleKey] =
            new Avalonia.Media.SolidColorBrush(accent, DesignTokens.SubtleAccentOpacity);

        ApplySystemAccent(accent);

        AccentChanged?.Invoke(this, EventArgs.Empty);
    }
}
