using System.Text.Json.Nodes;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Exchange.Tests;

public sealed class DeepAuditRoundFortySevenExchangeTests
{
    private readonly JsonExchangeSerializer _serializer = new(new LoadedExchangeTypeCatalog(), new ExchangeContentGuard());

    private JsonNode Group()
    {
        var shape = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle };
        var package = new ExchangePackage
        {
            Id = "p",
            Content = new ObjectsContent([new ExchangedObject(new ShapeGroupObject { Id = "g", Children = [new GroupChild(shape, 0, 0, 1, 1)] }, new DocumentSize(new Twip(500), new Twip(500)))]),
        };
        return JsonNode.Parse(_serializer.Write(package))!;
    }

    [Fact]
    public void UnGruppoConFigliNulliNelJsonNativoNonDiventaUnPacchetto()
    {
        var json = Group();
        json["Content"]!["Objects"]![0]!["Object"]!["Children"] = null;
        Assert.Null(_serializer.Read(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void UnFiglioDiGruppoSenzaContenutoNelJsonNativoNonDiventaUnPacchetto()
    {
        var json = Group();
        json["Content"]!["Objects"]![0]!["Object"]!["Children"]![0]!["Content"] = null;
        Assert.Null(_serializer.Read(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Fact]
    public void UnPacchettoNativoIntegroAttraversaAncoraLaLettura()
    {
        Assert.NotNull(_serializer.Read(System.Text.Encoding.UTF8.GetBytes(Group().ToJsonString())));
    }
}
