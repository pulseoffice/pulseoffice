using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;

namespace PulseOffice.Shell;

public static class ShellSettingKeys
{
    public const string Theme = "appearance.theme";
    public const string Density = "appearance.density";
    public const string Language = "general.language";
    public const string DefaultFormat = "documents.defaultFormat";
    public const string AskFormatOnSave = "documents.askFormatOnSave";
    public const string AutoSaveMinutes = "documents.autoSaveMinutes";
    public const string RecentDocuments = "general.recentDocuments";
    public const string MacroSecurity = PulseOffice.UI.Controls.Macros.MacroTrustPolicy.SettingKey;
    public const string PythonEngine = "macro.pythonEngine";
    public const string LogLevel = "diagnostics.logLevel";
    public const string WebAccess = "security.webAccess";
    public const string RibbonVisible = "interface.ribbonVisible";
    public const string StatusBarVisible = "interface.statusBarVisible";
    public const string DocumentTabsVisible = "interface.documentTabsVisible";
    public const string InterfaceMode = "interface.mode";
    public const string ScannerDevice = "scanner.device";
    public const string ScannerResolution = "scanner.resolution";
    public const string ScannerColour = "scanner.colour";
    public const string ScannerDevices = "scanner.devices";
}

public sealed class ShellSettings : ISettingsProvider
{
    public IEnumerable<SettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = ShellSettingKeys.RecentDocuments,
            Kind = SettingKind.Integer,
            DefaultValue = 20,
            Minimum = 0,
            Maximum = 100,
            Category = "general",
        },

        new()
        {
            Key = ShellSettingKeys.Language,
            Kind = SettingKind.Choice,
            DefaultValue = "system",
            Category = "language",
            Choices = [new("system"), new("it"), new("en"), new("de"), new("es"), new("fr")],
        },

        new()
        {
            Key = ShellSettingKeys.Theme,
            Kind = SettingKind.Choice,
            DefaultValue = "system",
            Category = "appearance",
            Choices = [new("system"), new("light"), new("dark")],
        },

        new()
        {
            Key = ShellSettingKeys.Density,
            Kind = SettingKind.Choice,
            DefaultValue = "compact",
            Category = "appearance",
            Choices = [new("compact"), new("comfortable"), new("touch")],
        },

        new()
        {
            Key = ShellSettingKeys.InterfaceMode,
            Kind = SettingKind.Choice,
            DefaultValue = "ribbon",
            Category = "appearance/interface",
            Choices = [new("ribbon"), new("compact")],
        },

        new()
        {
            Key = ShellSettingKeys.RibbonVisible,
            Kind = SettingKind.Boolean,
            DefaultValue = true,
            Category = "appearance/interface",
        },

        new()
        {
            Key = ShellSettingKeys.StatusBarVisible,
            Kind = SettingKind.Boolean,
            DefaultValue = true,
            Category = "appearance/interface",
        },

        new()
        {
            Key = ShellSettingKeys.DocumentTabsVisible,
            Kind = SettingKind.Boolean,
            DefaultValue = true,
            Category = "appearance/interface",
        },

        new()
        {
            Key = ShellSettingKeys.DefaultFormat,
            Kind = SettingKind.Choice,
            DefaultValue = "odf",
            Category = "documents",
            Choices = [new("odf"), new("ooxml")],
        },

        new()
        {
            Key = ShellSettingKeys.AskFormatOnSave,
            Kind = SettingKind.Boolean,
            DefaultValue = true,
            Category = "documents",
        },

        new()
        {
            Key = ShellSettingKeys.AutoSaveMinutes,
            Kind = SettingKind.Integer,
            DefaultValue = 10,
            Minimum = 0,
            Maximum = 120,
            Category = "documents/saving",
        },

        new()
        {
            Key = ShellSettingKeys.ScannerDevice,
            Kind = SettingKind.Text,
            DefaultValue = string.Empty,
            Category = "devices/scanner",
            OptionsSource = ShellSettingKeys.ScannerDevices,
        },

        new()
        {
            Key = ShellSettingKeys.ScannerResolution,
            Kind = SettingKind.Integer,
            DefaultValue = 300L,
            Minimum = 72,
            Maximum = 1200,
            Category = "devices/scanner",
        },

        new()
        {
            Key = ShellSettingKeys.ScannerColour,
            Kind = SettingKind.Boolean,
            DefaultValue = true,
            Category = "devices/scanner",
        },

        new()
        {
            Key = ShellSettingKeys.MacroSecurity,
            Kind = SettingKind.Choice,
            DefaultValue = "high",
            Category = "security/macros",
            Choices = [new("veryHigh"), new("high"), new("medium"), new("low")],
        },

        new()
        {
            Key = ShellSettingKeys.WebAccess,
            Kind = SettingKind.Boolean,
            DefaultValue = false,
            Category = "security/network",
        },

        new()
        {
            Key = ShellSettingKeys.PythonEngine,
            Kind = SettingKind.Choice,
            DefaultValue = "ironpython",
            Category = "macros",
            RequiresRestart = true,
            IsAdvanced = true,
            Choices = [new("ironpython"), new("cpython")],
        },

        new()
        {
            Key = ShellSettingKeys.LogLevel,
            Kind = SettingKind.Choice,
            DefaultValue = "information",
            Category = "diagnostics",
            IsAdvanced = true,
            RequiresRestart = true,
            Choices = [new("error"), new("warning"), new("information"), new("debug")],
        },
    ];
}
