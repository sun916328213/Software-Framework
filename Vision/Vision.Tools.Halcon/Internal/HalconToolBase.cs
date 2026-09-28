// ─────────────────────────────────────────────────────────────────────────────
// P3 —— Vision.Tools.Halcon 标准化基类（《Vision_Architecture_Optimization_Plan v1.2》§7）。
//
// §7 要求每个 Tool 建立 Parameters / Result / Validate / Run + Dispose。
// 本基类只定“形状”，不定算法：派生类提供 ValidateCore + RunCore，
// 基类统一负责计时（ElapsedMs）、校验结论回填（Result.Validation）、
// 运行前拦截（校验不通过直接返回失败结果，不碰 HALCON Runtime）。
//
// ⛔ 与旧 HVisionTool（DPLibrary_Halcon_F48/Tools/IHVisionTool.cs）的关系：
//    旧基类是迁移源，P3 不继承它（那会构成新工程→旧工程引用，违反 P1 守卫），
//    只复用它的算法思想。旧工程 P9 退场前两者并存。
// ─────────────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using Vision.Core.Results;

namespace Vision.Tools.Halcon.Internal;

/// <summary>
/// HALCON 传统 Tool 标准化基类。
/// </summary>
/// <typeparam name="TParameters">运行参数类型（纯数据，只含 Core/BCL 类型）。</typeparam>
/// <typeparam name="TResult">结果类型（须派生自 <see cref="ToolResult"/>）。</typeparam>
public abstract class HalconToolBase<TParameters, TResult> : IDisposable
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
    /// 运行前校验。只检查参数/模型/ROI，不碰图像与 Runtime，
    /// 因此普通 CI 无需 HALCON License 即可执行。
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
    /// <param name="image">HALCON 图像（调用方持有所有权，本方法不释放）。</param>
    public TResult Run(HalconDotNet.HObject? image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
            var result = RunCore(image, Parameters ?? new TParameters());
            sw.Stop();
            result.Success = true;
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

    /// <summary>派生类实现真正的 HALCON 执行（需要 Runtime；集成测试单独分组）。</summary>
    protected abstract TResult RunCore(HalconDotNet.HObject? image, TParameters parameters);

    /// <summary>释放模型句柄等原生资源。派生类重写 <see cref="DisposeManaged"/>。</summary>
    protected virtual void DisposeManaged()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeManaged();
        GC.SuppressFinalize(this);
    }
}
