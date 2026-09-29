using MotionControl.Core.Abstractions;
using MotionControl.Core.Definitions;
using MotionControl.Runtime.Services;
using MotionControl.Runtime.Vendors.Simulation;
using Xunit;

namespace MotionControl.Tests;

public sealed class MotionManagerTests
{
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

    [Fact]
    public void Register_DefaultDriverId_IsRejected()
    {
        var manager = new MotionManager();

        Assert.Throws<ArgumentException>(() =>
            manager.Register(default(MotionDriverId), _ => new SimulationMotionCard()));
    }

    [Fact]
    public void Create_FactoryReturnsNull_ReportsDriverId()
    {
        var manager = new MotionManager();
        manager.Register("company.motion.invalid", _ => null!);
        var parameter = new MotionCardParam { DriverId = "company.motion.invalid" };

        var exception = Assert.Throws<InvalidOperationException>(() => manager.Create(parameter));

        Assert.Contains("company.motion.invalid", exception.Message);
    }

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

    private sealed class ThrowingConnectCard : SimulationMotionCard, IMotionCard
    {
        MotionResult IMotionCard.ConnectResult() => throw new InvalidOperationException("driver exploded");
    }
}


