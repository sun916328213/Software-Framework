// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NewDeviceHostPageTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.Views、Device.Workbench、Device.Controls、Device.Controls.Views、Extension.Integration.Prism、Prism.DryIoc。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.Views;
using Device.Workbench;
using Device.Controls;
using Device.Controls.Views;
using Extension.Integration.Prism;
using Prism.DryIoc;
using Prism.Ioc;
using System.Windows;
using System.Windows.Controls;


namespace Device.Workbench.Tests;

/// <summary>
/// NewDeviceHostPageTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class NewDeviceHostPageTests(WorkbenchAppFixture fixture)
{
    /// <summary>
    /// 测试相关入口 ExistingNavigationNamesRenderTheNewDevicePages；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、RegisterFactories、RegisterTypes、RegisterNavigationView、IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("CamManagerView", typeof(CameraView))]
    [InlineData("LightControlManagerView", typeof(LightView))]
    [InlineData("RFIDManagerView", typeof(RfidView))]
    [InlineData("ScanManagerView", typeof(ScannerView))]
    public void ExistingNavigationNamesRenderTheNewDevicePages(string name, Type expectedPage)
    {
        fixture.Invoke(() =>
        {
            var registry = new DryIocContainerExtension();
            DeviceHostTestRegistration.RegisterFactories(registry);
            new DeviceControlsModule().RegisterTypes(registry);
            registry.RegisterNavigationView<CamManagerView>();
            registry.RegisterNavigationView<LightControlManagerView>();
            registry.RegisterNavigationView<RFIDManagerView>();
            registry.RegisterNavigationView<ScanManagerView>();
            var view = Assert.IsAssignableFrom<UserControl>(registry.Resolve<object>(name));
            Assert.Same(view, registry.Resolve<object>(name));
            var page = Assert.IsAssignableFrom<UserControl>(view.Content);
            Assert.Equal(expectedPage, page.GetType());
            Assert.NotNull(page.DataContext);
            Assert.Equal("Device.Controls", page.DataContext!.GetType().Assembly.GetName().Name);
            view.Measure(new Size(900, 700));
            view.Arrange(new Rect(new Size(900, 700)));
            view.UpdateLayout();
            Assert.True(page.ActualWidth > 0);
        });
    }
}
