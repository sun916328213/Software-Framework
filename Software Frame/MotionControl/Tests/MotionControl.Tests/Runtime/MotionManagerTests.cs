// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：MotionManagerTests、ThrowingConnectCard。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MotionControl.Core.Abstractions、MotionControl.Core.Definitions、MotionControl.Runtime.Services、MotionControl.Runtime.Vendors.Simulation、Xunit。导入不代表所有类型都会在运行时加载。
using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Services;
using MotionControl.Runtime.Vendors.Simulation;
using Xunit;

namespace MotionControl.Tests;

/// <summary>
/// MotionManagerTests 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class MotionManagerTests
{
    /// <summary>
    /// 测试相关入口 Register_CustomDriverId_CreatesDriverWithoutChangingVendorEnum；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Register、Create、IsAssignableFrom、Same、True；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Register_CustomDriverId_CreatesDriverWithoutChangingVendorEnum()
    {
        var manager = new MotionManager();
        manager.Register("company.motion.custom", _ => new SimulationMotionCard());
        var parameter = new MotionCardParam
        {
            CardType = EMotionCardType.ADLINK,
            DriverId = "COMPANY.MOTION.CUSTOM",
            Axises = [new AxisParam()],
        };

        var card = manager.Create(parameter);

        Assert.IsAssignableFrom<ISimulationMotionCard>(card);
        Assert.Same(parameter, card.Param);
        Assert.True(manager.IsRegistered(new MotionDriverId("Company.Motion.Custom")));
    }

    /// <summary>
    /// 测试相关入口 Create_WithoutDriverId_UsesLegacyCardTypeRegistration；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、IsAssignableFrom、True、IsRegistered；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Create_WithoutDriverId_UsesLegacyCardTypeRegistration()
    {
        var manager = new MotionManager();
        var parameter = new MotionCardParam
        {
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        };

        var card = manager.Create(parameter);

        Assert.IsAssignableFrom<ISimulationMotionCard>(card);
        Assert.True(manager.IsRegistered(EMotionCardType.Simulation));
    }

    /// <summary>
    /// 测试相关入口 Register_DefaultDriverId_IsRejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Throws、Register；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Register_DefaultDriverId_IsRejected()
    {
        var manager = new MotionManager();

        Assert.Throws<ArgumentException>(() =>
            manager.Register(default(MotionDriverId), _ => new SimulationMotionCard()));
    }

    /// <summary>
    /// 测试相关入口 Create_FactoryReturnsNull_ReportsDriverId；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Register、Throws、Create、Contains；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Create_FactoryReturnsNull_ReportsDriverId()
    {
        var manager = new MotionManager();
        manager.Register("company.motion.invalid", _ => null!);
        var parameter = new MotionCardParam { DriverId = "company.motion.invalid" };

        var exception = Assert.Throws<InvalidOperationException>(() => manager.Create(parameter));

        Assert.Contains("company.motion.invalid", exception.Message);
    }

    /// <summary>
    /// 测试相关入口 ManagerLifecycle_PreservesLegacyCallShapeWithoutDeviceDependency；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、ConnectAsync、Equal、Same、DeviceNameCollection；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ManagerLifecycle_PreservesLegacyCallShapeWithoutDeviceDependency()
    {
        using var manager = new MotionManager();
        var first = manager.Create(new MotionCardParam
        {
            Name = "AxisCard-1",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });
        var second = manager.Create(new MotionCardParam
        {
            Name = "AxisCard-2",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });

        await manager.ConnectAsync();

        Assert.Equal(2, manager.Count);
        Assert.Same(first, manager[0]);
        Assert.Same(second, manager["AxisCard-2"]);
        Assert.Equal(["AxisCard-1", "AxisCard-2"], manager.DeviceNameCollection());
        Assert.All(manager.DeviceCollection(), card => Assert.True(card.IsConnected));

        manager.Disconnect();

        Assert.All(manager.DeviceCollection(), card => Assert.False(card.IsConnected));
    }

    /// <summary>
    /// 测试相关入口 BatchOperations_ReturnOrderedPerDeviceResultsAndContinueAfterFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、ConnectAllResultAsync、False、Equal、Collection；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task BatchOperations_ReturnOrderedPerDeviceResultsAndContinueAfterFailure()
    {
        using var manager = new MotionManager();
        var valid = manager.Create(new MotionCardParam
        {
            Name = "Valid",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });
        var invalid = manager.Create(new MotionCardParam
        {
            Name = "Invalid",
            CardType = EMotionCardType.Simulation,
            Axises = [],
        });

        var connect = await manager.ConnectAllResultAsync();

        Assert.False(connect.IsSuccess);
        Assert.Equal(1, connect.SuccessCount);
        Assert.Equal(1, connect.FailureCount);
        Assert.Collection(
            connect.Results,
            item =>
            {
                Assert.Equal(0, item.DeviceIndex);
                Assert.Equal("Valid", item.DeviceName);
                Assert.Equal(EMotionResult.Success, item.Result.Code);
            },
            item =>
            {
                Assert.Equal(1, item.DeviceIndex);
                Assert.Equal("Invalid", item.DeviceName);
                Assert.Equal(EMotionResult.InvalidParam, item.Result.Code);
            });
        Assert.True(valid.IsConnected);
        Assert.False(invalid.IsConnected);

        var stop = manager.StopAllResult();
        Assert.Equal(EMotionResult.Success, stop.Results[0].Result.Code);
        Assert.Equal(EMotionResult.NotConnected, stop.Results[1].Result.Code);

        var disconnect = manager.DisconnectAllResult();
        Assert.True(disconnect.IsSuccess);
        Assert.All(manager.DeviceCollection(), card => Assert.False(card.IsConnected));
    }

    /// <summary>
    /// 测试相关入口 BatchOperation_EmptyManager_IsSuccessful；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ConnectAllResult、True、Equal、Empty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BatchOperation_EmptyManager_IsSuccessful()
    {
        using var manager = new MotionManager();

        var result = manager.ConnectAllResult();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Results);
    }

    /// <summary>
    /// 测试相关入口 BatchOperation_DriverException_IsCapturedAndLaterDevicesStillRun；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、Create、ConnectAllResult、False、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void BatchOperation_DriverException_IsCapturedAndLaterDevicesStillRun()
    {
        using var manager = new MotionManager();
        manager.Add(new ThrowingConnectCard
        {
            Param = new MotionCardParam
            {
                Name = "Throwing",
                Axises = [new AxisParam()],
            },
        });
        var later = manager.Create(new MotionCardParam
        {
            Name = "Later",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });

        var result = manager.ConnectAllResult();

        Assert.False(result.IsSuccess);
        Assert.Equal(EMotionResult.Fail, result.Results[0].Result.Code);
        Assert.Contains("driver exploded", result.Results[0].Result.Message);
        Assert.Equal(EMotionResult.Success, result.Results[1].Result.Code);
        Assert.True(later.IsConnected);
    }

    /// <summary>
    /// 测试相关入口 Config_WhenNewConfigurationIsInvalid_PreservesCurrentDevices；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、Throws、Config、Equal、Same；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Config_WhenNewConfigurationIsInvalid_PreservesCurrentDevices()
    {
        using var manager = new MotionManager();
        var current = manager.Create(new MotionCardParam
        {
            Name = "Current",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });

        Assert.Throws<NotSupportedException>(() => manager.Config(
        [
            new MotionCardParam
            {
                Name = "Replacement",
                CardType = EMotionCardType.Simulation,
                Axises = [new AxisParam()],
            },
            new MotionCardParam { DriverId = "missing.driver" },
        ]));

        Assert.Equal(1, manager.Count);
        Assert.Same(current, manager[0]);
    }

    /// <summary>
    /// 测试相关入口 Config_DisconnectsDevicesBeforeReplacingOwnedCollection；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、True、Connect、Config、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Config_DisconnectsDevicesBeforeReplacingOwnedCollection()
    {
        using var manager = new MotionManager();
        var current = manager.Create(new MotionCardParam
        {
            Name = "Current",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });
        Assert.True(current.Connect());

        manager.Config([]);

        Assert.False(current.IsConnected);
        Assert.Equal(0, manager.Count);
    }

    /// <summary>
    /// 测试相关入口 RemoveAndClear_DisconnectOwnedDevices；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Create、True、Connect、Remove、Clear；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RemoveAndClear_DisconnectOwnedDevices()
    {
        using var manager = new MotionManager();
        var removed = manager.Create(new MotionCardParam
        {
            Name = "Removed",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });
        var cleared = manager.Create(new MotionCardParam
        {
            Name = "Cleared",
            CardType = EMotionCardType.Simulation,
            Axises = [new AxisParam()],
        });
        Assert.True(removed.Connect());
        Assert.True(cleared.Connect());

        manager.Remove(removed);
        manager.Clear();

        Assert.False(removed.IsConnected);
        Assert.False(cleared.IsConnected);
        Assert.Equal(0, manager.Count);
    }

    /// <summary>
    /// 测试相关入口 MutatingDisposedManager_IsRejected；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Dispose、Throws、Create、Add、Register；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MutatingDisposedManager_IsRejected()
    {
        var manager = new MotionManager();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() => manager.Create(new MotionCardParam()));
        Assert.Throws<ObjectDisposedException>(() => manager.Add(new SimulationMotionCard()));
        Assert.Throws<ObjectDisposedException>(() =>
            manager.Register("company.motion.custom", _ => new SimulationMotionCard()));
        Assert.Throws<ObjectDisposedException>(() => manager.Devices.Add(new SimulationMotionCard()));
    }

    /// <summary>
    /// ThrowingConnectCard 的类型定义。MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：SimulationMotionCard、IMotionCard。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class ThrowingConnectCard : SimulationMotionCard, IMotionCard
    {
        /// <summary>
        /// 测试相关入口 ConnectResult；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        MotionResult IMotionCard.ConnectResult() => throw new InvalidOperationException("driver exploded");
    }
}


