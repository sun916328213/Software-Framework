// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionDeviceResult、MotionBatchResult。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using System.Collections.ObjectModel;

namespace MotionControl.Core.Definitions;

/// <summary>一次批量操作中单个设备的执行结果。</summary>
public readonly record struct MotionDeviceResult(int DeviceIndex, string DeviceName, MotionResult Result);

/// <summary>
/// 保持设备顺序的批量运动操作结果。
/// <para>P1 从旧 <c>MotionControl.Definition.MotionBatchResult</c> 迁移。</para>
/// <para>分层调整：构造器由 internal 改为 public，使跨程序集的 <c>MotionControl.Runtime</c> 可构造批量结果；语义不变。</para>
/// </summary>
public sealed class MotionBatchResult
{
    private readonly ReadOnlyCollection<MotionDeviceResult> _results;

    /// <summary>按设备顺序构造批量结果。</summary>
    public MotionBatchResult(IEnumerable<MotionDeviceResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        _results = Array.AsReadOnly(results.ToArray());
    }

    /// <summary>每个设备的执行结果，顺序与管理器设备集合一致。</summary>
    public IReadOnlyList<MotionDeviceResult> Results => _results;

    /// <summary>所有设备是否均执行成功；空集合视为成功。</summary>
    public bool IsSuccess => _results.All(item => item.Result.IsSuccess);

    /// <summary>执行成功的设备数量。</summary>
    public int SuccessCount => _results.Count(item => item.Result.IsSuccess);

    /// <summary>执行失败的设备数量。</summary>
    public int FailureCount => _results.Count - SuccessCount;
}
