using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vision.Core;
using Vision.Tools.OpenCv.Processing;
using Xunit;

namespace Vision.Tests.Tools.OpenCv;

[Trait("Category", "Contract")]
public sealed class ToolParameterInteractionTests
{
    [Theory]
    [InlineData(typeof(MorphologyParameters), nameof(MorphologyParameters.KernelSize), 1, 3)]
    [InlineData(typeof(ImageFilterParameters), nameof(ImageFilterParameters.KernelSize), 3, 3)]
    [InlineData(typeof(AdaptiveThresholdParameters), nameof(AdaptiveThresholdParameters.BlockSize), 3, 11)]
    public void OddSizeMetadata_UsesStepTwo_WithoutChangingMinimumOrDefault(Type type, string name, int minimum, int defaultValue)
    {
        var property = type.GetProperty(name)!;
        var metadata = property.GetCustomAttribute<ToolPropertyAttribute>()!;
        Assert.Equal(2d, metadata.Step);
        Assert.Equal((double)minimum, metadata.Min);
        Assert.Equal(defaultValue, property.GetValue(Activator.CreateInstance(type)));
    }

    [Theory]
    [InlineData(ImageFilterMode.Gaussian, true)]
    [InlineData(ImageFilterMode.Median, false)]
    [InlineData(ImageFilterMode.Mean, false)]
    public void FilterMode_ControlsOnlySigma_AndNotifiesOriginalPanel(ImageFilterMode mode, bool sigmaVisible)
    {
        var parameters = new ImageFilterParameters { Sigma = 2.75, KernelSize = 7, Border = ImageFilterBorder.Replicate };
        var visibility = VisibilityProperty(parameters, nameof(ImageFilterParameters.Sigma));
        var changes = Observe(parameters);
        parameters.Mode = ImageFilterMode.Mean;
        changes.Clear();
        parameters.Mode = mode;

        Assert.Equal(sigmaVisible, visibility.GetValue(parameters));
        AssertModeNotifications(changes, mode != ImageFilterMode.Mean, visibility.Name);
        Assert.Equal(2.75, parameters.Sigma);
        Assert.Equal(7, parameters.KernelSize);
        Assert.Equal(ImageFilterBorder.Replicate, parameters.Border);
        AssertUnconditional<ImageFilterParameters>(nameof(ImageFilterParameters.Mode), nameof(ImageFilterParameters.KernelSize), nameof(ImageFilterParameters.Border));
        changes.Clear();
        parameters.Mode = mode;
        Assert.Empty(changes);
    }

    [Theory]
    [InlineData(IntensityTransformMode.Linear, true, false)]
    [InlineData(IntensityTransformMode.Gamma, false, true)]
    [InlineData(IntensityTransformMode.Normalize, false, false)]
    public void IntensityMode_ControlsLinearAndGammaFields_WithoutResettingValues(IntensityTransformMode mode, bool linearVisible, bool gammaVisible)
    {
        var parameters = new IntensityTransformParameters { Gain = -2.5, Offset = 17, Gamma = 2.2 };
        var gain = VisibilityProperty(parameters, nameof(IntensityTransformParameters.Gain));
        var offset = VisibilityProperty(parameters, nameof(IntensityTransformParameters.Offset));
        var gamma = VisibilityProperty(parameters, nameof(IntensityTransformParameters.Gamma));
        var changes = Observe(parameters);
        parameters.Mode = IntensityTransformMode.Normalize;
        changes.Clear();
        parameters.Mode = mode;

        Assert.Equal(linearVisible, gain.GetValue(parameters));
        Assert.Equal(linearVisible, offset.GetValue(parameters));
        Assert.Equal(gammaVisible, gamma.GetValue(parameters));
        AssertModeNotifications(changes, mode != IntensityTransformMode.Normalize, gain.Name, offset.Name, gamma.Name);
        Assert.Equal(-2.5, parameters.Gain);
        Assert.Equal(17d, parameters.Offset);
        Assert.Equal(2.2, parameters.Gamma);
        AssertUnconditional<IntensityTransformParameters>(nameof(IntensityTransformParameters.Mode));
        changes.Clear();
        parameters.Mode = mode;
        Assert.Empty(changes);
    }

    [Fact]
    public void FilterJson_KeepsOriginalFieldsAndHiddenSigma_AndLegacyDefaults()
    {
        var parameters = new ImageFilterParameters { Mode = ImageFilterMode.Median, KernelSize = 9, Sigma = 4.25, Border = ImageFilterBorder.Replicate };
        var copy = RoundTrip(parameters, "Mode", "KernelSize", "Sigma", "Border");
        Assert.Equal(parameters.Mode, copy.Mode);
        Assert.Equal(parameters.KernelSize, copy.KernelSize);
        Assert.Equal(parameters.Sigma, copy.Sigma);
        Assert.Equal(parameters.Border, copy.Border);
        Assert.False((bool)VisibilityProperty(copy, nameof(ImageFilterParameters.Sigma)).GetValue(copy)!);

        var legacy = JsonSerializer.Deserialize<ImageFilterParameters>("{}")!;
        Assert.Equal(ImageFilterMode.Gaussian, legacy.Mode);
        Assert.Equal(3, legacy.KernelSize);
        Assert.Equal(1d, legacy.Sigma);
        Assert.Equal(ImageFilterBorder.Replicate, legacy.Border);
        Assert.True((bool)VisibilityProperty(legacy, nameof(ImageFilterParameters.Sigma)).GetValue(legacy)!);
    }

