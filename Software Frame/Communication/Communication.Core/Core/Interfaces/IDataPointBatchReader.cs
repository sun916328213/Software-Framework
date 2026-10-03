// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：IDataPointBatchReader。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Models。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Models;

namespace Communication.Core.Interfaces;

/// <summary>
/// 声明设备能将多个数据点合并为更少的底层读取操作。
/// </summary>
public interface IDataPointBatchReader
{
    /// <summary>
    /// 读取给定点位并为每个输入点位返回一个成功或失败结果。
    /// </summary>
    Task<IReadOnlyList<DataPointReadResult>> ReadPointsAsync(
        IReadOnlyList<IDataPoint> points,
        CancellationToken cancellationToken = default);
}
