using System.IO;
using System.Reflection;
using MachineApp.Business.Flow;
using MachineApp.Business.Ports;
using MachineApp.Infra.Plc;
using MachineApp.Infra.Tool;
using Prism.Ioc;
using SecsGem.Core;
using WPFControls.Controls.Diagnostics;
using Xunit;

namespace MachineApp.Host.Tests;

[Collection("ProductionComposition")]
public sealed class HostWindowShutdownTests(ProductionFixture fixture)
{
    [Fact]
    public async Task ActualMainWindowWaitsForCleanupAndRepeatedCloseStartsOnce()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var disposed = 0;
        var errors = new List<Exception>();
        var printer = Port<IProductionPrinter>(_ => null);
        var plc = Port<IProductionPlc>(_ => Task.CompletedTask);
        var secs = Port<ISecsGemRuntime>(_ => { calls++; return release.Task.WaitAsync(TimeSpan.FromSeconds(10)); });
        var devices = Port<IProductionDevices>(_ => { disposed++; return ValueTask.CompletedTask; });
        var cleanup = new Close(() => printer, () => plc, () => devices, secs, new TryCatchHelper(errors.Add));
        MainWindow? window = null;
        var file = Path.GetFullPath("Config/GlobalData.json");
        var bytes = File.Exists(file) ? File.ReadAllBytes(file) : null;
        try
        {
            fixture.Invoke(container =>
            {
                var process = (AutoProcess)typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(container.Resolve<AutoProcess>(), null)!;
                var provider = DispatchProxy.Create<IContainerProvider, ContainerProxy>();
                ((ContainerProxy)provider).Resolve = type => type == typeof(Close) ? cleanup :
                    type == typeof(AutoProcess) ? process : container.Resolve(type);
                window = new MainWindow(provider);
                window.Closed += (_, _) => closed.TrySetResult();
                window.Close();
                window.Close();
                Assert.Equal(1, calls);
                Assert.False(closed.Task.IsCompleted);
            });
            // Returning to and invoking the real STA dispatcher while disconnect is pending proves responsiveness.
            fixture.Invoke(_ => Assert.False(closed.Task.IsCompleted));
            Assert.Equal(0, disposed);
            release.SetResult();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, disposed);
            Assert.Empty(errors);
        }
        finally
        {
            release.TrySetResult();
            fixture.Invoke(_ => { if (!closed.Task.IsCompleted) window?.Close(); });
            if (bytes != null) File.WriteAllBytes(file, bytes);
            else if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public async Task ProductionPlcStopYieldsWhileAnEarlierOperationHoldsItsGate()
    {
        // SLMP is constructed but never connected; no socket or vendor COM instance is opened.
        var service = new PlcService(new PlcOptions { Backend = "Slmp", ServerIP = "127.0.0.1" });
        var gate = (SemaphoreSlim)typeof(PlcService).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        await gate.WaitAsync();
        Task? pending = null;
        try
        {
            fixture.Invoke(container =>
            {
                IProductionPlc plc = new PLCTool(container, service, container.Resolve<PlcTagCatalog>());
                pending = plc.StopRefreshAsync();
                Assert.False(pending.IsCompleted);
            });
            fixture.Invoke(_ => Assert.False(pending!.IsCompleted));
        }
        finally
        {
            gate.Release();
            if (pending != null) await pending.WaitAsync(TimeSpan.FromSeconds(10));
            await service.DisposeAsync();
        }
    }

    private static T Port<T>(Func<string, object?> handler) where T : class
    {
        var port = DispatchProxy.Create<T, HostAsyncBoundaryTests.PortProxy>();
        ((HostAsyncBoundaryTests.PortProxy)(object)port).Handler = handler;
        return port;
    }

    public class ContainerProxy : DispatchProxy
    {
        public Func<Type, object> Resolve { get; set; } = _ => throw new NotSupportedException();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod!.Name == "Resolve" ? Resolve((Type)args![0]!) : throw new NotSupportedException(targetMethod.Name);
    }
}
