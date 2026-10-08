// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NavigationRegistrationTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Infra.Persistence.Entities、DataManager.Controls、Device.Controls、MachineApp.UI.Model、Prism.DryIoc、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using MachineApp.Infra.Persistence.Entities;
using DataManager.Controls;
using Device.Controls;
using MachineApp.UI.Model;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// NavigationRegistrationTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class NavigationRegistrationTests
{
    /// <summary>
    /// 测试相关入口 ModulesPreserveHostNavigationReplacement；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、RegisterInstance、RegisterTypes、Same、Resolve；具体条件和先后次序见方法体。
    /// </summary>
    [Theory]
    [InlineData("DataManager", true, "LogView")]
    [InlineData("DataManager", false, "LogView")]
    [InlineData("Device", true, "CameraView")]
    [InlineData("Device", false, "CameraView")]
    [InlineData("ScriptEditor", false, "ScriptManagerView")]
    [InlineData("WPFControls", false, "PrintView")]
    [InlineData("PublicControls", true, "PaginationView")]
    [InlineData("FlowEditor", true, "FlowToolboxView")]
    [InlineData("WPFCustomControls", true, "ConfigManagerView")]
    public void ModulesPreserveHostNavigationReplacement(string feature, bool reuse, string navigationName)
    {
        IModule module = feature switch
        {
            "DataManager" => new DataManagerModule<SqlConfigEntity, ExcelConfigEntity>(new DataManagerModuleOptions { ReuseNavigationViews = reuse }),
            "Device" => new DeviceControlsModule(new DeviceModuleOptions { ReuseNavigationViews = reuse }),
            "ScriptEditor" => new ScriptEditor.Controls.ScriptEditorModule(),
            "WPFControls" => new WPFControls.WPFControlsModule(),
            "PublicControls" => new WPFControls.WPFControlsModule(),
            "FlowEditor" => new FlowEditor.Controls.FlowEditorModule(),
            "WPFCustomControls" => new WPFCustomControls.WPFCustomControlsModule(),
            _ => throw new ArgumentOutOfRangeException(nameof(feature))
        };
        var container = new DryIocContainerExtension();
        var replacement = new object();
        container.RegisterInstance(typeof(object), replacement, navigationName);
        module.RegisterTypes(container);
        Assert.Same(replacement, container.Resolve<object>(navigationName));
        module.RegisterTypes(container);
        Assert.Same(replacement, container.Resolve<object>(navigationName));
    }
}
