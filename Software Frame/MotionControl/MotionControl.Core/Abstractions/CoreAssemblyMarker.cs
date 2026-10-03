// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：CoreAssemblyMarker。成员说明可结合下方具体实现阅读。
// P0 骨架占位：阶段 2 再从旧 MotionControl 迁移 IMotionCard / IMotionDevice / Definition。
// 本文件仅保证程序集非空且命名空间存在，不承载任何行为。
namespace MotionControl.Core.Abstractions;

/// <summary>Core 层程序集标记，P0 骨架用。正式契约在阶段 2 迁入。</summary>
public static class CoreAssemblyMarker
{
    /// <summary>程序集标识。</summary>
    public const string AssemblyId = "MotionControl.Core";
}
