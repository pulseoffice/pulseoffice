using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class AnimationIdTests
{
    private readonly Slide _slide = new SlideFactory()
        .Create("slide1", new MasterFactory().CreateStandard(new ThemeCatalog().Default), SlideLayout.TitleSlide);

    [Fact]
    public void UnIdentificativoNuovoNonRipeteQuelloRimastoDopoUnaRimozione()
    {
        _slide.AddAnimation(new AnimationEffect { Id = _slide.NextAnimationId(), TargetId = "a" });
        var second = _slide.NextAnimationId();
        _slide.AddAnimation(new AnimationEffect { Id = second, TargetId = "b" });
        _slide.RemoveAnimation("slide1.anim1");

        var next = _slide.NextAnimationId();

        Assert.NotEqual(second, next);
        Assert.DoesNotContain(_slide.Animations, effect => effect.Id == next);
    }
}

public sealed class CustomShowNamingTests
{
    [Fact]
    public void NomiVuotiODoppiDiventanoDistinti()
    {
        var shows = new CustomShowNaming().Distinct(
            [new CustomShow("Breve", ["a"]), new CustomShow("breve", ["b"]), new CustomShow(" ", ["c"])], "Senza nome");

        Assert.Equal(["Breve", "breve 2", "Senza nome"], shows.Select(show => show.Name));
        Assert.Equal(["b"], shows[1].SlideIds);
    }
}
