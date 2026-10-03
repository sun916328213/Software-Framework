// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。
// 本文件定义：DataFormat。成员说明可结合下方具体实现阅读。
namespace Communication.Core.Abstractions;

/// <summary>
/// 设备侧 32 位数据的字节与字顺序。
/// 字母按标准大端值从最高有效字节到最低有效字节命名。
/// </summary>
public enum DataFormat
{
    ABCD,
    BADC,
    CDAB,
    DCBA,
}
