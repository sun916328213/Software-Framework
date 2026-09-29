// P0 骨架占位：阶段 3 再迁移 MotionManager / MotionControlBase / DeviceCollection / Internal / Vendors。
// 本文件仅验证 Runtime -> Core 的引用方向可编译。
using MotionControl.Core.Abstractions;

namespace MotionControl.Runtime.Services;

/// <summary>Runtime 层程序集标记，P0 骨架用。正式服务在阶段 3 迁入。</summary>
public static class RuntimeAssemblyMarker
{
    /// <summary>程序集标识（引用 Core 标记，避免引用被裁剪）。</summary>
    public static string AssemblyId => CoreAssemblyMarker.AssemblyId + ".Runtime";
}
