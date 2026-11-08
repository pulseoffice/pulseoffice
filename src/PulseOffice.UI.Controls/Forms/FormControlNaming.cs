using PulseOffice.Document.Forms;
using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.Forms;

public interface IFormControlNaming
{
    string NextName(FormControlKind kind, IEnumerable<string> taken);
}

public sealed class FormControlNaming(ITextCatalog texts) : IFormControlNaming
{
    public string NextName(FormControlKind kind, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var key = $"form.defaultName.{kind}";
        var prefix = texts.Contains(key) ? texts[key] : kind.ToString();

        for (var index = 1; ; index++)
        {
            var candidate = $"{prefix} {index}";
            if (!used.Contains(candidate)) return candidate;
        }
    }
}
