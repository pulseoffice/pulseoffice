using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.UI.Editing;

public sealed class PendingCharacterFormat
{
    private CharacterProperties? _properties;
    private DocumentPosition? _at;

    public void Toggle(CharacterToggle toggle, bool on, DocumentPosition at)
    {
        var current = _at == at ? _properties ?? CharacterProperties.None : CharacterProperties.None;
        _properties = toggle switch
        {
            CharacterToggle.Bold => current with { Bold = on },
            CharacterToggle.Italic => current with { Italic = on },
            _ => current with { Underline = on },
        };
        _at = at;
    }

    public bool? State(CharacterToggle toggle, DocumentPosition at) => _at != at || _properties is not { } properties
        ? null
        : toggle switch
        {
            CharacterToggle.Bold => properties.Bold,
            CharacterToggle.Italic => properties.Italic,
            _ => properties.Underline,
        };

    public CharacterProperties? Take(DocumentPosition at)
    {
        var taken = _at == at ? _properties : null;
        _properties = null;
        _at = null;
        return taken;
    }
}
