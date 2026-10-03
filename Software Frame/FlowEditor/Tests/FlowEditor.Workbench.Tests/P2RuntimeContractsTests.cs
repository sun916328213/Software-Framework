// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：P2RuntimeContractsTests、AlwaysConfigFailNode、FlakyOfflineNode、AlwaysOfflineNode。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FlowEditor.Controls.Core、FlowEditor.Core、FlowEditor.Runtime、FlowEditor.Controls.Models、FlowEditor.Core.Models、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core.Nodes;
using FlowEditor.Controls.Nodes.FlowControl;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>P2 验收：稳定标识/缺失兼容、重试边界、Telemetry 自动采集。</summary>
[Collection("NodeTypeRegistry")]
public class P2RuntimeContractsTests
{
    /// <summary>
    /// 验证 ClearRegistries 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Clear；具体条件和先后次序见方法体。
    /// </summary>
    private static void ClearRegistries()
    {
        NodeTypeRegistry.Clear();
        PluginRegistry.Clear();
    }

    // ── 稳定标识与插件注册 ──

    [Fact]
    public void Registry_Register_And_Resolve_RoundTrip()
    {
        try
        {
            Assert.Equal("flow.loop", NodeTypeRegistry.Register(typeof(LoopNode)));
            Assert.Same(typeof(LoopNode), NodeTypeRegistry.TryResolve("flow.loop"));
            Assert.Equal("flow.loop", NodeTypeRegistry.TryGetId(typeof(LoopNode)));
            Assert.Null(NodeTypeRegistry.TryResolve("flow.not-exist"));
        }
        finally { ClearRegistries(); }
    }

    /// <summary>
    /// 验证 Registry_LegacyAlias_Resolves_OldDocument 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Register、RegisterLegacyAlias、Same、ResolveDocumentType、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Registry_LegacyAlias_Resolves_OldDocument()
    {
        try
        {
            NodeTypeRegistry.Register(typeof(LoopNode));
            NodeTypeRegistry.RegisterLegacyAlias("OldNs.LoopNode", "flow.loop");
            Assert.Same(typeof(LoopNode),
                NodeTypeRegistry.ResolveDocumentType(null, "OldNs.LoopNode"));
            // 无标识无别名：未知类型返回 null（调用方建占位）。
            Assert.Null(NodeTypeRegistry.ResolveDocumentType(null, "Nope.Missing, Nope"));
        }
        finally { ClearRegistries(); }
    }

    /// <summary>
    /// 验证 PluginRegistry_Tracks_Availability 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Register、True、IsNodeAvailable、False、Unregister；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PluginRegistry_Tracks_Availability()
    {
        try
        {
            PluginRegistry.Register(new PluginManifest(
                "FlowNodes.Device", "1.0",
                ["device.camera-capture"], ["FlowEditor.Runtime"], ["Halcon"]));
            Assert.True(PluginRegistry.IsNodeAvailable("device.camera-capture"));
            Assert.False(PluginRegistry.IsNodeAvailable("vision.blob"));
            Assert.True(PluginRegistry.Unregister("FlowNodes.Device"));
            Assert.False(PluginRegistry.IsNodeAvailable("device.camera-capture"));
        }
        finally { ClearRegistries(); }
    }

    // ── 缺失插件兼容 ──

    [Fact]
    public void Factory_MissingNode_Preserves_Config()
    {
        try
        {
            var factory = new ReflectionNodeFactory { AllowMissingNodes = true };
            var doc = new NodeDocumentModel
            {
                Id = "n1",
                TypeName = "Gone.PluginNode, Gone",
                NodeTypeId = "gone.plugin-node",
                SchemaVersion = 2,
                Name = "老相机",
                IsInEnabled = true,
                IsOutEnabled = true,
                Properties = new Dictionary<string, object?> { ["Exposure"] = 42 },
                CustomInputPorts = [new CustomPortDocument { Id = "p1", Name = "Trigger" }]
            };
            var node = factory.Create(doc);
            var missing = Assert.IsType<MissingNode>(node);
            Assert.Equal("n1", missing.Id);
            Assert.Equal("老相机", missing.Name);
            Assert.Equal("gone.plugin-node", missing.OriginalNodeTypeId);
            Assert.Equal(42, missing.RawProperties["Exposure"]);
            Assert.Contains(missing.CustomInputPorts, p => p.Id == "p1");
        }
        finally { ClearRegistries(); }
    }

