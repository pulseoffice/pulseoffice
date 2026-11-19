using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round253ValidationChoiceOrderTests
{
    private readonly ValidationChoiceOrder _order = new();

    [Fact]
    public void A_sorted_list_puts_numbers_first_then_text_ignoring_case()
    {
        Assert.Equal(["2", "10", "apple", "Banana"], _order.Arrange(["Banana", "10", "apple", "2"], sorted: true));
    }

    [Fact]
    public void An_unsorted_list_keeps_its_order()
    {
        IReadOnlyList<string> choices = ["b", "a"];

        Assert.Same(choices, _order.Arrange(choices, sorted: false));
    }
}
