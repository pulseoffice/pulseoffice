using PulseOffice.Documents.Controls;
using PulseOffice.Localization;

namespace PulseOffice.Documents.UI.Editing;

public interface IContentControlPlaceholders
{
    string For(ContentControlKind kind);
}

public sealed class ContentControlPlaceholders(ITextCatalog texts) : IContentControlPlaceholders
{
    public string For(ContentControlKind kind) => kind switch
    {
        ContentControlKind.DropDownList or ContentControlKind.ComboBox => texts["contentControl.placeholder.choose"],
        ContentControlKind.Date => texts["contentControl.placeholder.date"],
        ContentControlKind.Picture => texts["contentControl.placeholder.picture"],
        ContentControlKind.CheckBox => string.Empty,
        _ => texts["contentControl.placeholder.text"],
    };
}
