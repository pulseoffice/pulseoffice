using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class FormFieldsPanel : UserControl
{
    private readonly FormPromptView _fields;

    public FormFieldsPanel(
        IReadOnlyList<FormField> fields,
        Localization.ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(fields);

        _fields = new FormPromptView(string.Empty, fields, texts, density) { ShowButtons = false };

        Content = _fields;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    public IReadOnlyDictionary<string, object> Values => _fields.Values;
}
