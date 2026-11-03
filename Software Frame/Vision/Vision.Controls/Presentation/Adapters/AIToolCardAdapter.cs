using Vision.Controls;
using Vision.Core.Imaging;
using Vision.Core.Results;

#nullable enable
namespace Vision.Controls.ViewModels;

/// <summary>AI Tool 适配器（Provider 由外部注入；执行前需 EnsureLoaded）。</summary>
public sealed class AIToolCardAdapter<TParameters, TResult> : IExecutableTool
    where TParameters : Vision.Tools.AI.AIModelParameters, new()
    where TResult : ToolResult, new()
{
    private readonly Vision.Tools.AI.Internal.AIToolBase<TParameters, TResult> _tool;
    private bool _disposed;

    /// <summary>包装一个 AI Tool。</summary>
    public AIToolCardAdapter(Vision.Tools.AI.Internal.AIToolBase<TParameters, TResult> tool)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
    }

    /// <inheritdoc/>
    public string Name => _tool.Name;

    /// <inheritdoc/>
    public long LastElapsedMs => _tool.LastElapsedMs;

    /// <summary>注入的 Provider（透传给 Tool）。</summary>
    public Vision.Core.AI.IAIProvider? Provider
    {
        get => _tool.Provider;
        set => _tool.Provider = value;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 Validate。
    /// 正文中的关键协作调用包括 ThrowIf、Validate；具体条件和先后次序见方法体。
    /// </remarks>
    public ValidationResult Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tool.Validate();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 ThrowIf、Run；具体条件和先后次序见方法体。
    /// </remarks>
    public ToolResult Run(VisionImage? image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tool.Run(image);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 处理当前类型提供的操作；对应入口为 EnsureLoaded。
    /// 正文中的关键协作调用包括 ThrowIf、LoadModel；具体条件和先后次序见方法体。
    /// </remarks>
    public void EnsureLoaded()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_tool.IsModelLoaded)
            _tool.LoadModel();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 Dispose、SuppressFinalize；具体条件和先后次序见方法体。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _tool.Dispose();
        GC.SuppressFinalize(this);
    }
}
