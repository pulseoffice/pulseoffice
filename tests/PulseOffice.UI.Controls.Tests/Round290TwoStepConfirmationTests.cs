using PulseOffice.UI.Controls.Settings;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round290TwoStepConfirmationTests
{
    [Fact]
    public void The_first_press_only_arms_and_the_second_confirms()
    {
        var confirmation = new TwoStepConfirmation();

        Assert.False(confirmation.Press());
        Assert.True(confirmation.IsArmed);
        Assert.True(confirmation.Press());
        Assert.False(confirmation.IsArmed);
    }

    [Fact]
    public void Disarming_needs_two_presses_again()
    {
        var confirmation = new TwoStepConfirmation();
        confirmation.Press();
        confirmation.Disarm();

        Assert.False(confirmation.Press());
    }
}
