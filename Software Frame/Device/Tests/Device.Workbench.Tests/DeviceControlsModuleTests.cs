// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DeviceControlsModuleTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Workbench、Device.Controls、Device.Controls.Configuration、Device.Controls.Services、Device.Controls.Views、Prism.DryIoc。导入不代表所有类型都会在运行时加载。
using Device.Workbench;
using Device.Controls;
using Device.Controls.Configuration;
using Device.Controls.Services;
using Device.Controls.Views;
using Prism.DryIoc;
using Prism.Ioc;
using System.IO;
using System.Xml.Linq;

namespace Device.Workbench.Tests;

/// <summary>
/// DeviceControlsModuleTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class DeviceControlsModuleTests
{
    private readonly WorkbenchAppFixture _fixture;
    /// <summary>
    /// 初始化 DeviceControlsModuleTests 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    public DeviceControlsModuleTests(WorkbenchAppFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 验证 DefaultsPreserveHostFactoryReplacementAcrossRepeatedRegistration 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultsPreserveHostFactoryReplacementAcrossRepeatedRegistration()
    {
        var container = new DryIocContainerExtension();
        var factory = new global::Device.Runtime.Cameras.CameraFactory();
        container.RegisterInstance<global::Device.Core.Cameras.ICameraFactory>(factory);
        new DeviceControlsModule().RegisterTypes(container);
        var labels = container.Resolve<global::Device.Core.LabelPrinting.ILabelJobService>();
        new DeviceControlsModule().RegisterTypes(container);
        Assert.Same(factory, container.Resolve<global::Device.Core.Cameras.ICameraFactory>());
        Assert.Same(labels, container.Resolve<global::Device.Core.LabelPrinting.ILabelJobService>());
    }

    /// <summary>
    /// 验证 NativeNavigationRegistrationPreservesWorkspace 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、RegisterTypes、Resolve、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NativeNavigationRegistrationPreservesWorkspace()
    {
        _fixture.Invoke(() =>
        {
            var container = new DryIocContainerExtension();
            new DeviceControlsModule(new DeviceModuleOptions { ReuseNavigationViews = false }).RegisterTypes(container);
            var workspace = container.Resolve<CameraWorkspace>();
            new DeviceControlsModule(new DeviceModuleOptions { ReuseNavigationViews = false }).RegisterTypes(container);
            Assert.Same(workspace, container.Resolve<CameraWorkspace>());
        });
    }

    /// <summary>
    /// 验证 DefaultFactoriesBelongToEachContainer 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 RegisterTypes、NotSame、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultFactoriesBelongToEachContainer()
    {
        var first = new DryIocContainerExtension();
        var second = new DryIocContainerExtension();
        new DeviceControlsModule().RegisterTypes(first);
        new DeviceControlsModule().RegisterTypes(second);
        Assert.NotSame(first.Resolve<global::Device.Core.Cameras.ICameraFactory>(), second.Resolve<global::Device.Core.Cameras.ICameraFactory>());
    }

    /// <summary>
    /// 验证 NavigationLifetimeMatchesHostPolicy_WhileWorkspaceRemainsSingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、RegisterFactories、RegisterTypes、Resolve、nameof；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NavigationLifetimeMatchesHostPolicy_WhileWorkspaceRemainsSingleton(bool reuseViews)
    {
        _fixture.Invoke(() =>
        {
            var container = new DryIocContainerExtension();
            DeviceHostTestRegistration.RegisterFactories(container);
            new DeviceControlsModule(new DeviceModuleOptions { ReuseNavigationViews = reuseViews }).RegisterTypes(container);
            var first = container.Resolve<object>(nameof(CameraView));
            var second = container.Resolve<object>(nameof(CameraView));
            Assert.IsType<CameraView>(first);
            if (reuseViews) Assert.Same(first, second);
            else Assert.NotSame(first, second);
            Assert.Same(container.Resolve<CameraWorkspace>(), container.Resolve<CameraWorkspace>());
        });
    }

    /// <summary>
    /// 验证 DeviceConfigurationAndCompositionDoNotDependOnWpfControl 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Equal、GetName、DoesNotContain、GetReferencedAssemblies；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DeviceConfigurationAndCompositionDoNotDependOnWpfControl()
    {
        Assert.Equal("Device.Controls", typeof(LegacyDeviceConfigImporter).Assembly.GetName().Name);
        Assert.DoesNotContain(typeof(DeviceControlsModule).Assembly.GetReferencedAssemblies(), name => name.Name == "WPFControls");
        Assert.DoesNotContain(typeof(LegacyDeviceConfigImporter).Assembly.GetReferencedAssemblies(),
            name => name.Name is "WPFControls" or "Device.Composition" );
    }

    /// <summary>
    /// 验证 WpfHostDelegatesDeviceRegistrationAndHasPortablePackageConfiguration 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Exists、Combine、NotNull、Load、ToArray；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void WpfHostDelegatesDeviceRegistrationAndHasPortablePackageConfiguration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Device.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var projectDirectory = Path.Combine(directory!.Parent!.FullName, "WPFControls", "WPFControls");
        var xml = XDocument.Load(Path.Combine(projectDirectory, "WPFControls.csproj"));
        var references = xml.Descendants("ProjectReference").Select(element => (string?)element.Attribute("Include")).ToArray();
        Assert.Contains(references, path => path!.EndsWith("Device.Controls.csproj"));
        Assert.Contains(references, path => path!.EndsWith("Device.Runtime.csproj"));
        var removals = xml.Descendants().Select(element => (string?)element.Attribute("Remove")).Where(value => value is not null).ToArray();
        Assert.DoesNotContain(removals, value => value!.Contains(":\\"));
        Assert.Contains(removals, value => value!.Contains("$(NuGetPackageRoot)"));
        var module = File.ReadAllText(Path.Combine(projectDirectory, "WPFControlsModule.cs"));
        Assert.Contains("ModuleDependency(nameof(DeviceControlsModule))", module);
        Assert.DoesNotContain("Device.Runtime", module);
        var composition = File.ReadAllText(Path.Combine(projectDirectory, "WPFControlsComposition.cs"));
        Assert.Contains("catalog.AddModule<Device.Controls.DeviceControlsModule>()", composition);
        Assert.DoesNotContain("Connect(", composition);
    }
}
