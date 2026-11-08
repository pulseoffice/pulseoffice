using Avalonia.Controls;
using PulseOffice.Settings;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Settings;

namespace PulseOffice.Shell;

public sealed class InterfaceModeSettingEditor(IGalleryNavigator navigator) : ISettingEditor
{
    public bool Handles(SettingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Key == ShellSettingKeys.InterfaceMode;
    }

    public SettingField Create(SettingEditorContext context) => new InterfaceModeSettingField(context, navigator);
}

internal sealed class InterfaceModeSettingField : SettingField
{
    private readonly IReadOnlyList<string> _modes;
    private readonly ThumbnailGallery<InterfaceModeChoice> _gallery;

    public InterfaceModeSettingField(SettingEditorContext context, IGalleryNavigator navigator) : base(context)
    {
        _modes = [.. context.Definition.Choices.Select(choice => choice.Value)];
        _gallery = new ThumbnailGallery<InterfaceModeChoice>(
            [.. _modes.Select(mode => new ThumbnailGalleryItem<InterfaceModeChoice>(
                new InterfaceModeChoice(KindOf(mode), context.Text(context.Definition.ChoiceKey(mode))),
                context.Text(context.Definition.ChoiceKey(mode))))],
            new InterfaceModePainter(), navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 150,
                ThumbnailHeight = 100,
                Columns = _modes.Count,
                AccessibleName = context.Text(context.Definition.TitleKey),
            },
            context.Metrics);
        _gallery.SelectionChanged += (_, _) =>
        {
            if (_gallery.SelectedIndex >= 0) Commit(_modes[_gallery.SelectedIndex]);
        };
    }

    public override Control View => _gallery;

    public override bool IsWide => true;

    protected override void Show()
    {
        var current = Context.Settings.Get<string>(Definition.Key);
        _gallery.Select(Math.Max(0, _modes.ToList().IndexOf(current ?? string.Empty)));
    }

    private InterfaceLayoutKind KindOf(string mode) => mode == "compact" ? InterfaceLayoutKind.Compact : InterfaceLayoutKind.Ribbon;
}
