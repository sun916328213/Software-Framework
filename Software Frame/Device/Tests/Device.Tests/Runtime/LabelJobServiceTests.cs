// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：LabelJobServiceTests、Factory、Engine。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Core.Devices、Device.Core.LabelPrinting、Device.Runtime.LabelPrinting、Xunit。导入不代表所有类型都会在运行时加载。
using Device.Core.Devices;
using Device.Core.LabelPrinting;
using Device.Runtime.LabelPrinting;
using Xunit;

namespace Device.Tests.Runtime;

/// <summary>
/// LabelJobServiceTests 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class LabelJobServiceTests
{
    private static readonly LabelPrintEngineOptions Options = new() { Name = "labels", DriverId = new("test") };

    /// <summary>
    /// 测试相关入口 CompleteJobPassesTemplateFieldsPrinterAndCopiesThenReleasesEngine；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 PrintAsync、Single、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CompleteJobPassesTemplateFieldsPrinterAndCopiesThenReleasesEngine()
    {
        var factory = new Factory();
        await new LabelJobService(factory).PrintAsync(Options, new("label.btw", "printer", new Dictionary<string, string> { ["ID"] = "123" }, 2));
        var engine = Assert.Single(factory.Engines);
        Assert.Equal(new[] { "open:label.btw", "fields:123", "print:2:printer", "close", "dispose" }, engine.Calls);
    }

    /// <summary>
    /// 测试相关入口 FailureIsReportedAndEngineIsReleased；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、PrintAsync、Equal、Last、Single；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData("open")]
    [InlineData("fields")]
    [InlineData("print")]
    public async Task FailureIsReportedAndEngineIsReleased(string failure)
    {
        var factory = new Factory { Failure = failure };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new LabelJobService(factory)
            .PrintAsync(Options, new("label.btw", Fields: new Dictionary<string, string> { ["ID"] = "123" })));
        Assert.Equal(failure, error.Message);
        Assert.Equal("dispose", Assert.Single(factory.Engines).Calls.Last());
        if (failure != "open") Assert.Contains("close", factory.Engines[0].Calls);
    }

    /// <summary>
    /// 测试相关入口 ConcurrentJobsDoNotInterleaveEngineLifetimes；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 PrintAsync、WaitAsync、FromSeconds、Single、SetResult；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ConcurrentJobsDoNotInterleaveEngineLifetimes()
    {
        var factory = new Factory { PausePrint = true };
        var service = new LabelJobService(factory);
        var first = service.PrintAsync(Options, new("first.btw"));
        await factory.PrintEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.PrintAsync(Options, new("second.btw"));
        Assert.Single(factory.Engines);
        factory.PrintRelease.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, factory.Engines.Count);
        Assert.All(factory.Engines, engine => Assert.Equal("dispose", engine.Calls.Last()));
    }

    /// <summary>
    /// 测试相关入口 CancellationDoesNotCancelCleanupAndGateCanBeReused；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAnyAsync、PrintAsync、Equal、TakeLast；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CancellationDoesNotCancelCleanupAndGateCanBeReused()
    {
        using var cancellation = new CancellationTokenSource();
        var factory = new Factory { Cancel = cancellation };
        var service = new LabelJobService(factory);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrintAsync(Options, new("first.btw"), cancellation.Token));
        Assert.Equal(new[] { "close", "dispose" }, factory.Engines[0].Calls.TakeLast(2));
        factory.Cancel = null;
        await service.PrintAsync(Options, new("next.btw"));
        Assert.Equal(2, factory.Engines.Count);
    }

    /// <summary>
    /// 测试相关入口 CleanupFailurePreservesOriginalPrintFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、PrintAsync、Equal、Select、Last；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CleanupFailurePreservesOriginalPrintFailure()
    {
        var factory = new Factory { Failure = "print", CloseFailure = true };
        var failure = await Assert.ThrowsAsync<AggregateException>(() => new LabelJobService(factory).PrintAsync(Options, new("label.btw")));
        Assert.Equal(new[] { "print", "close" }, failure.InnerExceptions.Select(error => error.Message));
        Assert.Equal("dispose", factory.Engines[0].Calls.Last());
    }

    /// <summary>
    /// Factory 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILabelPrintEngineFactory。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Factory : ILabelPrintEngineFactory
    {
        public string? Failure { get; set; }
        public bool CloseFailure { get; set; }
        public bool PausePrint { get; set; }
        public CancellationTokenSource? Cancel { get; set; }
        public List<Engine> Engines { get; } = [];
        public TaskCompletionSource PrintEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource PrintRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<DeviceDriverDescriptor> AvailableDrivers => [];
        /// <summary>
        /// 测试相关入口 Create；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public ILabelPrintEngine Create(LabelPrintEngineOptions options) { var engine = new Engine(this, options); Engines.Add(engine); return engine; }
    }

    /// <summary>
    /// Engine 的类型定义。Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：ILabelPrintEngine。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class Engine(Factory factory, LabelPrintEngineOptions options) : ILabelPrintEngine
    {
        public List<string> Calls { get; } = [];
        public string Name => options.Name;
        public LabelPrintEngineOptions Options => options;
        public string? TemplatePath { get; private set; }
        /// <summary>
        /// 测试相关入口 GetInstalledPrintersAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask<DeviceResult<IReadOnlyList<string>>> GetInstalledPrintersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 GetFieldsAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask<DeviceResult<IReadOnlyList<LabelTemplateField>>> GetFieldsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 RenderPreviewAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public ValueTask<DeviceResult<LabelPreview>> RenderPreviewAsync(CancellationToken ct = default) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 OpenTemplateAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add、Result；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> OpenTemplateAsync(string path, CancellationToken ct = default) { Calls.Add("open:" + path); TemplatePath = path; return Result("open"); }
        /// <summary>
        /// 测试相关入口 SetFieldsAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add、Result；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> SetFieldsAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct = default) { Calls.Add("fields:" + fields["ID"]); return Result("fields"); }
        /// <summary>
        /// 测试相关入口 PrintAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add、TrySetResult、ThrowIfCancellationRequested、Result；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async ValueTask<DeviceResult> PrintAsync(int copies = 1, string? printerName = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"print:{copies}:{printerName}");
            factory.PrintEntered.TrySetResult();
            if (factory.PausePrint) await factory.PrintRelease.Task;
            factory.Cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return await Result("print");
        }
        /// <summary>
        /// 测试相关入口 CloseTemplateAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 False、Add、FromResult、Failure、Ok；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask<DeviceResult> CloseTemplateAsync(CancellationToken ct = default)
        {
            Assert.False(ct.IsCancellationRequested); Calls.Add("close"); TemplatePath = null;
            return ValueTask.FromResult(factory.CloseFailure ? DeviceResult.Failure("close", "close") : DeviceResult.Ok());
        }
        /// <summary>
        /// 测试相关入口 DisposeAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public ValueTask DisposeAsync() { Calls.Add("dispose"); return ValueTask.CompletedTask; }
        /// <summary>
        /// 测试相关入口 Result；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FromResult、Failure、Ok；具体条件和先后次序见方法体。
        /// </summary>
        private ValueTask<DeviceResult> Result(string operation) => ValueTask.FromResult(factory.Failure == operation ? DeviceResult.Failure(operation, operation) : DeviceResult.Ok());
    }
}
