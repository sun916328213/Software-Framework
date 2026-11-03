using Vision.Controls.ViewModels;
using Cv = Vision.Tools.OpenCv.Inspection;
using Halcon = Vision.Tools.Halcon.Inspection;

namespace Vision.Workbench.Tests.Controls;

[Trait("Category", "Unit")]
public sealed class BlobPhiDefaultTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewlySelectedPhi_HasNegativeMinimum_AndPreservesExistingRanges(bool halcon)
    {
        var cv = new Cv.BlobParameters { Features = [new() { Feature = "area", Min = 12, Max = 345 }] };
        var h = new Halcon.BlobParameters { Features = [new() { Feature = "area", Min = 12, Max = 345 }] };
        using var editor = BlobParametersEditorViewModel.TryCreate(halcon ? h : cv, () => { });
        Assert.NotNull(editor);

        editor.SelectedFeatures.Add("phi");

        var phi = editor.Ranges.Single(r => r.Feature == "phi");
        Assert.True(phi.Min < 0);
        Assert.True(phi.Max > 0);
        if (halcon)
        {
            Assert.True(h.Features.Single(f => f.Feature == "phi").Min < 0);
            var area = h.Features.Single(f => f.Feature == "area");
            Assert.Equal(12, area.Min);
            Assert.Equal(345, area.Max);
        }
        else
        {
            Assert.True(cv.Features.Single(f => f.Feature == "phi").Min < 0);
            var area = cv.Features.Single(f => f.Feature == "area");
            Assert.Equal(12, area.Min);
            Assert.Equal(345, area.Max);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingPhi_IsPreserved_AndOtherNewFeaturesKeepTheirDefaults(bool halcon)
    {
        var cv = new Cv.BlobParameters { Features = [new() { Feature = "phi", Min = 0.2, Max = 0.4 }] };
        var h = new Halcon.BlobParameters { Features = [new() { Feature = "phi", Min = 0.2, Max = 0.4 }] };
        using var editor = BlobParametersEditorViewModel.TryCreate(halcon ? h : cv, () => { });
        Assert.NotNull(editor);

        editor.SelectedFeatures.Add("width");

        var width = editor.Ranges.Single(r => r.Feature == "width");
        Assert.Equal(0, width.Min);
        Assert.Equal(99999, width.Max);
        if (halcon)
        {
            var phi = h.Features.Single(f => f.Feature == "phi");
            Assert.Equal(0.2, phi.Min);
            Assert.Equal(0.4, phi.Max);
            Assert.Equal(0, h.Features.Single(f => f.Feature == "width").Min);
            Assert.Equal(99999, h.Features.Single(f => f.Feature == "width").Max);
        }
        else
        {
            var phi = cv.Features.Single(f => f.Feature == "phi");
            Assert.Equal(0.2, phi.Min);
            Assert.Equal(0.4, phi.Max);
            Assert.Equal(0, cv.Features.Single(f => f.Feature == "width").Min);
            Assert.Equal(99999, cv.Features.Single(f => f.Feature == "width").Max);
        }
    }
}
