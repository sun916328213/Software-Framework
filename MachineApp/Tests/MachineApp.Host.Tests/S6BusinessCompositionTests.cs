// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：S6BusinessCompositionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Reflection、MachineApp.Business.Flow、MachineApp.Business.Station、MachineApp.Infra.Device、Prism.Ioc、SecsGem.Core。导入不代表所有类型都会在运行时加载。
using System.Reflection;
using MachineApp.Business.Flow;
using MachineApp.Business.Station;
using MachineApp.Infra.Device;
using Prism.Ioc;
using SecsGem.Core;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// S6BusinessCompositionTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ProductionComposition")]
public sealed class S6BusinessCompositionTests(ProductionFixture host)
{
    /// <summary>
    /// 验证 ProductionContainerComposesAllFlowsAndStationsWithoutConnectingDevices 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、NotNull、Resolve、DoesNotContain、SelectMany；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProductionContainerComposesAllFlowsAndStationsWithoutConnectingDevices()
    {
        host.Invoke(container =>
        {
            Type[] types = [typeof(Load), typeof(ReadID), typeof(Accessory), typeof(Label),
                typeof(HeatSeal), typeof(ALDetect), typeof(UnLoad), typeof(HumidityCard),
                typeof(Recipe), typeof(MachineApp.Business.Flow.Process), typeof(AutoProcess), typeof(Init), typeof(Close)];
            foreach (var type in types)
            {
                Assert.NotNull(container.Resolve(type));
                Assert.DoesNotContain(type.GetConstructors().SelectMany(c => c.GetParameters()),
                    p => p.ParameterType == typeof(IContainerProvider));
            }
            var services = container.Resolve<StationServices>();
            Assert.Same(container.Resolve<ProductionDeviceSession>(), services.Devices());
            Assert.Same(services.Plc(), container.Resolve<Load>()._PLCMethod);
            Assert.Same(services.Vision(), container.Resolve<ReadID>()._VisionMethod);
            Assert.Empty(services.Devices().Scanners);
            Assert.Empty(container.Resolve<ProductionDeviceSession>().Cameras);
            Assert.False(container.Resolve<ISecsGemRuntime>().GetState().IsConnected);
            Assert.Empty(typeof(Init).GetFields(BindingFlags.NonPublic | BindingFlags.Static));
        });
    }

    /// <summary>
    /// 验证 StationConstructionKeepsOrderAndLogLookupDeferred 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Add、Get、Equal、Last、Clear；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StationConstructionKeepsOrderAndLogLookupDeferred()
    {
        var order = new List<string>();
        T Get<T>(string name) { order.Add(name); return default!; }
        var services = new StationServices(() => Get<MachineApp.Business.Ports.IProductionLog>("log"),
            () => Get<MachineApp.Business.Ports.IProductionSecs>("secs"),
            () => Get<MachineApp.Business.Ports.IProductionVision>("vision"),
            () => Get<MachineApp.Business.Ports.IProductionDevices>("devices"),
            () => Get<MachineApp.Business.Ports.IProductionPrinter>("printer"),
            () => Get<MachineApp.Business.Ports.IProductionPlc>("plc"));
        var station = new StationBase<int>(services);
        Assert.Equal(new[] { "secs", "vision", "devices", "printer", "plc" }, order);
        _ = station._LogInfoService;
        Assert.Equal("log", order.Last());
        var failing = new StationServices(services.LogInfo,
            () => throw new InvalidOperationException("test"), services.Vision,
            services.Devices, services.Printer, services.Plc);
        order.Clear();
        _ = new StationBase<int>(failing);
        Assert.Empty(order);
    }
}
