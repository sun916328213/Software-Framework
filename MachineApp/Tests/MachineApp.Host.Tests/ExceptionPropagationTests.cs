using System.Reflection;
using System.Runtime.CompilerServices;
using DataManager.Core.Log.Definition;
using MachineApp.Business.Flow;
using MachineApp.Business.Ports;
using MachineApp.Business.Station;
using MachineApp.Infra.Tool;
using Prism.Ioc;
using WPFControls.ProgramControl.Services;
using Xunit;

namespace MachineApp.Host.Tests;

[Collection("ProductionComposition")]
public sealed class ExceptionPropagationTests(ProductionFixture fixture)
{
    [Fact]
    public async Task SecsCallbackFailurePreservesOriginAndExceptionIdentity()
    {
        var tools = new List<SecsTool>();
        fixture.Invoke(container => tools.Add(container.Resolve<SecsTool>()));
        var tool = Assert.Single(tools);
        var fault = new InvalidOperationException("callback-fault");
        Func<Task> callback = () => ThrowOriginAsync(fault);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => tool.Run(callback, callback, callback));

        Assert.Same(fault, actual);
        Assert.Contains(nameof(ThrowOriginAsync), actual.StackTrace);
    }

    [Theory]
    [InlineData(typeof(ReadID), "Readlabel", 2)]
    [InlineData(typeof(Label), "ReadLabel", 2)]
    [InlineData(typeof(HeatSeal), "Detect", 1)]
    public async Task VisionFailurePreservesOriginAndStationLogging(Type stationType, string methodName, int logCount)
    {
        var log = new Log();
        var vision = DispatchProxy.Create<IProductionVision, FailingVision>();
        var failure = Assert.IsAssignableFrom<FailingVision>(vision).Fault;
        var stations = new List<object>();
        fixture.Invoke(container =>
        {
            var existing = container.Resolve<StationServices>();
            var services = new StationServices(() => log, existing.Secs, () => vision,
                existing.Devices, existing.Printer, existing.Plc);
            stations.Add(stationType == typeof(ReadID)
                ? new ReadID(services, () => container.Resolve<DisplayService>(), () => container.Resolve<Recipe>())
                : Activator.CreateInstance(stationType, services) ?? throw new InvalidOperationException());
        });
        var station = Assert.Single(stations);
        InvalidOperationException actual;
        if (station is HeatSeal heatSeal)
            actual = await Assert.ThrowsAsync<InvalidOperationException>(() => heatSeal.Detect("ID"));
        else
        {
            var method = stationType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Station entry missing");
            object?[] args = station is ReadID ? ["ID"] : ["1", "ID", null];
            var invocation = Assert.Throws<TargetInvocationException>(() => method.Invoke(station, args));
            actual = Assert.IsType<InvalidOperationException>(invocation.InnerException);
        }

        Assert.Same(failure, actual);
        Assert.Contains(nameof(ThrowOriginAsync), actual.StackTrace);
        Assert.Equal(logCount, log.Messages.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task ThrowOriginAsync(Exception fault) => throw fault;

    public class FailingVision : DispatchProxy
    {
        public InvalidOperationException Fault { get; } = new("camera-fault");
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name is not ("GrabImage" or "GrabImages"))
                throw new NotSupportedException("Unexpected vision operation");
            return ThrowOriginAsync(Fault);
        }
    }

    private sealed class Log : IProductionLog
    {
        public List<string> Messages { get; } = [];
        public void AddRunInfo(string info, ELogLevel level = ELogLevel.Information, int retainNum = 1000)
            => Messages.Add(info);
    }
}
