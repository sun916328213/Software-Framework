using System;
using Vision.Core.Geometry;
using Vision.Core.Models;
using Xunit;

namespace Vision.Tests.Core;

[Trait("Category", "Contract")]
public sealed class TemplateSearchRangeTests
{
    [Theory]
    [InlineData(0.1, 0.1, 0.2, 0, true)]
    [InlineData(0.3, 0.1, 0.4, 0, true)]
    [InlineData(0.1, 0.3, 0.2, 0.2, true)]
    [InlineData(179.9, 0.2, -179.9, 0, true)]
    [InlineData(170, 30, -175, 15, true)]
    [InlineData(170, 30, -175, 15.00000001, false)]
    [InlineData(0.1, 0.1, 0.20000001, 0, false)]
    [InlineData(0.1, 0.1, 0.09999999, 0, false)]
    [InlineData(0.2, 0.1, 360.2, 0.1, true)]
    [InlineData(179.9, 0.2, 539.9, 0.2, true)]
    [InlineData(0, 0, 360, 0, true)]
    [InlineData(0, 0, 0.00000001, 0, false)]
    [InlineData(170, 30, -175, 360, false)]
    [InlineData(0, 360, 175, 360, true)]
    public void Contains_AcceptsRoundedAngularBoundaries_ButRejectsOutsideRanges(
        double preparedStart,
        double preparedExtent,
        double searchStart,
        double searchExtent,
        bool expected)
    {
        var prepared = new TemplateSearchRange(preparedStart, preparedExtent);
        var search = new TemplateSearchRange(searchStart, searchExtent);

        Assert.Equal(expected, prepared.Contains(search));
    }

    [Fact]
    public void Contains_KeepsScaleBoundsStrict_EvenForFullCircle()
    {
        var prepared = new TemplateSearchRange(0, 360, 0.5, 2);

        Assert.True(prepared.Contains(new TemplateSearchRange(170, 30, 0.5, 2)));
        Assert.False(prepared.Contains(new TemplateSearchRange(170, 30, 0.499999999, 2)));
        Assert.False(prepared.Contains(new TemplateSearchRange(170, 30, 0.5, 2.000000001)));
    }

    [Fact]
    public void Pose_PositiveQuarterTurn_IsClockwiseInImageCoordinates()
    {
        var pose = new Pose2D(10, 20, 90);
        var local = new Point2D(1, 0);

        Assert.Equal(new Point2D(10, 21), pose.Transform(local));
        Assert.Equal(local, pose.InverseTransform(new Point2D(10, 21)));
    }
}
