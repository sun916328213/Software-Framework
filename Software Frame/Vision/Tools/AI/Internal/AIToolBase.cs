// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：AIToolBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Vision.Tools.AI、System.Diagnostics、Vision.Core.AI、Vision.Core.Imaging、Vision.Core.Results、Vision.Core.Roi。导入不代表所有类型都会在运行时加载。
using Vision.Tools.AI;
// ─────────────────────────────────────────────────────────────────────────────
// P5 —— Vision.Tools.AI 标准化基类（《Vision_Architecture_Optimization_Plan v1.2》§6、§7）。
//
// §6 的分工：“Vision.Tools.AI 只定义分类/检测/分割/异常检测这些工具怎么工作；
// Vision.AI.* 只负责某家 SDK 如何加载模型、执行推理、释放资源并转换结果。”
// ⇒ 本基类只定“形状”，不认识任何一家 Provider 实现：
//    Provider 由 Workbench / 正式应用注入（§3），Tool 内⛔ 不写品牌 if/else。
//
// 生命周期（§7「Train / Load / Unload」＋ P2/IAIModelSession 三条契约）：
//   · LoadModel：显式建模，失败抛异常且不留半加载会话（失败回滚）；
//   · UnloadModel：经打开它的 Provider 释放，失败上抛并保留会话状态；
//   · Dispose：幂等兜底、不抛（与 UnloadModel 分工，见 IAIProvider 注释）；
//   · Run：只消费已加载会话，未加载直接返回失败结果（不抛、不隐式加载）。
// ─────────────────────────────────────────────────────────────────────────────

using System.Diagnostics;
using Vision.Core.AI;
using Vision.Core.Imaging;
using Vision.Core.Results;
using Vision.Core.Roi;

namespace Vision.Tools.AI.Internal;

