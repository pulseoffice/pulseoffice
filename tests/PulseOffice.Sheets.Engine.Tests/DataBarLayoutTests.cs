using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class DataBarLayoutTests
{
    private readonly DataBarLayout _layout = new();

    private ConditionalRule Rule(DataBarOptions options) => new()
    {
        Range = new PulseOffice.Sheets.Addressing.CellRange(new PulseOffice.Sheets.Addressing.CellAddress(0, 0), new PulseOffice.Sheets.Addressing.CellAddress(2, 0)),
        Visual = ConditionalVisual.DataBar,
        BarColorHex = "#638EC6",
        NegativeBarColorHex = "#FF0000",
        Bar = options,
    };

    [Fact]
    public void SenzaValoriNegativiLaBarraParteDaSinistraSenzaAsse()
    {
        var shape = _layout.Shape(Rule(DataBarOptions.Default), 50, 0, 100);

        Assert.Equal(0, shape.Start, 6);
        Assert.Equal(0.5, shape.End, 6);
        Assert.Null(shape.Axis);
        Assert.True(shape.Gradient);
    }

    [Fact]
    public void ConValoriNegativiLAsseAutomaticoSiMetteInProporzioneEIlNegativoHaIlSuoColore()
    {
        var rule = Rule(DataBarOptions.Default with { BorderColorHex = "#111111", NegativeBorderColorHex = "#222222" });

        var negative = _layout.Shape(rule, -25, -50, 50);
        var positive = _layout.Shape(rule, 50, -50, 50);

        Assert.Equal(0.5, negative.Axis!.Value, 6);
        Assert.Equal(0.25, negative.Start, 6);
        Assert.Equal(0.5, negative.End, 6);
        Assert.Equal("#FF0000", negative.FillHex);
        Assert.Equal("#222222", negative.BorderHex);
        Assert.Equal(1, positive.End, 6);
        Assert.Equal("#111111", positive.BorderHex);
    }

    [Fact]
    public void LaBarraPienaDaDestraSiSpecchia()
    {
        var shape = _layout.Shape(Rule(DataBarOptions.Default with { Gradient = false, Direction = DataBarDirection.RightToLeft }), 25, 0, 100);

        Assert.Equal(0.75, shape.Start, 6);
        Assert.Equal(1, shape.End, 6);
        Assert.False(shape.Gradient);
    }

    [Fact]
    public void LAsseAlCentroELeLunghezzeMinimaEMassimaSiRispettano()
    {
        var middle = _layout.Shape(Rule(DataBarOptions.Default with { Axis = DataBarAxis.Middle }), 10, 0, 10);
        var bounded = _layout.Shape(Rule(DataBarOptions.Default with { MinimumLength = 10, MaximumLength = 90 }), 0, 0, 10);

        Assert.Equal(0.5, middle.Start, 6);
        Assert.Equal(1, middle.End, 6);
        Assert.Equal(0.1, bounded.End, 6);
    }
}
