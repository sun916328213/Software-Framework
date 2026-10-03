// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：OpenCvToolBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Diagnostics、Vision.Core.Imaging、Vision.Core.Results。导入不代表所有类型都会在运行时加载。
// ─────────────────────────────────────────────────────────────────────────────
// P4 —— Vision.Tools.OpenCv 标准化基类（计划 §7，与 P3 HalconToolBase 同形）。
//
// 差异只有输入类型：HALCON 侧以其原生图像类型为入参，OpenCV 侧 Run(VisionImage) ——
// VisionImage 是 Core 稳定类型，Mat 只在 Internal 边界内存在（计划 §5.1）。
// 普通 CI 可直接构造 VisionImage 跑完全部 P4 Tool，无需相机 / GPU / License。
// ─────────────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using Vision.Core.Imaging;
using Vision.Core.Results;

namespace Vision.Tools.OpenCv.Internal;

/// <summary>
/// OpenCV Tool 标准化基类。
/// </summary>
/// <typeparam name="TParameters">运行参数类型（纯数据，只含 Core/BCL 类型）。</typeparam>
/// <typeparam name="TResult">结果类型（须派生自 <see cref="ToolResult"/>）。</typeparam>
public abstract class OpenCvToolBase<TParameters, TResult> : IDisposable
    where TParameters : class, new()
    where TResult : ToolResult, new()
{
    private bool _disposed;

    /// <summary>工具名称（Workbench Tool Browser 显示用）。</summary>
    public abstract string Name { get; }

    /// <summary>运行参数。</summary>
    public TParameters Parameters { get; set; } = new();

    /// <summary>上次运行耗时（毫秒）。</summary>
    public long LastElapsedMs { get; private set; }

    /// <summary>
    /// 运行前校验。只检查参数，不碰图像，因此普通 CI 无需任何运行时即可执行。
    /// </summary>
    public ValidationResult Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ValidateCore(Parameters ?? new TParameters());
    }

    /// <summary>派生类实现参数校验。</summary>
    protected abstract ValidationResult ValidateCore(TParameters parameters);

    /// <summary>
    /// 标准执行入口：先 <see cref="Validate"/>，不通过则直接返回失败结果；
    /// 通过则计时执行 <see cref="RunCore"/>，异常转为失败结果（不抛）。
    /// </summary>
    /// <param name="image">输入图像（调用方保留所有权）。</param>
    public TResult Run(VisionImage? image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LastElapsedMs = 0;
        var validation = Validate();
        if (!validation.IsValid)
        {
            return new TResult
            {
                Success = false,
                Message = validation.ToMessage(),
                Validation = validation,
                ElapsedMs = 0,
            };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            ArgumentNullException.ThrowIfNull(image);
            var result = RunCore(image, Parameters ?? new TParameters());
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            result.Validation = validation;
            LastElapsedMs = result.ElapsedMs;
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LastElapsedMs = sw.ElapsedMilliseconds;
            return new TResult
            {
                Success = false,
                Message = ex.Message,
                Validation = validation,
                ElapsedMs = sw.ElapsedMilliseconds,
            };
        }
    }

    /// <summary>派生类实现真正的 OpenCV 执行。</summary>
    protected abstract TResult RunCore(VisionImage image, TParameters parameters);

    /// <summary>释放原生资源。P4 首批 Tool 均无跨 Run 句柄，默认空实现。</summary>
    protected virtual void DisposeManaged()
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 DisposeManaged、SuppressFinalize；具体条件和先后次序见方法体。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeManaged();
        GC.SuppressFinalize(this);
    }
}
