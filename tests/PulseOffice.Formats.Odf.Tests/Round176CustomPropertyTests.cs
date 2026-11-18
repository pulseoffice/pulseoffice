using PulseOffice.Document.Metadata;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round176CustomPropertyTests
{
    [Fact]
    public void A_custom_value_that_does_not_fit_its_kind_is_written_as_text_not_as_an_invalid_typed_value()
    {
        var properties = new DocumentProperties();
        properties.Custom["Price"] = "3,5";
        properties.CustomKinds["Price"] = CustomPropertyKind.Number;
        properties.Custom["Done"] = "True";
        properties.CustomKinds["Done"] = CustomPropertyKind.Boolean;
        var mapper = new OdfMetaMapper();

        var read = new DocumentProperties();
        mapper.Read(mapper.Build(properties), read);

        Assert.Equal(CustomPropertyKind.Text, read.KindOf("Price"));
        Assert.Equal("3,5", read.Custom["Price"]);
        Assert.Equal(CustomPropertyKind.Boolean, read.KindOf("Done"));
        Assert.Equal("true", read.Custom["Done"]);
    }
}

public sealed class Round176MetaReadTests
{
    [Fact]
    public void Reading_meta_without_dates_or_cycles_does_not_keep_the_previous_ones()
    {
        var mapper = new OdfMetaMapper();
        var target = new DocumentProperties { Created = DateTimeOffset.UnixEpoch, Modified = DateTimeOffset.UnixEpoch, EditingCycles = 7 };
        var meta = mapper.Build(new DocumentProperties());
        meta.Descendants().Where(element => element.Name.LocalName is "creation-date").ToList().ForEach(element => element.Remove());

        mapper.Read(meta, target);

        Assert.Null(target.Created);
        Assert.Null(target.Modified);
        Assert.Equal(0, target.EditingCycles);
    }
}