    /// <summary>
    /// 验证 MissingNode_Validate_And_Execute_Use_StableCode 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ValidateAsync、CreateTest、Contains、ThrowsAsync、ExecuteAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task MissingNode_Validate_And_Execute_Use_StableCode()
    {
        var missing = new MissingNode { OriginalNodeTypeId = "gone.plugin-node", OriginalTypeName = "Gone.PluginNode" };
        var report = await missing.ValidateAsync(NodeValidationContext.CreateTest("n1"));
        Assert.Contains(report, d => d.Code == "E-PLUGIN-MISSING");
        var ex = await Assert.ThrowsAsync<FlowNodeException>(() => missing.ExecuteAsync(CancellationToken.None));
        Assert.Equal("E-PLUGIN-MISSING", ex.ErrorCode);
        Assert.False(ex.IsRetryable);
    }

    /// <summary>
    /// 验证 MissingNode_RoundTrip_Keeps_Original_Config 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 IsType、Create、ToNodeDocument、Equal、ClearRegistries；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MissingNode_RoundTrip_Keeps_Original_Config()
    {
        try
        {
            var factory = new ReflectionNodeFactory { AllowMissingNodes = true };
            var mapper = new FlowDocumentMapper(factory);
            var doc = new NodeDocumentModel
            {
                Id = "n1",
                TypeName = "Gone.PluginNode, Gone",
                NodeTypeId = "gone.plugin-node",
                SchemaVersion = 2,
                Name = "老相机",
                Properties = new Dictionary<string, object?> { ["Exposure"] = 42 }
            };
            var missing = Assert.IsType<MissingNode>(factory.Create(doc));
            var saved = mapper.ToNodeDocument(missing);
            Assert.Equal("gone.plugin-node", saved.NodeTypeId);
            Assert.Equal("Gone.PluginNode, Gone", saved.TypeName);
            Assert.Equal(2, saved.SchemaVersion);
            Assert.Equal(42, saved.Properties["Exposure"]);
            // 重新加载仍是占位且配置完整（不丢工程）。
            var reloaded = Assert.IsType<MissingNode>(factory.Create(saved));
            Assert.Equal(42, reloaded.RawProperties["Exposure"]);
        }
        finally { ClearRegistries(); }
    }

    /// <summary>
    /// 验证 MissingNode_Upgrade_Restores_RealNode_With_Config 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Register、Upgrade、IsType、Equal、ClearRegistries；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MissingNode_Upgrade_Restores_RealNode_With_Config()
    {
        try
        {
            var factory = new ReflectionNodeFactory { AllowMissingNodes = true };
            var missing = new MissingNode
            {
                OriginalTypeName = typeof(LoopNode).AssemblyQualifiedName!,
                OriginalNodeTypeId = "flow.loop",
                OriginalSchemaVersion = 2
            };
            missing.Id = "loop1";
            missing.Name = "循环";
            missing.RawProperties["Iterations"] = 7;
            NodeTypeRegistry.Register(typeof(LoopNode));
            var upgraded = missing.Upgrade(factory);
            var loop = Assert.IsType<LoopNode>(upgraded);
            Assert.Equal("loop1", loop.Id);
            Assert.Equal(7, loop.Iterations);
        }
        finally { ClearRegistries(); }
    }

    /// <summary>
    /// 验证 Factory_Throws_When_Missing_Disallowed 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Throws、Create；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Factory_Throws_When_Missing_Disallowed()
    {
        var factory = new ReflectionNodeFactory();
        var doc = new NodeDocumentModel { TypeName = "Gone.PluginNode, Gone" };
        Assert.Throws<TypeLoadException>(() => factory.Create(doc));
    }

    /// <summary>
    /// 验证 Mapper_Writes_StableId_And_SchemaVersion 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Register、ToNodeDocument、Equal、ClearRegistries；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void Mapper_Writes_StableId_And_SchemaVersion()
    {
        try
        {
            NodeTypeRegistry.Register(typeof(LoopNode));
            var mapper = new FlowDocumentMapper();
            var doc = mapper.ToNodeDocument(new LoopNode());
            Assert.Equal("flow.loop", doc.NodeTypeId);
            Assert.Equal(2, doc.SchemaVersion);
        }
        finally { ClearRegistries(); }
    }

    // ── 重试边界 ──

    [Fact]
    public void RetryBoundary_Rules()
    {
        Assert.False(RetryBoundary.ShouldRetry(null, 1, 3));
        Assert.False(RetryBoundary.ShouldRetry(new InvalidOperationException("x"), 3, 3));
        Assert.True(RetryBoundary.ShouldRetry(new InvalidOperationException("x"), 1, 3));
        Assert.False(RetryBoundary.ShouldRetry(
            FlowNodeException.Config("E-CFG", "bad"), 1, 3));
        Assert.True(RetryBoundary.ShouldRetry(
            FlowNodeException.DeviceOffline("down"), 1, 3));
        Assert.Equal("E-DEVICE-OFFLINE",
            RetryBoundary.GetErrorCode(FlowNodeException.DeviceOffline("down")));
        Assert.Null(RetryBoundary.GetErrorCode(new InvalidOperationException("x")));
    }

