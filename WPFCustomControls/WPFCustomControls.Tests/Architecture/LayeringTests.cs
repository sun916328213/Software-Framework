using WPFCustomControls.PropertyGrid.Services;
using WPFCustomControls.PropertyGrid.ViewModels;

namespace WPFCustomControls.Tests.Architecture;

public sealed class LayeringTests
{
    [Fact]
    public void PropertyGridCore_DoesNotReferenceWpfOrPresentationAssembly()
    {
        var references = typeof(PropertyGridBuilderService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("WPFCustomControls", references);
        Assert.DoesNotContain("PresentationCore", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("WindowsBase", references);
    }

    [Fact]
    public void PropertyGridPresentationModels_AreOwnedByCoreAssembly()
    {
        Assert.Equal(
            "WPFCustomControls.PropertyGrid.Core",
            typeof(PropertyGridHostViewModel).Assembly.GetName().Name);
    }

    [Fact]
    public void WpfProject_DependsOnPropertyGridCore()
    {
        var references = typeof(WPFCustomControlsModule).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.Contains("WPFCustomControls.PropertyGrid.Core", references);
    }

    [Fact]
    public void LegacyPropertyGridTypeNames_AreForwardedToCore()
    {
        var legacyName =
            "WPFCustomControls.PropertyGrid.ViewModels.PropertyGridHostViewModel, WPFCustomControls";

        Assert.Equal(typeof(PropertyGridHostViewModel), Type.GetType(legacyName, throwOnError: true));
    }
}
