using System.Reflection;
using System.Runtime.CompilerServices;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;
using Xunit;

namespace WPFControls.Tests;

public class ResourceMonitorLifecycleTests
{
    [Fact]
    public void QueuedPublicationContainsSubscriberFailure()
    {
        using var service = Create();
        service.StartAutoRefresh(60000);
        var timer = typeof(ResourceMonitorService).GetField("refreshTimer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service);
        service.Model.PropertyChanged += (_, _) => throw new InvalidOperationException("subscriber-fault");
        var publish = typeof(ResourceMonitorService).GetMethod("PublishSample", BindingFlags.NonPublic | BindingFlags.Instance)!;
        publish.Invoke(service, [timer, 1f, (2f, "memory"), (3d, "disk")]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueuedSampleFromStoppedTimerCannotPublishIntoRestart(bool restart)
    {
        using var service = Create();
        service.StartAutoRefresh(60000);
        var timer = typeof(ResourceMonitorService).GetField("refreshTimer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service);
        service.Model.SystemCPU = 777;
        service.StopAutoRefresh();
        if (restart) service.StartAutoRefresh(60000);
        var publish = typeof(ResourceMonitorService).GetMethod("PublishSample", BindingFlags.NonPublic | BindingFlags.Instance)!;
        publish.Invoke(service, [timer, 1f, (2f, "memory"), (3d, "disk")]);
        Assert.Equal(777, service.Model.SystemCPU);
        if (restart)
        {
            var current = typeof(ResourceMonitorService).GetField("refreshTimer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service);
            publish.Invoke(service, [current, 1f, (2f, "memory"), (3d, "disk")]);
            Assert.Equal(1, service.Model.SystemCPU);
        }
    }

    [Fact]
    public void DisposedMonitorCannotRestart()
    {
        var service = Create();
        service.Dispose();
        try { Assert.Throws<ObjectDisposedException>(() => service.StartAutoRefresh(60000)); }
        finally { service.StopAutoRefresh(); }
    }

    [Fact]
    public void CounterCalculationsAfterDisposalDoNotPublish()
    {
        var service = Create();
        service.Model.SystemCPU = 777;
        service.Model.SystemMemory = 888;
        service.Dispose();
        service.CalCPUSystem();
        service.CalMemorySystem();
        Assert.Equal(777, service.Model.SystemCPU);
        Assert.Equal(888, service.Model.SystemMemory);
    }

    private static ResourceMonitorService Create()
    {
        var service = (ResourceMonitorService)RuntimeHelpers.GetUninitializedObject(typeof(ResourceMonitorService));
        typeof(ResourceMonitorService).GetField("lockObject", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, new object());
        typeof(ResourceMonitorService).GetField("<Model>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, new ResourceMonitorModel());
        return service;
    }
}