/// <summary>
/// 统一 AI Tool 标准化基类。
/// </summary>
/// <typeparam name="TParameters">运行参数类型（须派生自 <see cref="AIModelParameters"/>）。</typeparam>
/// <typeparam name="TResult">结果类型（Core 公共结果，即统一结果语义本身）。</typeparam>
public abstract class AIToolBase<TParameters, TResult> : IDisposable
    where TParameters : AIModelParameters, new()
    where TResult : ToolResult, new()
{
    private bool _disposed;
    private IAIModelSession? _session;
    private IAIProvider? _sessionOwner;
    private string? _loadedModelPath;
    private string? _loadedDeviceId;

    /// <summary>工具名称（Workbench Tool Browser 显示用）。</summary>
    public abstract string Name { get; }

    /// <summary>本工具的模型任务类型（会话类型必须与之一致）。</summary>
    public abstract AIModelType ModelType { get; }

    /// <summary>本工具要求的能力位（Validate 时向 Provider 声明查验）。</summary>
    public abstract AICapabilities RequiredCapability { get; }

    /// <summary>运行参数。</summary>
    public TParameters Parameters { get; set; } = new();

    /// <summary>可选的图像 ROI；当前支持轴对齐的整数像素矩形，null 表示整图。</summary>
    public IRoi? Roi { get; set; }

    /// <summary>
    /// 注入的 Provider（Workbench / 正式应用装配；测试注入 fake）。
    /// 只决定下一次 <see cref="LoadModel"/> 用谁，已加载会话归属不变
    /// （切换前请先 <see cref="UnloadModel"/>；<see cref="Dispose"/> 按归属释放）。
    /// </summary>
    public IAIProvider? Provider { get; set; }

    /// <summary>当前已加载的会话；null 表示未加载。</summary>
    public IAIModelSession? Session => _session;

    /// <summary>模型当前是否已加载且可用。</summary>
    public bool IsModelLoaded =>
        _session?.IsLoaded == true &&
        ReferenceEquals(_sessionOwner, Provider) &&
        string.Equals(_loadedModelPath, Parameters?.ModelPath, StringComparison.Ordinal) &&
        string.Equals(_loadedDeviceId, Parameters?.DeviceId ?? string.Empty, StringComparison.Ordinal);

    /// <summary>上次推理耗时（毫秒，取会话结果自带值）。</summary>
    public long LastElapsedMs { get; private set; }

    /// <summary>
    /// 运行前校验：Provider、模型路径、参数区间、能力声明、会话类型。
    /// 不碰图像与 Runtime，普通 CI 可执行。
    /// </summary>
    public ValidationResult Validate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = Parameters ?? new TParameters();
        var result = new ValidationResult();

        if (Provider == null)
        {
            result.AddError("PROVIDER_MISSING", "未注入 IAIProvider（由 Workbench / 正式应用装配，测试注入 fake）。", nameof(Provider));
            return result;
        }

        if (string.IsNullOrWhiteSpace(parameters.ModelPath))
            result.AddError("MODEL_PATH_EMPTY", "模型路径为空。", nameof(AIModelParameters.ModelPath));

        ValidateParameters(parameters, result);

        if (!Provider.Capabilities.HasFlag(RequiredCapability))
            result.AddError("CAPABILITY_UNSUPPORTED",
                $"提供方 {Provider.Name} 未声明 {RequiredCapability} 能力（三家不强求 1:1，见计划 §6）。",
                nameof(Provider));

        if (!Provider.IsAvailable)
            result.AddWarning("PROVIDER_UNAVAILABLE",
                $"提供方 {Provider.Name} 当前不可用（SDK / DLL / License 缺失）；仍可走完全流程，加载时将暴露真实错误。",
                nameof(Provider));

        if (_session != null && _session.ModelType != ModelType)
            result.AddError("SESSION_TYPE_MISMATCH",
                $"已加载会话类型为 {_session.ModelType}，本工具要求 {ModelType}。", "Session");

        return result;
    }

    /// <summary>派生类校验自有参数（阈值区间等）；基类已查完 Provider/路径/能力。</summary>
    protected virtual void ValidateParameters(TParameters parameters, ValidationResult result)
    {
        if (double.IsNaN(parameters.ScoreThreshold) || parameters.ScoreThreshold > 1)
            result.AddError("SCORE_THRESHOLD", $"置信度阈值 {parameters.ScoreThreshold} 非法（须 ≤1；≤0 表示用 Provider 默认值）。", nameof(AIModelParameters.ScoreThreshold));
    }

    /// <summary>
    /// 加载模型（打开会话）。先校验，不通过抛 <see cref="InvalidOperationException"/>；
    /// 打开失败（抛异常 / 返回 null / 类型不符 / 未加载）时不留半加载会话，调用方可直接重试。
    /// 已加载时先释放旧会话再打开（替换语义，与旧 AITool.Config 一致）。
    /// </summary>
    /// <exception cref="InvalidOperationException">校验不通过或打开失败。</exception>
    public void LoadModel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var validation = Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException($"模型加载前校验不通过：{validation.ToMessage()}");

        UnloadModel();

        var parameters = Parameters ?? new TParameters();
        var request = new AIModelRequest
        {
            ModelPath = parameters.ModelPath,
            ModelType = ModelType,
            DeviceId = parameters.DeviceId ?? string.Empty,
        };

        var provider = Provider!;
        IAIModelSession? session;
        try
        {
            session = provider.OpenSession(request);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"提供方 {provider.Name} 打开会话失败：{ex.Message}", ex);
        }

        if (session == null)
            throw new InvalidOperationException($"提供方 {provider.Name} 返回了空会话。");
        if (session.ModelType != ModelType)
        {
            TryCloseQuietly(provider, session);
            throw new InvalidOperationException($"会话类型为 {session.ModelType}，本工具要求 {ModelType}。");
        }
        if (!session.IsLoaded)
        {
            TryCloseQuietly(provider, session);
            throw new InvalidOperationException($"提供方 {provider.Name} 返回了未加载的会话。");
        }

        _session = session;
        _sessionOwner = provider;
        _loadedModelPath = request.ModelPath;
        _loadedDeviceId = request.DeviceId;
    }

    /// <summary>
    /// 卸载模型（关闭会话）。无会话时无操作。
    /// 关闭失败上抛并保留会话状态（调用方看得见失败；兜底清理走 <see cref="Dispose"/>）。
    /// </summary>
    public void UnloadModel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var session = _session;
        if (session == null)
            return;

        var owner = _sessionOwner;
        try
        {
            owner?.CloseSession(session);
        }
        catch
        {
            // 保留状态并上抛（P2 会话契约第 ② 条）：_session 不清空。
            throw;
        }

        _session = null;
        _sessionOwner = null;
        _loadedModelPath = null;
        _loadedDeviceId = null;
    }

    /// <summary>
    /// 标准执行入口：校验→会话→推理→后过滤，全程不抛异常，失败转为失败结果。
    /// </summary>
    /// <param name="image">输入图像（调用方保留所有权）。</param>
    public TResult Run(VisionImage? image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LastElapsedMs = 0;
        var validation = Validate();
        if (!validation.IsValid)
            return Failed(validation, validation.ToMessage(), 0);

        var session = _session;
        if (session == null || !session.IsLoaded)
            return Failed(validation, "模型尚未加载，请先调用 LoadModel。", 0);
        if (!ReferenceEquals(_sessionOwner, Provider))
            return Failed(validation, "Provider 已切换；请先卸载旧会话并重新加载模型。", 0);
        if (!IsModelLoaded)
            return Failed(validation, "模型路径或设备参数已更改；请重新加载模型。", 0);
        if (session.ModelType != ModelType)
            return Failed(validation, $"会话类型为 {session.ModelType}，本工具要求 {ModelType}。", 0);

        var parameters = Parameters ?? new TParameters();
        var sw = Stopwatch.StartNew();
        try
        {
            ArgumentNullException.ThrowIfNull(image);
            var prepared = Roi == null ? (AIRoiImageAdapter.PreparedImage?)null : AIRoiImageAdapter.Crop(image, Roi);
            var request = new AIInferenceRequest
            {
                Image = prepared?.Image ?? image,
                Roi = null, // 已在 Tool 层裁剪，Provider 只接收整张输入图。
                ScoreThreshold = parameters.ScoreThreshold,
                MaxResults = parameters.MaxResults,
            };

            var raw = session.Run(request);
            sw.Stop();
            if (raw is not TResult typed)
            {
                LastElapsedMs = sw.ElapsedMilliseconds;
                return Failed(validation,
                    $"会话返回了 {raw?.GetType().Name ?? "null"}，本工具要求 {typeof(TResult).Name}（Provider 结果转换错误，归 P6）。",
                    LastElapsedMs);
            }

            typed.Validation = validation;
            if (typed.Success && prepared is { } cropped)
                AIRoiImageAdapter.RestoreCoordinates(typed, cropped);
            FilterResult(typed, parameters);
            LastElapsedMs = typed.ElapsedMs;
            return typed;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LastElapsedMs = sw.ElapsedMilliseconds;
            return Failed(validation, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Tool 层后过滤（“工具怎么工作”的一部分）：Provider 可能忽略阈值/数量，
    /// Tool 按统一语义二次保证。默认无操作，检测/分割类 Tool 重写。
    /// </summary>
    protected virtual void FilterResult(TResult result, TParameters parameters)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// 正文中的关键协作调用包括 CloseSession、Dispose、SuppressFinalize；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        // 兜底清理：不抛（P2 会话契约第 ③ 条；失败路径由 UnloadModel 显式处理）。
        try
        {
            var session = _session;
            _session = null;
            var owner = _sessionOwner;
            _sessionOwner = null;
            _loadedModelPath = null;
            _loadedDeviceId = null;
            if (session != null)
            {
                if (owner != null)
                    owner.CloseSession(session);
                else
                    session.Dispose();
            }
        }
        catch
        {
            // 故意吞掉：Dispose 在 finally/终结路径调用，抛异常只会掩盖原始错误。
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Failed。
    /// </summary>
    private static TResult Failed(ValidationResult validation, string message, long elapsedMs) =>
        new() { Success = false, Message = message, Validation = validation, ElapsedMs = elapsedMs };

    /// <summary>
    /// 尝试执行对应操作并按当前约定处理结果；对应入口为 TryCloseQuietly。
    /// 正文中的关键协作调用包括 CloseSession、Dispose；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    private static void TryCloseQuietly(IAIProvider? provider, IAIModelSession session)
    {
        try
        {
            if (provider != null)
                provider.CloseSession(session);
            else
                session.Dispose();
        }
        catch
        {
            // 类型不符的会话已不可用，关闭失败不掩盖主错误。
        }
    }
}
