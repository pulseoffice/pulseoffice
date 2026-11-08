namespace PulseOffice.Document.Forms;

public sealed record OptionRelease(int Index, FormControlProperties Released);

public interface IOptionGroups
{
    IReadOnlyList<OptionRelease> Release(IReadOnlyList<FormControlObject?> controls, int chosen);
}

public sealed class OptionGroups : IOptionGroups
{
    public IReadOnlyList<OptionRelease> Release(IReadOnlyList<FormControlObject?> controls, int chosen)
    {
        ArgumentNullException.ThrowIfNull(controls);

        if (chosen < 0 || chosen >= controls.Count
            || controls[chosen] is not { ControlKind: FormControlKind.OptionButton } option
            || option.Properties.Value.Checked != CheckState.Checked)
            return [];

        if (string.IsNullOrEmpty(option.Properties.Group)) return [];

        var releases = new List<OptionRelease>();

        for (var index = 0; index < controls.Count; index++)
        {
            if (index == chosen || controls[index] is not { ControlKind: FormControlKind.OptionButton } sibling) continue;
            if (!string.Equals(sibling.Properties.Group, option.Properties.Group, StringComparison.Ordinal)) continue;
            if (sibling.Properties.Value.Checked == CheckState.Unchecked) continue;

            releases.Add(new OptionRelease(index, sibling.Properties with
            {
                Value = sibling.Properties.Value with { Checked = CheckState.Unchecked },
            }));
        }

        return releases;
    }
}
