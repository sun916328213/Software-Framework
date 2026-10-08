// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowNodeSeamTests、RecordingEditor、RecordingRuntime、RecordingSession、RecordingTool、RecordingVisionNode、RecordingVisionExecutor。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.ComponentModel、System.IO、System.Text.Json、FlowEditor.Controls.Core、FlowEditor.Controls.Interfaces、FlowEditor.Controls.Nodes。导入不代表所有类型都会在运行时加载。
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Nodes;
using FlowEditor.Controls.Services.Storage;
using FlowEditor.Core.Models;
using FlowNodes.Script;
using FlowNodes.Vision.Execution;
using FlowNodes.Vision.SingleTool.Definition;
using HalconDotNet;
using Prism.Ioc;
using ScriptEditor.Core.Contracts;
using ScriptEditor.Core.Models;
using ScriptEditor.Core.Options;
using Vision.Core.Results;
using Vision.Core.Tools;
using Vision.Tools.Halcon.Sessions;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// FlowNodeSeamTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ProductionComposition")]
public sealed class FlowNodeSeamTests
{
    /// <summary>
    /// 测试相关入口 NodeDefinitionsKeepWindowConstructionAndConcreteRoslynOutsideExecution；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Exists、Combine、NotNull、ReadAllText、DoesNotContain；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NodeDefinitionsKeepWindowConstructionAndConcreteRoslynOutsideExecution()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Build", "Test-Integration.ps1"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var relativePath in new[] { "Software Frame/FlowNodes/FlowNodes/Script/ScriptNode.cs",
                     "Software Frame/FlowNodes/FlowNodes/Vision/SingleTool/Definition/VisionToolNodeBase.cs" })
        {
            var source = File.ReadAllText(Path.Combine(root!.FullName, relativePath));
            Assert.DoesNotContain("using System.Windows;", source);
            Assert.DoesNotContain("new Window", source);
            Assert.DoesNotContain("ShowDialog()", source);
            Assert.DoesNotContain("RoslynScriptRuntime.Shared", source);
        }
    }

    /// <summary>
    /// 测试相关入口 ScriptExecutionUsesInjectedRuntimeAndReleasesEachIndependentSession；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、ExecuteAsync、Equal、NotSame、Single；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ScriptExecutionUsesInjectedRuntimeAndReleasesEachIndependentSession()
    {
        var runtime = new RecordingRuntime();
        var editor = new RecordingEditor();
        var node = new ScriptNode(runtime, editor) { Name = "script", EditorState = new() { Code = "return Inputs[\"count\"];" } };
        node.EditorState.References.Add(" ");
        node.EditorState.References.Add("input.dll");
        node.CustomInputPorts.Add(new PortModel { Name = "count", Value = 42 });

        await node.ExecuteAsync(CancellationToken.None);
        await node.ExecuteAsync(CancellationToken.None);

        Assert.Equal(42, node.ScriptResult);
        Assert.Equal(2, runtime.Created.Count);
        Assert.NotSame(runtime.Created[0], runtime.Created[1]);
        Assert.Equal(runtime.Created, runtime.Released);
        Assert.Equal("script", runtime.Title);
        Assert.Equal(node.EditorState.Code, runtime.Code);
        Assert.Single(runtime.References);
        Assert.Equal("input.dll", runtime.References[0].Path);
        Assert.All(runtime.Created, session => Assert.Equal(42, Assert.IsType<Dictionary<string, object?>>(session.Inputs)["count"]));
        Assert.Null(editor.LastNode);
    }

    /// <summary>
    /// 测试相关入口 EmptyScriptKeepsNoOpBehaviorWithoutCreatingSession；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExecuteAsync、Null、Empty；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task EmptyScriptKeepsNoOpBehaviorWithoutCreatingSession()
    {
        var runtime = new RecordingRuntime();
        var node = new ScriptNode(runtime, new RecordingEditor()) { ScriptResult = "previous" };
        await node.ExecuteAsync(CancellationToken.None);
        Assert.Null(node.ScriptResult);
        Assert.Empty(runtime.Created);
    }

    /// <summary>
    /// 测试相关入口 ScriptFailureKeepsResultAndDiagnosticAndReleasesSession；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task ScriptFailureKeepsResultAndDiagnosticAndReleasesSession()
    {
        var runtime = new RecordingRuntime { Success = false };
        var node = new ScriptNode(runtime, new RecordingEditor()) { Name = "failed", EditorState = new() { Code = "invalid" } };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Equal("脚本执行失败 [failed]：output", error.Message);
        Assert.Equal(42, node.ScriptResult);
        Assert.Equal(runtime.Created, runtime.Released);
    }

    /// <summary>
    /// 测试相关入口 SessionThrowsOrCancelsStillReleasesAndPreservesOriginalException；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ExceptionAsync、ExecuteAsync、Same、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionThrowsOrCancelsStillReleasesAndPreservesOriginalException(bool cancel)
    {
        Exception error = cancel ? new OperationCanceledException("cancelled") : new InvalidOperationException("run fault");
        var runtime = new RecordingRuntime { RunError = error };
        var node = new ScriptNode(runtime, new RecordingEditor()) { EditorState = new() { Code = "run" } };
        var actual = await Record.ExceptionAsync(() => node.ExecuteAsync(CancellationToken.None));
        Assert.Same(error, actual);
        Assert.Equal(runtime.Created, runtime.Released);
    }

    /// <summary>
    /// 测试相关入口 ReflectionFactoryUsesRegisteredScriptPortsAndPreservesDocumentIdentity；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterService、Add、ToNodeDocument、Combine、GetTempPath；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ReflectionFactoryUsesRegisteredScriptPortsAndPreservesDocumentIdentity()
    {
        var runtime = new RecordingRuntime();
        var editor = new RecordingEditor();
        var factory = new ReflectionNodeFactory();
        factory.RegisterService<IScriptRuntime>(runtime);
        factory.RegisterService<INodeEditorLauncher>(editor);
        var mapper = new FlowDocumentMapper(factory);
        var original = new ScriptNode { Name = "saved", EditorState = new() { Code = "return 42;" } };
        original.EditorState.References.Add("saved.dll");
        var descriptor = mapper.ToNodeDocument(original);
        var file = Path.Combine(Path.GetTempPath(), $"s1-script-node-{Guid.NewGuid():N}.flow");
        string serialized;
        ScriptNode restored;
        try
        {
            var store = new JsonFlowDocumentStore();
            store.Save(file, descriptor);
            serialized = File.ReadAllText(file);
            restored = Assert.IsType<ScriptNode>(mapper.ToNode(store.Load<NodeDocumentModel>(file)));
        }
        finally { File.Delete(file); }

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal("FlowNodes.Script.ScriptNode", restored.GetType().FullName);
        Assert.Equal(original.EditorState.Code, restored.EditorState.Code);
        Assert.Equal(original.EditorState.References, restored.EditorState.References);
        restored.EditScript();
        Assert.Same(restored, editor.LastNode);
        Assert.DoesNotContain("_getRuntime", serialized);
    }

    /// <summary>
    /// 测试相关入口 DefaultScriptAdapterKeepsRealRoslynInputAndResult；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Add、ExecuteAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task DefaultScriptAdapterKeepsRealRoslynInputAndResult()
    {
        var node = new ScriptNode { EditorState = new() { Code = "return (int)Inputs[\"count\"] + 1;" } };
        node.CustomInputPorts.Add(new PortModel { Name = "count", Value = 41 });
        await node.ExecuteAsync(CancellationToken.None);
        Assert.Equal(42, node.ScriptResult);
    }

    /// <summary>
    /// 测试相关入口 PrismFactoryHonorsHostRuntimeReplacement；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 RegisterInstance、RegisterTypes、IsType、Create、Resolve；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task PrismFactoryHonorsHostRuntimeReplacement()
    {
        var runtime = new RecordingRuntime();
        var container = new Prism.DryIoc.DryIocContainerExtension();
        ((Prism.Ioc.IContainerRegistry)container).RegisterInstance<IScriptRuntime>(runtime);
        ((Prism.Ioc.IContainerRegistry)container).RegisterInstance<INodeEditorLauncher>(new RecordingEditor());
        new FlowEditor.Controls.FlowEditorModule().RegisterTypes(container);
        var node = Assert.IsType<ScriptNode>(container.Resolve<INodeFactory>().Create(typeof(ScriptNode)));
        node.EditorState.Code = "host runtime";
        await node.ExecuteAsync(CancellationToken.None);
        Assert.Single(runtime.Created);
        Assert.Equal(runtime.Created, runtime.Released);
    }

    /// <summary>
    /// 测试相关入口 DefaultVisualExecutorKeepsToolInputRunAndResultOrder；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Execute、Equal、Null；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DefaultVisualExecutorKeepsToolInputRunAndResultOrder()
    {
        using var tool = new RecordingTool();
        var result = new HalconVisionNodeExecutor().Execute(tool, null);
        Assert.Equal(1, tool.RunCount);
        Assert.Null(tool.Image);
        Assert.Equal(1, result.ResultCount);
        Assert.Equal(E_ToolState.RunCompleted, result.State);
        Assert.Equal("done", result.Message);
    }

    /// <summary>
    /// 测试相关入口 VisualNodeUsesReplaceableExecutionAndUpdatesCountBeforeFailure；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 SetExecutionAdapter、ThrowsAsync、ExecuteAsync、Equal、Same；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VisualNodeUsesReplaceableExecutionAndUpdatesCountBeforeFailure(bool fail)
    {
        var node = new RecordingVisionNode();
        var executor = new RecordingVisionExecutor(fail);
        node.SetExecutionAdapter(executor);
        try
        {
            if (fail)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => node.ExecuteAsync(CancellationToken.None));
                Assert.Equal("vision fault", error.Message);
            }
            else await node.ExecuteAsync(CancellationToken.None);
            Assert.Equal(3, node.ResultCount);
            Assert.Same(node.visionTool, executor.Tool);
            Assert.Equal(0, node.visionTool.RunCount);
            var properties = new FlowDocumentMapper().ToNodeDocument(node).Properties;
            Assert.DoesNotContain(properties.Keys, key => key.Contains("Executor", StringComparison.Ordinal));
        }
        finally { ToolMge.Instance.Remove(node.visionTool); node.visionTool.Dispose(); }
    }

    /// <summary>
    /// RecordingEditor 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：INodeEditorLauncher。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RecordingEditor : INodeEditorLauncher
    {
        public NodeModel? LastNode { get; private set; }
        /// <summary>
        /// 测试相关入口 Open；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Open(NodeModel node) => LastNode = node;
    }

    /// <summary>
    /// RecordingRuntime 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IScriptRuntime。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RecordingRuntime : IScriptRuntime
    {
        public bool Success { get; init; } = true;
        public Exception? RunError { get; init; }
        public List<RecordingSession> Created { get; } = [];
        public List<RecordingSession> Released { get; } = [];
        public string? Title { get; private set; }
        public string? Code { get; private set; }
        public IReadOnlyList<ScriptReference> References { get; private set; } = [];
        /// <summary>
        /// 测试相关入口 CreateDetachedSession；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 getCode、Add；具体条件和先后次序见方法体。
        /// </summary>
        public IScriptSession CreateDetachedSession(string title, Func<string> getCode,
            Func<IReadOnlyList<ScriptReference>>? getReferences = null, Func<IEnumerable<int>>? getBreakpoints = null)
        {
            Title = title; Code = getCode(); References = getReferences?.Invoke() ?? [];
            var session = new RecordingSession(Success, RunError); Created.Add(session); return session;
        }
        /// <summary>
        /// 测试相关入口 ReleaseSession；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public void ReleaseSession(IScriptSession session) => Released.Add((RecordingSession)session);
        /// <summary>
        /// 测试相关入口 GetSession；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public IScriptSession GetSession(ScriptItem item) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 FindSession；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public IScriptSession? FindSession(ScriptItem item) => null;
        /// <summary>
        /// 测试相关入口 ReleaseSession；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void ReleaseSession(ScriptItem item) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task<ScriptExecutionResult> ExecuteAsync(ScriptProjectItem script, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 CompileAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task<bool> CompileAsync(ScriptProjectItem script, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// RecordingSession 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IScriptSession。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RecordingSession(bool success, Exception? error) : IScriptSession
    {
        public object? Inputs { get; private set; }
        public Guid ScriptId { get; } = Guid.NewGuid();
        public string Title => "session";
        public bool IsRunning => false;
        public bool IsLooping => false;
        public string Output => "output";
        public string StatusText => "status";
        public string ExecutionTime => "0";
        public int LoopInterval { get; set; }
        public ScriptCompilationState CompilationState => default;
        public object? LastResult => 42;
        public ScriptProjectContext? ProjectContext { get; set; }
        public event Action? StateChanged { add { } remove { } }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        /// <summary>
        /// 测试相关入口 RunAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 FromResult、FromException；具体条件和先后次序见方法体。
        /// </summary>
        public Task<bool> RunAsync() => error is null ? Task.FromResult(success) : Task.FromException<bool>(error);
        /// <summary>
        /// 测试相关入口 SetInputData；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SetInputData(object? data) => Inputs = data;
        /// <summary>
        /// 测试相关入口 LoopRunAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task LoopRunAsync() => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 CompileAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task<bool> CompileAsync() => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 PreCompileAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public Task PreCompileAsync(string code) => throw new NotSupportedException();
        /// <summary>
        /// 测试相关入口 Terminate；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void Terminate() { }
        /// <summary>
        /// 测试相关入口 StopLoop；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void StopLoop() { }
        /// <summary>
        /// 测试相关入口 FlushOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void FlushOutput() { }
        /// <summary>
        /// 测试相关入口 SetOutput；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SetOutput(string output) { }
        /// <summary>
        /// 测试相关入口 SetStatus；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public void SetStatus(string statusText) { }
    }

    /// <summary>
    /// RecordingTool 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：HalconToolSession。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class RecordingTool : HalconToolSession
    {
        public int RunCount { get; private set; }
        public override object EditableParameters { get; } = new object();
        /// <summary>
        /// 测试相关入口 Validate；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public override ValidationResult Validate() => new();
        /// <summary>
        /// 测试相关入口 Run；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Add；具体条件和先后次序见方法体。
        /// </summary>
        public override void Run()
        {
            RunCount++; Results.Add(new ToolResult()); ToolState = E_ToolState.RunCompleted; RunMessage = "done";
        }
        /// <summary>
        /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Reset；具体条件和先后次序见方法体。
        /// </summary>
        public override void Dispose() => Reset();
    }

    /// <summary>
    /// RecordingVisionNode 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：VisionToolNodeBase&lt;RecordingTool&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class RecordingVisionNode : VisionToolNodeBase<RecordingTool>
    {
        /// <summary>
        /// 测试相关入口 ExecuteAsync；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、RunTool；具体条件和先后次序见方法体。
        /// </summary>
        public override Task ExecuteAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); RunTool(); return Task.CompletedTask; }
    }

    /// <summary>
    /// RecordingVisionExecutor 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IVisionNodeExecutor。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class RecordingVisionExecutor(bool fail) : IVisionNodeExecutor
    {
        public HalconToolSession? Tool { get; private set; }
        /// <summary>
        /// 测试相关入口 Execute；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public VisionNodeExecutionResult Execute(HalconToolSession tool, HObject? image)
        {
            Tool = tool;
            return new(3, fail ? E_ToolState.Failed : E_ToolState.RunCompleted, "vision fault");
        }
    }
}
