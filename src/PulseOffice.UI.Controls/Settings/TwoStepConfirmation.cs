namespace PulseOffice.UI.Controls.Settings;

public sealed class TwoStepConfirmation
{
    public bool IsArmed { get; private set; }

    public bool Press()
    {
        if (IsArmed)
        {
            IsArmed = false;
            return true;
        }

        IsArmed = true;
        return false;
    }

    public void Disarm() => IsArmed = false;
}
