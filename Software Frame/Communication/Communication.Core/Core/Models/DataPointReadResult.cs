// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DataPointReadResult。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Core.Interfaces。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Core.Interfaces;

namespace Communication.Core.Models;

/// <summary>表示批量读取中一个数据点的原始结果。</summary>
public sealed class DataPointReadResult
{
    /// <summary>
    /// 初始化 DataPointReadResult 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// </summary>
    private DataPointReadResult(IDataPoint point, byte[]? rawValue, Exception? error)
    {
        Point = point;
        RawValue = rawValue;
        Error = error;
    }

    /// <summary>结果对应的数据点。</summary>
    public IDataPoint Point { get; }

    /// <summary>成功读取的原始字节；失败时为空。</summary>
    public byte[]? RawValue { get; }

    /// <summary>读取错误；成功时为空。</summary>
    public Exception? Error { get; }

    /// <summary>创建成功结果。</summary>
    public static DataPointReadResult Success(IDataPoint point, byte[] rawValue)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(rawValue);
        return new DataPointReadResult(point, rawValue.ToArray(), null);
    }

    /// <summary>创建失败结果。</summary>
    public static DataPointReadResult Failure(IDataPoint point, Exception error)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(error);
        return new DataPointReadResult(point, null, error);
    }
}
