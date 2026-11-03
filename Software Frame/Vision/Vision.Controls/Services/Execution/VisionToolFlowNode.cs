using System.Text.Json.Serialization;
using FlowEditor.Controls.Nodes;
using FlowEditor.Core;
using Vision.Core.Geometry;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;
using Vision.Core.Tools;

#nullable enable
namespace Vision.Controls.Services;

/// <summary>将独立图像工具接入现有流程执行器；不创建第二套调度、结果或编辑会话。</summary>
/// <remarks>
/// 构造成功后本节点拥有工具与每次结果，图像及基准 ROI 仅借用。
/// 宿主必须在任务移除/关闭时 Dispose，不能因编辑视图卸载而释放节点。
/// 当前为程序化任务接入：不注册工具箱占位项，不宣称支持流程文档恢复。
/// </remarks>
public sealed class VisionToolFlowNode : NodeModel, IDisposable
{
    private readonly IImageTool _tool;
    private readonly object _gate = new();
    private bool _running;
    private bool _disposed;

    /// <summary>接管一个独立工具实例；同一工具不可同时交给其他节点或卡片拥有。</summary>
    public VisionToolFlowNode(IImageTool tool)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        Name = tool.Name;
    }

    /// <summary>借用的输入图像；为 null 时仍遵循原工具的校验/取图行为。</summary>
    [JsonIgnore, NodeInput]
    public VisionImage? Image { get; set; }

    /// <summary>基准图像中的搜索 ROI；运行不修改此对象。仅用于声明搜索域能力的工具。</summary>
    public Roi? SearchRoi { get; set; }

    /// <summary>基准图像中定位坐标系的位姿，角度为度。</summary>
    public Pose2D ReferenceFixture { get; set; }

    /// <summary>本帧定位位姿；null 不跟随。当前只接入平移/旋转，不隐式处理缩放或镜像。</summary>
    [JsonIgnore, NodeInput]
    public Pose2D? Fixture { get; set; }

    /// <summary>最近结果，由节点拥有；下游仅借用，至本节点重跑或任务释放前有效。</summary>
    [JsonIgnore, NodeOutput]
    public ToolResult? Result { get; private set; }

    /// <summary>
    /// 沿用工具的同步执行与线程约定。取消只在调用前后检查，不把停止等待冒充 SDK 已停止。
    /// 算法失败转为流程异常，由原执行器应用失败/重试策略。
    /// </summary>
    public override Task ExecuteAsync(CancellationToken ct)
    {
        ToolResult? previous;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_running) throw new InvalidOperationException("同一视觉工具实例不能重入执行。");
            _running = true;
            previous = Result;
            Result = null;
        }
        ToolResult? pending = null;
        var target = _tool as ISearchRegionTool;
        SearchRegion? previousRegion = null;
        var capturedRegion = false;
        try
        {
            // 失败/取消也必须清空旧端口，不能让下游继续取得上一次成功结果。
            SyncOutputValues();
            previous?.Dispose();
            ct.ThrowIfCancellationRequested();
            if (target != null)
            {
                previousRegion = target.SearchRegion;
                capturedRegion = true;
            }
            if (SearchRoi != null || Fixture != null)
            {
                if (target == null) throw new InvalidOperationException("此工具未声明搜索域能力，不能应用定位 ROI。");
                if (SearchRoi == null) throw new InvalidOperationException("定位跟随需要基准搜索 ROI。");
                target.SearchRegion = SearchRegionPreparation.Capture(SearchRoi, ReferenceFixture, Fixture);
            }
            pending = _tool is Vision.Core.Presentation.ISearchRegionFixtureTool prepared
                ? prepared.PrepareSearchRegionRun(Image, target!.SearchRegion, ReferenceFixture, Fixture)()
                : _tool.Run(Image);
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                Result = pending;
                pending = null;
            }
            if (!Result.Success) throw new InvalidOperationException($"{Name}：{Result.Message}");
            return Task.CompletedTask;
        }
        finally
        {
            try { pending?.Dispose(); }
            finally
            {
                try { if (capturedRegion) target!.SearchRegion = previousRegion; }
                finally
                {
                    ToolResult? retired = null;
                    bool release;
                    lock (_gate)
                    {
                        _running = false;
                        release = _disposed;
                        if (release) { retired = Result; Result = null; }
                    }
                    if (release) Release(retired);
                }
            }
        }
    }

    /// <summary>运行期间只登记释放请求，等实际 SDK 调用结束后释放，且不发布迟到结果。</summary>
    public void Dispose()
    {
        ToolResult? result;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_running) return;
            result = Result;
            Result = null;
        }
        Release(result);
    }

    private void Release(ToolResult? result)
    {
        try
        {
            try { SyncOutputValues(); }
            finally { result?.Dispose(); }
        }
        finally { _tool.Dispose(); }
    }
}
