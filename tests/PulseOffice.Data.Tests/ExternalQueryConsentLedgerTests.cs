using Xunit;

namespace PulseOffice.Data.Tests;

public sealed class ExternalQueryConsentLedgerTests
{
    private readonly ExternalQueryConsentLedger _ledger = new();
    private readonly ExternalQueryRequest _sales = new("sales", "SELECT * FROM orders");

    [Fact]
    public void A_query_is_pending_until_granted_for_that_document()
    {
        var document = new object();

        Assert.Equal([_sales], _ledger.Pending(document, [_sales, _sales]));
        _ledger.Grant(document, [_sales]);

        Assert.Empty(_ledger.Pending(document, [_sales]));
    }

    [Fact]
    public void Another_document_or_another_text_asks_again()
    {
        var document = new object();
        _ledger.Grant(document, [_sales]);

        Assert.Single(_ledger.Pending(new object(), [_sales]));
        Assert.Single(_ledger.Pending(document, [_sales with { Sql = "SELECT * FROM orders WHERE 1" }]));
        Assert.Single(_ledger.Pending(document, [_sales with { ConnectionId = "archive" }]));
    }
}
