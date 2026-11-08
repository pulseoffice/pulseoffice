using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class AnimationPresetRegistryTests
{
    private readonly IAnimationPresetRegistry _reg = new AnimationPresetRegistry();

    [Theory]
    [InlineData(AnimationCategory.Entrance, "fade")]
    [InlineData(AnimationCategory.Entrance, "fly-in")]
    [InlineData(AnimationCategory.Entrance, "zoom")]
    [InlineData(AnimationCategory.Entrance, "wheel")]
    [InlineData(AnimationCategory.Emphasis, "spin")]
    [InlineData(AnimationCategory.Exit, "fly-out")]
    [InlineData(AnimationCategory.Exit, "disappear")]
    public void KnownPresetsRoundTripThroughOoxmlAndOdf(AnimationCategory cat, string preset)
    {
        Assert.Equal(preset, _reg.FromOoxml(cat, _reg.OoxmlPresetId(cat, preset)));
        Assert.Equal(preset, _reg.FromOdf(cat, _reg.OdfPresetId(cat, preset)));
    }

    [Fact]
    public void UnknownOdfPresetIsPreservedVerbatim()
    {
        var canonical = _reg.FromOdf(AnimationCategory.Entrance, "ooo-entrance-exotic-thing");
        Assert.Equal("ooo-entrance-exotic-thing", canonical);
        Assert.Equal("ooo-entrance-exotic-thing", _reg.OdfPresetId(AnimationCategory.Entrance, canonical));
    }

    [Fact]
    public void UnknownOoxmlPresetIsPreservedVerbatim()
    {
        var canonical = _reg.FromOoxml(AnimationCategory.Entrance, "137");
        Assert.Equal("ooxml-137", canonical);
        Assert.Equal("137", _reg.OoxmlPresetId(AnimationCategory.Entrance, canonical));
    }

    [Theory]
    [InlineData(AnimationCategory.Entrance, "wheel", "21", "ooo-entrance-wheel")]
    [InlineData(AnimationCategory.Entrance, "swivel", "19", "ooo-entrance-swivel")]
    [InlineData(AnimationCategory.Emphasis, "teeter", "32", "ooo-emphasis-teeter")]
    [InlineData(AnimationCategory.Emphasis, "transparency", "9", "ooo-emphasis-transparency")]
    [InlineData(AnimationCategory.MotionPath, "line", "42", "ooo-motionpath-down")]
    public void PowerPointAndOdfIdentifiersMatchTheReferenceTable(AnimationCategory category, string preset, string ooxml, string odf)
    {
        var registry = new AnimationPresetRegistry();

        Assert.Equal(ooxml, registry.OoxmlPresetId(category, preset));
        Assert.Equal(odf, registry.OdfPresetId(category, preset));
        Assert.Equal(preset, registry.FromOoxml(category, ooxml));
        Assert.Equal(preset, registry.FromOdf(category, odf));
    }

    [Theory]
    [InlineData(AnimationCategory.Entrance, "ooo-entrance-breaks", "34")]
    [InlineData(AnimationCategory.Exit, "ooo-exit-spin-out", "49")]
    [InlineData(AnimationCategory.Emphasis, "ooo-emphasis-font-color", "3")]
    public void ImpressOnlyNamesReachPowerPointThroughTheNativeTable(AnimationCategory category, string odf, string ooxml)
    {
        Assert.Equal(ooxml, _reg.OoxmlPresetId(category, odf));
        Assert.True(_reg.ReachesOoxml(category, odf));
        Assert.Equal(odf, _reg.OdfPresetId(category, _reg.FromOoxml(category, ooxml)));
    }

    [Fact]
    public void PresetsWithoutPrefixOooAreKeptInOdfAndDeclaredForOoxml()
    {
        Assert.Equal("libo-physics-fall", _reg.OdfPresetId(AnimationCategory.Emphasis, "libo-physics-fall"));
        Assert.False(_reg.ReachesOoxml(AnimationCategory.Emphasis, "libo-physics-fall"));
    }

    [Fact]
    public void PowerPointNumbersWithoutImpressNameSurviveOdfVerbatim()
    {
        Assert.Equal("ooxml-137", _reg.OdfPresetId(AnimationCategory.Entrance, "ooxml-137"));
        Assert.Equal("ooxml-137", _reg.FromOdf(AnimationCategory.Entrance, "ooxml-137"));
    }
}