    [Fact]
    public void IntensityJson_KeepsOriginalFieldsAndHiddenValues_AndLegacyDefaults()
    {
        var parameters = new IntensityTransformParameters { Mode = IntensityTransformMode.Normalize, Gain = -3, Offset = 42, Gamma = 1.75 };
        var copy = RoundTrip(parameters, "Mode", "Gain", "Offset", "Gamma");
        Assert.Equal(parameters.Mode, copy.Mode);
        Assert.Equal(parameters.Gain, copy.Gain);
        Assert.Equal(parameters.Offset, copy.Offset);
        Assert.Equal(parameters.Gamma, copy.Gamma);
        foreach (var name in new[] { nameof(copy.Gain), nameof(copy.Offset), nameof(copy.Gamma) })
            Assert.False((bool)VisibilityProperty(copy, name).GetValue(copy)!);

        var legacy = JsonSerializer.Deserialize<IntensityTransformParameters>("{}")!;
        Assert.Equal(IntensityTransformMode.Linear, legacy.Mode);
        Assert.Equal(1d, legacy.Gain);
        Assert.Equal(0d, legacy.Offset);
        Assert.Equal(1d, legacy.Gamma);
        Assert.True((bool)VisibilityProperty(legacy, nameof(legacy.Gain)).GetValue(legacy)!);
        Assert.False((bool)VisibilityProperty(legacy, nameof(legacy.Gamma)).GetValue(legacy)!);
    }

    [Theory]
    [InlineData(ImageFilterMode.Median)]
    [InlineData(ImageFilterMode.Mean)]
    public void HiddenInvalidSigma_IsStillRejected(ImageFilterMode mode)
    {
        using var tool = new ImageFilterTool();
        tool.Parameters.Mode = mode;
        tool.Parameters.Sigma = 0;
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == "FILTER_SIGMA");
        Assert.Equal(0d, tool.Parameters.Sigma);
    }

    [Theory]
    [InlineData(IntensityTransformMode.Gamma, "Gain", 101d, "INTENSITY_GAIN")]
    [InlineData(IntensityTransformMode.Normalize, "Offset", 65536d, "INTENSITY_OFFSET")]
    [InlineData(IntensityTransformMode.Linear, "Gamma", 0d, "INTENSITY_GAMMA")]
    public void HiddenInvalidIntensityField_IsStillRejected(IntensityTransformMode mode, string name, double value, string code)
    {
        using var tool = new IntensityTransformTool();
        tool.Parameters.Mode = mode;
        typeof(IntensityTransformParameters).GetProperty(name)!.SetValue(tool.Parameters, value);
        Assert.Contains(tool.Validate().Issues, issue => issue.Code == code);
        Assert.Equal(value, typeof(IntensityTransformParameters).GetProperty(name)!.GetValue(tool.Parameters));
    }

    private static PropertyInfo VisibilityProperty(object parameters, string name)
    {
        var metadata = parameters.GetType().GetProperty(name)!.GetCustomAttribute<ToolPropertyAttribute>()!;
        Assert.True(metadata.IsDisplay);
        Assert.False(string.IsNullOrWhiteSpace(metadata.IsVisibleProperty));
        var property = parameters.GetType().GetProperty(metadata.IsVisibleProperty);
        Assert.NotNull(property);
        Assert.Equal(typeof(bool), property.PropertyType);
        Assert.False(property.CanWrite);
        Assert.True(property.IsDefined(typeof(JsonIgnoreAttribute)));
        Assert.False(property.GetCustomAttribute<ToolPropertyAttribute>()!.IsDisplay);
        return property;
    }

    private static List<string?> Observe(object parameters)
    {
        var changes = new List<string?>();
        Assert.IsAssignableFrom<INotifyPropertyChanged>(parameters).PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        return changes;
    }

    private static void AssertModeNotifications(List<string?> changes, bool changed, params string[] visibilityNames)
    {
        if (!changed)
        {
            Assert.Empty(changes);
            return;
        }
        Assert.Equal(1, changes.Count(name => name == "Mode"));
        foreach (var name in visibilityNames.Distinct())
            Assert.Equal(1, changes.Count(change => change == name));
    }

    private static void AssertUnconditional<T>(params string[] names)
    {
        foreach (var name in names)
        {
            var metadata = typeof(T).GetProperty(name)!.GetCustomAttribute<ToolPropertyAttribute>()!;
            Assert.True(metadata.IsDisplay);
            Assert.True(string.IsNullOrEmpty(metadata.IsVisibleProperty));
        }
    }

    private static T RoundTrip<T>(T parameters, params string[] fields)
    {
        var json = JsonSerializer.Serialize(parameters);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(fields.OrderBy(name => name, StringComparer.Ordinal),
            document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
