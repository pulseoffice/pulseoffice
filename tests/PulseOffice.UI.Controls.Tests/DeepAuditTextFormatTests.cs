using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditTextFormatTests
{
    [Fact]
    public void L_anteprima_di_un_elenco_senza_livelli_e_vuota_invece_di_lanciare()
    {
        var rows = new ListSampleRows(new ListLabeler());

        Assert.Empty(rows.Rows([], rows.Pattern(ListSampleMode.Items, 0)));
    }
}