    /// <summary>
    /// 验证 Retry_NonRetryable_Breaks_Early 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、ExecuteAsync、IsType、Equal、Empty；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Retry_NonRetryable_Breaks_Early()
    {
        var model = new AlwaysConfigFailNode { Name = "C", RetryCount = 3, IsInEnabled = false };
        var telemetry = new InMemoryNodeTelemetry();
        var engine = new FlowChartExecutionEngine();
        var ex = await Assert.ThrowsAsync<AggregateException>(() => engine.ExecuteAsync(
            new List<INodeExecutable> { model }, new List<ConnectionModel>(),
            CancellationToken.None, _ => { }, telemetry: telemetry));
        var flow = Assert.IsType<FlowNodeException>(ex.InnerException);
        Assert.Equal("E-DEMO-CONFIG", flow.ErrorCode);
        Assert.Equal(1, model.Attempts);
        Assert.Empty(telemetry.Retries);
        Assert.Single(telemetry.Executions);
        Assert.Equal("E-DEMO-CONFIG", telemetry.Executions[0].ErrorCode);
    }

    /// <summary>
    /// 验证 Retry_Retryable_Honored_With_Telemetry 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、Equal、All、True；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Retry_Retryable_Honored_With_Telemetry()
    {
        var model = new FlakyOfflineNode { Name = "F", RetryCount = 3, IsInEnabled = false };
        var telemetry = new InMemoryNodeTelemetry();
        var engine = new FlowChartExecutionEngine();
        await engine.ExecuteAsync(
            new List<INodeExecutable> { model }, new List<ConnectionModel>(),
            CancellationToken.None, _ => { }, telemetry: telemetry);
        Assert.Equal(3, model.Attempts);
        Assert.Equal(2, telemetry.Retries.Count);
        Assert.All(telemetry.Retries, r => Assert.Equal("E-DEVICE-OFFLINE", r.ErrorCode));
        Assert.True(telemetry.Executions[0].Success);
    }

    // ── Telemetry 自动采集 ──

    [Fact]
    public async Task Telemetry_Records_Failure_With_Code()
    {
        var model = new AlwaysOfflineNode
        {
            Name = "F",
            ErrorPolicy = NodeErrorPolicy.ContinueOnError,
            IsInEnabled = false
        };
        var telemetry = new InMemoryNodeTelemetry();
        var instance = new FlowRuntimeInstance<INodeExecutable>(
            "t", new List<INodeExecutable> { model }, new List<ConnectionModel>(),
            new FlowChartExecutionEngine());
        await instance.RunAsync(CancellationToken.None, null, telemetry);
        var record = Assert.Single(telemetry.Executions);
        Assert.False(record.Success);
        Assert.Equal("E-DEVICE-OFFLINE", record.ErrorCode);
        Assert.True(record.ElapsedMs >= 0);
    }

    // ── 测试 doubles ──

    /// <summary>始终报配置错误的节点（不可重试）。</summary>
    public class AlwaysConfigFailNode : NodeModel
    {
        /// <summary>实际执行次数。</summary>
        public int Attempts;

        /// <inheritdoc />
        /// <remarks>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Increment、Config；具体条件和先后次序见方法体。
        /// </remarks>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Attempts);
            throw FlowNodeException.Config("E-DEMO-CONFIG", "演示配置错误。");
        }
    }

    /// <summary>前两次设备离线、第三次成功的节点（可重试）。</summary>
    public class FlakyOfflineNode : NodeModel
    {
        /// <summary>实际执行次数。</summary>
        public int Attempts;

        /// <inheritdoc />
        /// <remarks>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Increment、DeviceOffline；具体条件和先后次序见方法体。
        /// </remarks>
        public override Task ExecuteAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Attempts);
            if (Attempts < 3)
                throw FlowNodeException.DeviceOffline("设备瞬断。");
            return Task.CompletedTask;
        }
    }

    /// <summary>始终设备离线的节点。</summary>
    public class AlwaysOfflineNode : NodeModel
    {
        /// <inheritdoc />
        /// <remarks>
        /// 验证 ExecuteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 DeviceOffline；具体条件和先后次序见方法体。
        /// </remarks>
        public override Task ExecuteAsync(CancellationToken ct) =>
            throw FlowNodeException.DeviceOffline("设备离线。");
    }
}
