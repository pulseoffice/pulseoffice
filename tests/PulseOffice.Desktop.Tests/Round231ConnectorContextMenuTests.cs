using PulseOffice.Desktop.Composition;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round231ConnectorContextMenuTests
{
    [Fact]
    public void Connector_ends_is_placed_in_the_slide_object_context_menu()
    {
        Assert.Contains("object/format", PulseContextLayout.Build().PathsFor("deck.connector.sites"));
    }
}
