// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionResult。成员说明可结合下方具体实现阅读。
namespace MotionControl.Core.Definitions;

/// <summary>
/// 不带返回值的运动控制操作结果。
/// <para>P1 从旧 <c>MotionControl.Definition.MotionResult</c> 迁移，语义不变。</para>
/// </summary>
public readonly record struct MotionResult
{
    /// <summary>
    /// 初始化 MotionResult 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    private MotionResult(EMotionResult code, string? message, int? vendorCode)
    {
        _code = code;
        _initialized = true;
        Message = message;
        VendorCode = vendorCode;
    }

    private readonly EMotionResult _code;
    private readonly bool _initialized;

    /// <summary>结果码。</summary>
    public EMotionResult Code => _initialized ? _code : EMotionResult.Fail;

    /// <summary>面向诊断的错误说明。</summary>
    public string? Message { get; }

    /// <summary>厂商SDK原始错误码（如果可用）。</summary>
    public int? VendorCode { get; }

    /// <summary>操作是否成功。</summary>
    public bool IsSuccess => Code == EMotionResult.Success;

    /// <summary>创建成功结果。</summary>
    public static MotionResult Success() => new(EMotionResult.Success, null, null);

    /// <summary>创建失败结果。</summary>
    public static MotionResult Failure(EMotionResult code, string? message = null, int? vendorCode = null)
    {
        if (code == EMotionResult.Success)
            throw new ArgumentException("失败结果不能使用 Success 结果码。", nameof(code));
        return new MotionResult(code, message, vendorCode);
    }
}

/// <summary>
/// 同时携带返回值和错误语义的运动控制结果。
/// <para>仅当 <see cref="IsSuccess"/> 为 true 时，<see cref="Value"/> 才有效。</para>
/// <para>P1 从旧 <c>MotionControl.Definition.MotionResult{T}</c> 迁移，语义不变。</para>
/// </summary>
public readonly record struct MotionResult<T>
{
    /// <summary>
    /// 初始化 MotionResult 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    private MotionResult(EMotionResult code, T value, string? message, int? vendorCode)
    {
        _code = code;
        _initialized = true;
        Value = value;
        Message = message;
        VendorCode = vendorCode;
    }

    private readonly EMotionResult _code;
    private readonly bool _initialized;

    /// <summary>结果码。</summary>
    public EMotionResult Code => _initialized ? _code : EMotionResult.Fail;

    /// <summary>成功时的返回值。</summary>
    public T Value { get; }

    /// <summary>面向诊断的错误说明。</summary>
    public string? Message { get; }

    /// <summary>厂商SDK原始错误码（如果可用）。</summary>
    public int? VendorCode { get; }

    /// <summary>操作是否成功。</summary>
    public bool IsSuccess => Code == EMotionResult.Success;

    /// <summary>创建成功结果。</summary>
    public static MotionResult<T> Success(T value) => new(EMotionResult.Success, value, null, null);

    /// <summary>创建失败结果。</summary>
    public static MotionResult<T> Failure(EMotionResult code, string? message = null, int? vendorCode = null)
    {
        if (code == EMotionResult.Success)
            throw new ArgumentException("失败结果不能使用 Success 结果码。", nameof(code));
        return new MotionResult<T>(code, default!, message, vendorCode);
    }

    /// <summary>成功时返回值，否则返回 false。</summary>
    public bool TryGetValue(out T value)
    {
        value = Value;
        return IsSuccess;
    }
}
